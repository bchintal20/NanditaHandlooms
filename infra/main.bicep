targetScope = 'subscription'

@minLength(1)
param environmentName string
@allowed(['centralus'])
param location string
param sessionId string
param deployedBy string
param createdAt string

@description('Required SQL administrator login; used for schema bootstrap only, never by the application.')
@minLength(1)
param sqlAdministratorLogin string
@secure()
@minLength(16)
param sqlAdministratorPassword string

@description('TLS SQL connection string for the separate, least-privilege application database user.')
@secure()
@minLength(1)
param boutiqueSqlConnection string
@description('Exact email address associated with the invited boutique-owner platform identity.')
@minLength(1)
param boutiqueOwnerEmail string

@description('Optional single public IPv4 address for temporary schema/bootstrap access. Remove this rule after setup.')
param deploymentClientIp string = ''

var tags = {
  'app-onboard-skill': 'true'
  'app-onboard-session-id': sessionId
  'created-at': createdAt
  environment: environmentName
  'deployed-by': deployedBy
}

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: 'rg-nandita-dev-cbbf'
  location: location
  tags: tags
}

module sql './modules/sql-database.bicep' = {
  name: 'sql-database'
  scope: rg
  params: {
    location: location
    tags: tags
    serverName: 'sql-nandita-dev-cbbf'
    databaseName: 'nandita'
    serverVersion: '12.0'
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorPassword
    deploymentClientIp: deploymentClientIp
  }
}

module site './modules/static-web-app.bicep' = {
  name: 'static-web-app'
  scope: rg
  params: {
    location: location
    tags: tags
    siteName: 'swa-nandita-dev-cbbf'
    boutiqueSqlConnection: boutiqueSqlConnection
    boutiqueOwnerEmail: boutiqueOwnerEmail
  }
}

output resourceGroupName string = rg.name
output siteUrl string = site.outputs.siteUrl
output sqlServerHostname string = sql.outputs.serverHostname
output sqlDatabaseName string = sql.outputs.databaseName
