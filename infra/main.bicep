targetScope = 'subscription'

@minLength(1)
param environmentName string

@minLength(1)
param location string

@secure()
#disable-next-line secure-parameter-default
param adminPassword string = 'ChangeThis1!'

param cosmosEnableFreeTier bool = false
param musicgenCpu string = '3.5'
param musicgenMemory string = '24Gi'
param musicgenMinReplicas int = 1

@description('Container image for the web service. Defaults to the placeholder; azd/CI override with the built image.')
param webImageName string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

@description('Container image for the musicgen service. Defaults to the placeholder; azd/CI override with the built image.')
param musicgenImageName string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

var resourceToken = uniqueString(subscription().id, environmentName, location)
var resourceGroupName = 'rg-${environmentName}'

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

module resources 'resources.bicep' = {
  name: 'resources-${resourceToken}'
  scope: rg
  params: {
    environmentName: environmentName
    location: location
    resourceToken: resourceToken
    adminPassword: adminPassword
    cosmosEnableFreeTier: cosmosEnableFreeTier
    musicgenCpu: musicgenCpu
    musicgenMemory: musicgenMemory
    musicgenMinReplicas: musicgenMinReplicas
    webImageName: webImageName
    musicgenImageName: musicgenImageName
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output WEB_BASE_URL string = resources.outputs.WEB_BASE_URL
output AZURE_COSMOS_ENDPOINT string = resources.outputs.AZURE_COSMOS_ENDPOINT
output AZURE_STORAGE_BLOB_ENDPOINT string = resources.outputs.AZURE_STORAGE_BLOB_ENDPOINT
output MUSICGEN_INTERNAL_FQDN string = resources.outputs.MUSICGEN_INTERNAL_FQDN
