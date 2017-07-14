<#
 .SYNOPSIS
    Builds, configures, and deploys AzureWorker.

 .DESCRIPTION

 
 .PARAMETER connectionStringSecretName
    Name of the secret in which connection string to the environment (keys to storage and batch) is kept.

 .PARAMETER storage
    Storage used in deployment.

 .PARAMETER keyVault
    Key vault used in deployment.

 .PARAMETER AADAppServicePrincipal
    AAD app service principal, that havs access to secrets in the vault.

 .PARAMETER certThumbprint
    Thumbprint of the certificate used as credentials for AAD application.

 .PARAMETER reportRecipients
    String with e-mail addresses to which reports of nightly tests should be sent separated by semicolon ;.

 .PARAMETER smtpServerUrl
    An address of an SMTP server to send e-mails.

 .PARAMETER sendEmailCredentialsSecretId
    Name of a secret in the Key Vault which keeps user name and password divided by :. E.g. user@foo.com:password. If the property value is an empty string, no e-mail is sent.

 .PARAMETER linkPage
    Address of nightly web app. Used in email alerts


 .OUTPUTS
#>
param(
 [Parameter(Mandatory=$True)]
 [string]
 $connectionStringSecretName,
 
 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.Management.Storage.Models.PSStorageAccount]
 $storage,
 
 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.KeyVault.Models.PSVault]
 $keyVault,
 
 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.Resources.Models.ActiveDirectory.PSADServicePrincipal]
 $AADAppServicePrincipal,
 
 [Parameter(Mandatory=$True)]
 [string]
 $certThumbprint,

 [string]
 $reportRecipients,

 [string]
 $smtpServerUrl,

 [string]
 $sendEmailCredentialsSecretId,

 [string]
 $linkPage
)

$ErrorActionPreference = "Stop"

$cpath = Get-Location
$cdir = $cpath.Path

Write-Host "Building AzureWorker..."
$null = .\Build-AzureWorker.ps1
$null = mkdir "AzureWorker" -Force
Copy-Item ..\src\AzurePerformanceTest\AzureWorker\bin\Release\*.exe .\AzureWorker
Copy-Item ..\src\AzurePerformanceTest\AzureWorker\bin\Release\*.dll .\AzureWorker
Copy-Item ..\src\AzurePerformanceTest\AzureWorker\bin\Release\*.config .\AzureWorker

Write-Host "Configuring AzureWorker..."
$confPath = Join-Path $cdir "\AzureWorker\AzureWorker.exe.config"
$conf = [xml] (Get-Content $confPath)
($conf.configuration.applicationSettings.'AzureWorker.Properties.Settings'.setting | where {$_.name -eq 'KeyVaultUrl'}).Value = $keyVault.VaultUri
($conf.configuration.applicationSettings.'AzureWorker.Properties.Settings'.setting | where {$_.name -eq 'AADApplicationId'}).Value = $AADAppServicePrincipal.ApplicationId.ToString()
($conf.configuration.applicationSettings.'AzureWorker.Properties.Settings'.setting | where {$_.name -eq 'AADApplicationCertThumbprint'}).Value = $certThumbprint
($conf.configuration.applicationSettings.'AzureWorker.Properties.Settings'.setting | where {$_.name -eq 'ConnectionStringSecretId'}).Value = $connectionStringSecretName
if ($reportRecipients) {
    ($conf.configuration.applicationSettings.'AzureWorker.Properties.Settings'.setting | where {$_.name -eq 'ReportRecipients'}).Value = $reportRecipients
}
if ($smtpServerUrl) {
    ($conf.configuration.applicationSettings.'AzureWorker.Properties.Settings'.setting | where {$_.name -eq 'SmtpServerUrl'}).Value = $smtpServerUrl
}
if ($sendEmailCredentialsSecretId) {
    ($conf.configuration.applicationSettings.'AzureWorker.Properties.Settings'.setting | where {$_.name -eq 'SendEmailCredentialsSecretId'}).Value = $sendEmailCredentialsSecretId
}
if ($linkPage) {
    ($conf.configuration.applicationSettings.'AzureWorker.Properties.Settings'.setting | where {$_.name -eq 'LinkPage'}).Value = $linkPage
}
$conf.Save($confPath)

Write-Host "Retrieving configuration blob container..."
$container = Get-AzureStorageContainer -Name "config" -Context $storage.Context -ErrorAction SilentlyContinue
if (-not $container) {
    Write-Host "Container does not exist, creating new one..."
    $container = New-AzureStorageContainer -Name "config" -Permission Off -Context $storage.Context
}

Write-Host "Uploading AzureWorker..."
foreach($file in Get-ChildItem (Join-Path $cdir "\AzureWorker") -File)
{
    $null = Set-AzureStorageBlobContent -Blob $file.Name -CloudBlobContainer $container.CloudBlobContainer -File $file.FullName -Context $storage.Context -Force
}

Write-Host "Deleting Temporary Files"
Remove-Item (Join-Path $cdir "\AzureWorker") -Recurse