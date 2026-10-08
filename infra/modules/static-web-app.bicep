param location string
param tags object
param siteName string
@secure()
param boutiqueSqlConnection string
param boutiqueOwnerEmail string

// Detached token deployment; no repository or CI integration and no separate Functions host.
resource site 'Microsoft.Web/staticSites@2025-05-01' = {
  name: siteName
  location: location
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

// SWA managed Functions do not support managed identity or Key Vault references.
// The secure deploy-time connection string is stored only in protected backend settings.
resource backendSettings 'Microsoft.Web/staticSites/config@2025-05-01' = {
  parent: site
  name: 'appsettings'
  properties: {
    BoutiqueSqlConnection: boutiqueSqlConnection
    BoutiqueOwnerEmail: boutiqueOwnerEmail
    BoutiquePublicOrigin: 'https://${site.properties.defaultHostname}'
  }
}

output siteUrl string = 'https://${site.properties.defaultHostname}'
