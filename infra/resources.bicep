param environmentName string
param location string
@minLength(13)
param resourceToken string

@secure()
param adminPassword string

param cosmosEnableFreeTier bool = false
param musicgenCpu string = '3.5'
param musicgenMemory string = '24Gi'
param musicgenMinReplicas int = 1
param musicgenWorkloadProfileType string = 'E4'
param webImageName string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
param musicgenImageName string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

// Private networking. The Container Apps environment is VNet-integrated so the app can reach
// Cosmos and Storage over private endpoints; both data stores keep public network access disabled
// per subscription policy. The infra subnet must be delegated to Microsoft.App/environments
// (>= /27; /23 recommended for workload-profile environments).
param vnetAddressPrefix string = '10.0.0.0/16'
param infraSubnetPrefix string = '10.0.0.0/23'
param privateEndpointSubnetPrefix string = '10.0.2.0/24'

var logAnalyticsName = take('law-${environmentName}-${resourceToken}', 63)
var containerAppsEnvironmentName = 'cae-${resourceToken}'
var acrName = 'acr${resourceToken}'
var identityName = 'id-${environmentName}-${resourceToken}'
var storageAccountName = 'st${resourceToken}'
var cosmosAccountName = 'cosmos-${resourceToken}'
var databaseName = 'musicstore'
var thumbnailsContainerName = 'thumbnails'
var musicContainerName = 'music'
var webContainerAppName = 'ca-web-${resourceToken}'
var musicgenContainerAppName = 'ca-musicgen-${resourceToken}'
var musicgenWorkloadProfileName = 'musicgen'

var vnetName = 'vnet-${resourceToken}'
var infraSubnetName = 'snet-infra'
var privateEndpointSubnetName = 'snet-private-endpoints'
var cosmosPrivateDnsZoneName = 'privatelink.documents.azure.com'
var blobPrivateDnsZoneName = 'privatelink.blob.${environment().suffixes.storage}'

var acrPullRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
var storageBlobDataContributorRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
var cosmosDataContributorRoleDefinitionGuid = '00000000-0000-0000-0000-000000000002'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressPrefix
      ]
    }
    subnets: [
      {
        name: infraSubnetName
        properties: {
          addressPrefix: infraSubnetPrefix
          delegations: [
            {
              name: 'Microsoft.App.environments'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: privateEndpointSubnetName
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

resource infraSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  parent: virtualNetwork
  name: infraSubnetName
}

resource privateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  parent: virtualNetwork
  name: privateEndpointSubnetName
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppsEnvironmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    vnetConfiguration: {
      // Public ingress for the web app is preserved (internal: false); only egress and the
      // private endpoints flow through the VNet.
      internal: false
      infrastructureSubnetId: infraSubnet.id
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
      {
        name: musicgenWorkloadProfileName
        workloadProfileType: musicgenWorkloadProfileType
        minimumCount: 0
        maximumCount: 2
      }
    ]
  }
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, userAssignedIdentity.id, acrPullRoleDefinitionId)
  scope: containerRegistry
  properties: {
    roleDefinitionId: acrPullRoleDefinitionId
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource thumbnailsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: thumbnailsContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource musicContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: musicContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource storageBlobDataContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, userAssignedIdentity.id, storageBlobDataContributorRoleDefinitionId)
  scope: storageAccount
  properties: {
    roleDefinitionId: storageBlobDataContributorRoleDefinitionId
    principalId: userAssignedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    enableFreeTier: cosmosEnableFreeTier
    publicNetworkAccess: 'Disabled'
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
  }
}

resource cosmosSqlDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

resource cosmosSqlDataContributorAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, userAssignedIdentity.id, cosmosDataContributorRoleDefinitionGuid)
  properties: {
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/${cosmosDataContributorRoleDefinitionGuid}'
    principalId: userAssignedIdentity.properties.principalId
    scope: cosmosAccount.id
  }
}

// ---------------------------------------------------------------------------
// Private connectivity: private endpoints + private DNS so the VNet-integrated
// Container Apps environment resolves Cosmos and Storage to private IPs and the
// app reaches them with its managed identity while public access stays disabled.
// ---------------------------------------------------------------------------

resource cosmosPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: cosmosPrivateDnsZoneName
  location: 'global'
}

resource cosmosPrivateDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: cosmosPrivateDnsZone
  name: 'link-${vnetName}'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource cosmosPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: 'pe-cosmos-${resourceToken}'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'cosmos'
        properties: {
          privateLinkServiceId: cosmosAccount.id
          groupIds: [
            'Sql'
          ]
        }
      }
    ]
  }
}

