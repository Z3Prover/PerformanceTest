<#
 .SYNOPSIS
    Retrieves azure resource group with given name. If it doesn't exist, creates a new one.

 .DESCRIPTION


 .PARAMETER resourceGroupName
    Name of the resource group.

 .PARAMETER resourceGroupLocation
    Location, where new resource group will be created, if needed.


 .OUTPUTS
    Resource group object.
#>
param(
 [Parameter(Mandatory=$True)]
 [string]
 $resourceGroupName,

 [string]
 $resourceGroupLocation
)

$ErrorActionPreference = "Stop"

#Create or check for existing resource group
$resourceGroup = Get-AzureRmResourceGroup -Name $resourceGroupName -ErrorAction SilentlyContinue
if(!$resourceGroup)
{
    if(!$resourceGroupLocation) {
        Write-Host "Resource group '$resourceGroupName' does not exist. To create a new resource group, please enter a location.";
        $resourceGroupLocation = Read-Host "resourceGroupLocation";
    }
    $resourceGroup = New-AzureRmResourceGroup -Name $resourceGroupName -Location $resourceGroupLocation
}

$resourceGroup