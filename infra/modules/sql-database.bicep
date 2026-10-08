param location string
param tags object
param serverName string
param databaseName string
param serverVersion string
param administratorLogin string
@secure()
param administratorLoginPassword string
param deploymentClientIp string = ''

resource server 'Microsoft.Sql/servers@2025-01-01' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    version: serverVersion
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Managed SWA Functions lack fixed egress IPs. This exception permits Azure traffic,
// including other tenants; SQL authentication and least-privilege grants remain required.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2025-01-01' = {
  parent: server
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Exactly one optional bootstrap address; never a range permitting internet-wide access.
resource deploymentClient 'Microsoft.Sql/servers/firewallRules@2025-01-01' = if (!empty(deploymentClientIp)) {
  parent: server
  name: 'DeploymentClient'
  properties: {
    startIpAddress: deploymentClientIp
    endIpAddress: deploymentClientIp
  }
}

resource database 'Microsoft.Sql/servers/databases@2025-01-01' = {
  parent: server
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5_2'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    createMode: 'Default'
    maxSizeBytes: 34359738368
    requestedBackupStorageRedundancy: 'Local'
    zoneRedundant: false
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
  }
}

output serverHostname string = server.properties.fullyQualifiedDomainName
output databaseName string = database.name
