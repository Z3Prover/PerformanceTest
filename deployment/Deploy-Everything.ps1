<#
 .SYNOPSIS
    Builds and deploys z3 performance testing environment.

 .DESCRIPTION
    Builds and deploys all the entities required to run performance tests of z3 in Azure. These include:
    * Resource group that contains everything else (if a resource group with the given name exists already, it will be used instead of creating a new one).
    * Storage account (if a storage account with the given name exists already, it will be used instead of creating a new one).
    * Batch account (if a batch account with the given name exists already, it will be used instead of creating a new one).
    * Key vault where keys to storage and batch are securely kept (if a key vault with the given name exists already, it will be used instead of creating a new one).
    * Nightly web application  (if a web application with the given name exists already, it will be updated instead of creating a new one).
    * Reference experiment settings and data (if provided).
    * Azure worker - application that measures performance in azure batch.
    * Nightly runner - application that starts nightly performance tests. Is scheduled to run in batch every 24 hours.
    * Azure Active Directory (AAD) application - an entity required to authenticate azure worker, nightly web app, and nightly runner in azure (if an AAD application with the given name exists already, you will be prompted to use it or create a new one).
    * A self-signed certificate as credentials for AAD application.


 .PARAMETER name
    Name of the deployment. Used for resource group and everything else unless drectly specified otherwise.

 .PARAMETER certPassword
    Password for the private key of the self-signed certificate used as credentials for AAD application.

 .PARAMETER connectionStringSecretName
    Name of the secret in which connection string to the environment (keys to storage and batch) is kept. Default is "connectionString".

 .PARAMETER certPfxPath
    Path to the pfx file with self-signed certificate to use as credentials for AAD application. If not provided, a new certificate is created and pfx with it is saved in the current directory.

 .PARAMETER location
    Location of azure datacenter to use. Default is "West Europe"

 .PARAMETER storageName
    Custom name for the storage account. Defaults to $name.ToLowerInvariant().

 .PARAMETER batchName
    Custom name for the batch account. Defaults to $name.ToLowerInvariant().

 .PARAMETER keyVaultName
    Custom name for the key vault. Defaults to $name.ToLowerInvariant().

 .PARAMETER webAppName
    Custom name for the nightly web app. Defaults to $name.ToLowerInvariant().

 .PARAMETER referenceJsonPath
    Path to json describing reference experiment. If not provided, reference experiment will not be deployed.

 .PARAMETER referenceExecutablePath
    Path to executable (or zip) which should be used in reference experiment. If not provided, reference experiment will not be deployed.

 .PARAMETER referenceInputPath
    Path to input file for the reference experiment. If not provided, reference experiment will still be deployed, but required input files should be uploaded separately.

 .PARAMETER poolNameForNightlyRuns
    Name of the batch pool on which nightly runs should be scheduled. By default first pool on the account will be used.

 .PARAMETER poolNameForRunner
    Name of the batch pool on which nightly runner application (the one that schedules nightly tests) will run. By default first pool on the account will be used.
    AAD app service principal, that should have access to secrets in the vault.

 .PARAMETER sendEmailCredentialsSecretId
    Name of a secret in the Key Vault which keeps user name and password divided by :. E.g. user@foo.com:password. This email is used to send reports of nightly tests.

 .PARAMETER sendEmailCredentials
    User name and password divided by :. E.g. user@foo.com:password. This email is used to send reports of nightly tests. If the property value is an empty string, no e-mail is sent.

 .PARAMETER reportRecipients
    String with e-mail addresses to which reports of nightly tests should be sent separated by semicolon ;.

 .PARAMETER smtpServerUrl
    An address of an SMTP server to send e-mails.

 .PARAMETER certSecretId
    Name of a secret in the Key Vault which keeps certificate used as credentials for AAD application.

 .PARAMETER certPasswordSecretId
    Name of a secret in the Key Vault which keeps password to the private key of the certificate used as credentials for AAD application.

 .OUTPUTS
    Connection string to the deployed performance testing environment.
#>
param(
 [Parameter(Mandatory=$True)]
 [string]
 $name,

 [Parameter(Mandatory=$True)]
 [string]
 $certPassword,

 [string]
 $connectionStringSecretName,

 [string]
 $certPfxPath,

 [string]
 $location,

 [string]
 $storageName,

 [string]
 $batchName,

 [string]
 $keyVaultName,

 [string]
 $webAppName,
 
 [string]
 $referenceJsonPath,
 
 [string]
 $referenceExecutablePath,
 
 [string]
 $referenceInputPath,

 [string]
 $poolNameForNightlyRuns,

 [string]
 $poolNameForRunner,

 [string]
 $sendEmailCredentialsSecretId,

 [string]
 $sendEmailCredentials,

 [string]
 $reportRecipients,

 [string]
 $smtpServerUrl,
 
 [string]
 $certSecretId,
 
 [string]
 $certPasswordSecretId
 )

