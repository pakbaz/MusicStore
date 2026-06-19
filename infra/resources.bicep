param environmentName string
param location string
@minLength(13)
param resourceToken string

@secure()
param adminPassword string

param cosmosEnableFreeTier bool = false
param musicgenCpu string = '3.5'
param musicgenMemory string = '24Gi'
param musicgenWorkloadProfileType string = 'E4'
param webImageName string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
param musicgenImageName string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

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
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
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
    publicNetworkAccess: 'Enabled'
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
              value: 'cpu'
            }
            {
              name: 'ACESTEP_CONFIG_PATH'
              value: 'acestep-v15-turbo'
            }
            {
              name: 'HF_HOME'
              value: '/models'
            }
          ]
          resources: {
            cpu: json(musicgenCpu)
            memory: musicgenMemory
          }
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

resource webContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: webContainerAppName
  location: location
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
