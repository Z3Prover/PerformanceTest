<#
 .SYNOPSIS
    Retrieves azure storage with given name. If it doesn't exist, creates a new one.

 .DESCRIPTION


 .PARAMETER storageName
    Name of the storage.

 .PARAMETER resourceGroup
    Resourse group object.


 .OUTPUTS
    Storage object.
#>
param(
 [Parameter(Mandatory=$True)]
 [string]
 $storageName,

 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.ResourceManager.Cmdlets.SdkModels.PSResourceGroup]
 $resourceGroup
)

$ErrorActionPreference = "Stop"

#Create or check for existing
$storage = Get-AzureRmStorageAccount -Name $storageName -ResourceGroupName $resourceGroup.ResourceGroupName -ErrorAction SilentlyContinue
if(!$storage)
{
    $storage = New-AzureRmStorageAccount -Name $storageName -ResourceGroupName $resourceGroup.ResourceGroupName -Location $resourceGroup.Location -SkuName Standard_LRS -Kind Storage
}

$storage