$ErrorActionPreference = "Stop"

if (-not $connectionStringSecretName) {
    $connectionStringSecretName = "connectionString"
}
if (-not $storageName) {
    $storageName = $name.ToLowerInvariant()
}
if (-not $batchName) {
    $batchName = $name.ToLowerInvariant()
}
if (-not $keyVaultName) {
    $keyVaultName = $name.ToLowerInvariant()
}
if (-not $webAppName) {
    $webAppName = $name.ToLowerInvariant()
}

if (-not $location) {
    $location = "West Europe"
}

 if ([Environment]::Is64BitOperatingSystem) {
    $pfiles = ${env:PROGRAMFILES(X86)}
    $platform = '/p:Platform="x64"'
} else {
    $pfiles = $env:PROGRAMFILES
    $platform = '/p:Platform="x86"'
}
$msbuild = $pfiles + '\MSBuild\14.0\Bin\MSBuild.exe'
if (!(Test-Path $msbuild)) {
    Write-Error -Message 'ERROR: Failed to locate MSBuild at ' + $msbuild
    exit 1
}


if($certPfxPath) {
    Write-Host "Importing certificate..."
    $cert = Import-PfxCertificate -FilePath $certPfxPath -CertStoreLocation Cert:\CurrentUser\My -Password (ConvertTo-SecureString -String $certPassword -Force -AsPlainText) -Exportable
} else {
    Write-Host "Creating certificate..."
    $now = Get-Date
    $endDate = $now.AddYears(5)
    [System.Security.Cryptography.X509Certificates.X509Certificate2]$cert = .\New-Cert.ps1 $name $certPassword $now $endDate
}
Write-Host "Registering AAD application..."
[Microsoft.Azure.Commands.Resources.Models.ActiveDirectory.PSADServicePrincipal]$sp = .\Deploy-AADApp.ps1 $name $cert
Write-Host "Creating resource group, if needed..."
[Microsoft.Azure.Commands.ResourceManager.Cmdlets.SdkModels.PSResourceGroup]$rg = .\Deploy-ResourceGroup.ps1 $name $location
Write-Host "Deploying storage..."
[Microsoft.Azure.Commands.Management.Storage.Models.PSStorageAccount]$storage = .\Deploy-Storage.ps1 $storageName $rg
Write-Host "Deploying batch account..."
[Microsoft.Azure.Commands.Batch.BatchAccountContext]$batch = .\Deploy-Batch.ps1 $batchName $rg $storage $cert $certPassword
Write-Host "Deploying key vault..."
[Microsoft.Azure.Commands.KeyVault.Models.PSVault]$vault = .\Deploy-KeyVault.ps1 $keyVaultName $rg $connectionStringSecretName $storage $batch $sp $sendEmailCredentialsSecretId $sendEmailCredentials $cert $certPassword $certSecretId $certPasswordSecretId
Write-Host "Deploying AzureWorker..."
$null = .\Deploy-AzureWorker.ps1 $connectionStringSecretName $storage $vault $sp $cert.Thumbprint $reportRecipients $smtpServerUrl $sendEmailCredentialsSecretId
Write-Host "Deploying NightlyWebApp..."
[Microsoft.Azure.Management.WebSites.Models.Site]$webApp = .\Deploy-WebApp.ps1 $webAppName $rg $connectionStringSecretName $storage $vault $sp $cert $certPassword
if ($referenceJsonPath -and $referenceExecutablePath) {
    Write-Host "Deploying reference experiment..."
    $null = .\Deploy-ReferenceExperiment.ps1 $storage $referenceJsonPath $referenceExecutablePath $referenceInputPath
}
Write-Host "Deploying NightlyRunner..."
$null = .\Deploy-NightlyRunner.ps1 $rg $connectionStringSecretName $storage $batch $vault $sp $cert.Thumbprint $poolNameForNightlyRuns $poolNameForRunner

$storageKeys = Get-AzureRmStorageAccountKey -Name $storage.StorageAccountName -ResourceGroupName $rg.ResourceGroupName
$stName = $storage.StorageAccountName
$stKey = $storageKeys[0].Value
$batchAccount = Get-AzureRmBatchAccountKeys -AccountName $batch.AccountName -ResourceGroupName $rg.ResourceGroupName
$batchName = $batchAccount.AccountName
$batchKey = $batchAccount.PrimaryAccountKey
$batchAddr = $batchAccount.AccountEndpoint

$connectionString = "DefaultEndpointsProtocol=https;AccountName=$stName;AccountKey=$stKey;BatchAccount=$batchName;BatchURL=https://$batchAddr;BatchAccessKey=$batchKey;"

Remove-Item -Path ("Cert:\CurrentUser\My\" + $cert.Thumbprint) -DeleteKey

$connectionString