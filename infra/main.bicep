// AdaManagement – ingyenes Azure infrastruktúra.
// App Service F1 (Linux) + Azure SQL Database free offer (auto-pause, ha elfogy a havi keret) + 1 €-s budget riasztás.
// Telepítés (erőforráscsoport szinten):
//   az deployment group create -g <rg> -f infra/main.bicep -p infra/main.parameters.json -p sqlAdminPassword=... googleClientSecret=...

@description('Egyedi névelőtag (kisbetű, szám), pl. ada-szile. Ebből lesz a <prefix>.azurewebsites.net cím.')
@minLength(3)
@maxLength(20)
param namePrefix string

param location string = resourceGroup().location

@description('SQL admin felhasználónév.')
param sqlAdminLogin string = 'adaadmin'

@secure()
@description('SQL admin jelszó (erős, legalább 12 karakter).')
param sqlAdminPassword string

@description('Google OAuth kliens azonosító.')
param googleClientId string

@secure()
@description('Google OAuth kliens titok.')
param googleClientSecret string

@description('A belépésre jogosult Google e-mail cím.')
param allowedEmail string

@description('Budget riasztás címzettje.')
param budgetEmail string

@description('A budget kezdőnapja: egy hónap első napja, pl. 2026-11-01. Később nem módosítható.')
param budgetStartDate string

@description('Az SQL szerver neve (globálisan egyedi, kisbetű). Akkor írd át, ha a név foglalt, pl. egy elbukott telepítés után.')
param sqlServerName string = '${namePrefix}-sql'

var webAppName = namePrefix
var planName = '${namePrefix}-plan'
var dbName = 'adamanagement'

// ---------- App Service: F1 (ingyenes) Linux ----------

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  properties: {
    reserved: true // Linux
  }
}

resource web 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      // A publish mappában a kliens runtimeconfig-ja is ott van, ezért a platform magától nem tudja, mit indítson.
      appCommandLine: 'dotnet AdaManagement.Server.dll'
      alwaysOn: false // F1-en nem elérhető
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
        { name: 'Auth__Google__ClientId', value: googleClientId }
        { name: 'Auth__Google__ClientSecret', value: googleClientSecret }
        { name: 'Auth__AllowedEmails__0', value: allowedEmail }
      ]
      connectionStrings: [
        {
          name: 'Default'
          type: 'SQLAzure'
          // Connect Timeout=60: a serverless adatbázis ébredése eltarthat egy ideig.
          connectionString: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${dbName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connect Timeout=60;'
        }
      ]
    }
  }
}

// ---------- Azure SQL: free offer ----------

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Csak Azure-szolgáltatások (az App Service) érhetik el. A GitHub Actions a migráció idejére ideiglenes szabályt vesz fel.
resource allowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource db 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: dbName
  location: location
  sku: {
    name: 'GP_S_Gen5_2' // General Purpose serverless – ez kell a free offerhez
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    useFreeLimit: true
    // FONTOS: ha elfogy a havi ingyenes keret, az adatbázis a hónap végéig szünetel, NEM számláz.
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368 // 32 GB
    requestedBackupStorageRedundancy: 'Local'
    zoneRedundant: false
  }
}

// ---------- Budget riasztás: 1 € / hó ----------

resource budget 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: '${namePrefix}-budget'
  properties: {
    category: 'Cost'
    amount: 1
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: budgetStartDate
    }
    notifications: {
      actualAnyCost: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 1 // a budget 1%-a, vagyis már ~0,01 € tényleges költségnél szól
        thresholdType: 'Actual'
        contactEmails: [ budgetEmail ]
      }
      forecasted: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [ budgetEmail ]
      }
    }
  }
}

output webAppUrl string = 'https://${web.properties.defaultHostName}'
output googleRedirectUri string = 'https://${web.properties.defaultHostName}/signin-google'
output sqlServerName string = sqlServer.name
output webAppName string = web.name
