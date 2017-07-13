<#
 .SYNOPSIS
    Builds and updates Azure worker in an existing deployment.

 .DESCRIPTION
    Builds and updates Azure worker in an existing deployment. AzureWorker.exe.config file is not updated so, that configuration is preserved.

 .PARAMETER name
    Name of the deployment.

 .PARAMETER storageName
    Name of the storage account in the deployment (if differs from default one).
#>
param(
 [Parameter(Mandatory=$True)]
 [string]
 $name,

 [string]
 $storageName
)

$ErrorActionPreference = "Stop"

if (-not $storageName) {
    $storageName = $name.ToLowerInvariant()
}

$cpath = Get-Location
$cdir = $cpath.Path

$storage = Get-AzureRmStorageAccount -Name $storageName -ResourceGroupName $name -ErrorAction SilentlyContinue
if(!$storage)
{
    Write-Error "Storage not found, update is impossible. Please, perform a complete deployment."
    exit 1
}

Write-Host "Building AzureWorker..."
$null = .\Build-AzureWorker.ps1
$null = mkdir "AzureWorker" -Force
Copy-Item ..\src\AzurePerformanceTest\AzureWorker\bin\Release\*.exe .\AzureWorker
Copy-Item ..\src\AzurePerformanceTest\AzureWorker\bin\Release\*.dll .\AzureWorker

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