resource cosmosPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: cosmosPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'cosmos'
        properties: {
          privateDnsZoneId: cosmosPrivateDnsZone.id
        }
      }
    ]
  }
}

resource blobPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: blobPrivateDnsZoneName
  location: 'global'
}

resource blobPrivateDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: blobPrivateDnsZone
  name: 'link-${vnetName}'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}

resource blobPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: 'pe-blob-${resourceToken}'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnet.id
    }
    privateLinkServiceConnections: [
      {
        name: 'blob'
        properties: {
          privateLinkServiceId: storageAccount.id
          groupIds: [
            'blob'
          ]
        }
      }
    ]
  }
}

resource blobPrivateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: blobPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'blob'
        properties: {
          privateDnsZoneId: blobPrivateDnsZone.id
        }
      }
    ]
  }
}

resource musicgenContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: musicgenContainerAppName
  location: location
  tags: {
    'azd-service-name': 'musicgen'
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    workloadProfileName: musicgenWorkloadProfileName
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 8000
        transport: 'http'
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          identity: userAssignedIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'musicgen'
          image: musicgenImageName
          env: [
            {
              name: 'ACESTEP_DEVICE'
              value: 'auto'
            }
            {
              name: 'ACESTEP_CONFIG_PATH'
              value: 'acestep-v15-turbo'
            }
            {
              name: 'ACESTEP_INFERENCE_STEPS'
              value: '8'
            }
            {
              name: 'ACESTEP_INFERENCE_SHIFT'
              value: '3.0'
            }
            {
              name: 'MUSICGEN_PRELOAD_MODEL'
              value: 'true'
            }
            {
              name: 'MUSICGEN_CPU_THREADS'
              value: '4'
            }
            {
              name: 'HF_HOME'
              value: '/models/huggingface'
            }
          ]
          resources: {
            cpu: json(musicgenCpu)
            memory: musicgenMemory
          }
        }
      ]
      scale: {
        minReplicas: musicgenMinReplicas
        maxReplicas: 1
      }
    }
  }
}

resource webContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: webContainerAppName
  location: location
  // Ensure private DNS + endpoints are wired before the app starts, since startup seeding
  // reaches Cosmos and Storage over the private endpoints.
  dependsOn: [
    cosmosPrivateDnsZoneGroup
    blobPrivateDnsZoneGroup
    cosmosSqlDataContributorAssignment
    storageBlobDataContributorAssignment
  ]
  tags: {
    'azd-service-name': 'web'
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      secrets: [
        {
          name: 'default-admin-password'
          value: adminPassword
        }
      ]
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        stickySessions: {
          affinity: 'sticky'
        }
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          identity: userAssignedIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: webImageName
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'Cosmos__Endpoint'
              value: cosmosAccount.properties.documentEndpoint
            }
            {
              name: 'Cosmos__Database'
              value: databaseName
            }
            {
              name: 'Storage__BlobEndpoint'
              value: storageAccount.properties.primaryEndpoints.blob
            }
            {
              name: 'Storage__ThumbnailsContainer'
              value: thumbnailsContainerName
            }
            {
              name: 'Storage__MusicContainer'
              value: musicContainerName
            }
            {
              name: 'MusicGen__BaseUrl'
              value: 'https://${musicgenContainerApp.properties.configuration.ingress.fqdn}'
            }
            {
              // Public base URL used to build absolute links in store emails (unsubscribe, cart, orders).
              // Configure Email__Provider=Acs plus Email__Endpoint/FromAddress (and grant the managed
              // identity an ACS sender role) to switch from the default log-only provider. See README.
              name: 'Email__BaseUrl'
              value: 'https://${webContainerAppName}.${containerAppsEnvironment.properties.defaultDomain}'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: userAssignedIdentity.properties.clientId
            }
            {
              name: 'AppSettings__DefaultAdminUsername'
              value: 'Administrator'
            }
            {
              name: 'AppSettings__DefaultAdminPassword'
              secretRef: 'default-admin-password'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.properties.loginServer
output WEB_BASE_URL string = 'https://${webContainerApp.properties.configuration.ingress.fqdn}'
output AZURE_COSMOS_ENDPOINT string = cosmosAccount.properties.documentEndpoint
output AZURE_STORAGE_BLOB_ENDPOINT string = storageAccount.properties.primaryEndpoints.blob
output MUSICGEN_INTERNAL_FQDN string = musicgenContainerApp.properties.configuration.ingress.fqdn
