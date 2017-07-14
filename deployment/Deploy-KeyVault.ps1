<#
 .SYNOPSIS
    Retrieves azure key vault with given name. If it doesn't exist, creates a new one. Puts there a connection string to z3 performance testing environment and gives an AAD app permission to access it.

 .DESCRIPTION


 .PARAMETER keyVaultName
    Name of the key vault.

 .PARAMETER resourceGroup
    Resourse group object.

 .PARAMETER connectionStringSecretName
    Name of the secret in which connection string to the environment (keys to storage and batch) is kept.

 .PARAMETER storage
    Storage account object to which connection string in the vault should point.

 .PARAMETER batchAccount
    Batch account object to which connection string in the vault should point.

 .PARAMETER AADAppServicePrincipal
    AAD app service principal, that should have access to secrets in the vault.

 .PARAMETER sendEmailCredentialsSecretId
    Name of a secret in the Key Vault which keeps user name and password divided by :. E.g. user@foo.com:password. This email is used to send reports of nightly tests.

 .PARAMETER sendEmailCredentials
    User name and password divided by :. E.g. user@foo.com:password. This email is used to send reports of nightly tests. If the property value is an empty string, no e-mail is sent.


 .OUTPUTS
    Key vault object.
#>
param(
 [Parameter(Mandatory=$True)]
 [string]
 $keyVaultName,

 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.ResourceManager.Cmdlets.SdkModels.PSResourceGroup]
 $resourceGroup,
 
 [Parameter(Mandatory=$True)]
 [string]
 $connectionStringSecretName,
 
 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.Management.Storage.Models.PSStorageAccount]
 $storage,
 
 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.Batch.BatchAccountContext]
 $batchAccount,

 [Microsoft.Azure.Commands.Resources.Models.ActiveDirectory.PSADServicePrincipal]
 $AADAppServicePrincipal,

 [string]
 $sendEmailCredentialsSecretId,

 [string]
 $sendEmailCredentials
)

$ErrorActionPreference = "Stop"

#Create or check for existing
$keyVault = Get-AzureRmKeyVault -VaultName $keyVaultName -ResourceGroupName $resourceGroup.ResourceGroupName -ErrorAction SilentlyContinue
if(!$keyVault)
{
    $keyVault = New-AzureRmKeyVault -VaultName $keyVaultName -ResourceGroupName $resourceGroup.ResourceGroupName -Location $resourceGroup.Location
}

$storageKeys = Get-AzureRmStorageAccountKey -Name $storage.StorageAccountName -ResourceGroupName $resourceGroup.ResourceGroupName
$stName = $storage.StorageAccountName
$stKey = $storageKeys[0].Value
$batchAccount = Get-AzureRmBatchAccountKeys -AccountName $batchAccount.AccountName -ResourceGroupName $resourceGroup.ResourceGroupName
$batchName = $batchAccount.AccountName
$batchKey = $batchAccount.PrimaryAccountKey
$batchAddr = $batchAccount.AccountEndpoint

$connectionString = "DefaultEndpointsProtocol=https;AccountName=$stName;AccountKey=$stKey;BatchAccount=$batchName;BatchURL=https://$batchAddr;BatchAccessKey=$batchKey;"
$secureConnString = ConvertTo-SecureString -String $connectionString -Force -AsPlainText
$null = Set-AzureKeyVaultSecret -Name $connectionStringSecretName -SecretValue $secureConnString -VaultName $keyVaultName

if ($AADAppServicePrincipal) {
    Set-AzureRmKeyVaultAccessPolicy -VaultName $keyVaultName -ObjectId $AADAppServicePrincipal.Id -PermissionsToSecrets all -ResourceGroupName $resourceGroup.ResourceGroupName
}

if ($sendEmailCredentialsSecretId -and $sendEmailCredentials) {
    $secureCredentials = ConvertTo-SecureString -String $sendEmailCredentials -Force -AsPlainText
    $null = Set-AzureKeyVaultSecret -Name $sendEmailCredentialsSecretId -SecretValue $secureCredentials -VaultName $keyVaultName
}

$keyVault