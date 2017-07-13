<#
 .SYNOPSIS
    Deploys reference experiment.

 .DESCRIPTION


 .PARAMETER storage
    Azure storage object. Can be obtained via Get-AzureRmStorageAccount cmdlet.

 .PARAMETER jsonPath
    Path to json describing reference experiment. If not provided, reference experiment will not be deployed.

 .PARAMETER executablePath
    Path to executable (or zip) which should be used in reference experiment. If not provided, reference experiment will not be deployed.

 .PARAMETER inputPath
    Path to input file for the reference experiment. If not provided, reference experiment will still be deployed, but required input files should be uploaded separately.


 .OUTPUTS
#>
param(
 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.Management.Storage.Models.PSStorageAccount]
 $storage,
 
 [Parameter(Mandatory=$True)]
 [string]
 $jsonPath,
 
 [Parameter(Mandatory=$True)]
 [string]
 $executablePath,
 
 [string]
 $inputPath
)

$ErrorActionPreference = "Stop"

$cpath = Get-Location
$cdir = $cpath.Path

Write-Host "Retrieving configuration blob container..."
$confContainer = Get-AzureStorageContainer -Name "config" -Context $storage.Context -ErrorAction SilentlyContinue
if (-not $confContainer) {
    Write-Host "Container does not exist, creating new one..."
    $confContainer = New-AzureStorageContainer -Name "config" -Permission Off -Context $storage.Context
}

Write-Host "Retrieving bin blob container..."
$binContainer = Get-AzureStorageContainer -Name "bin" -Context $storage.Context -ErrorAction SilentlyContinue
if (-not $binContainer) {
    Write-Host "Container does not exist, creating new one..."
    $binContainer = New-AzureStorageContainer -Name "bin" -Permission Off -Context $storage.Context
}

$reference = Get-Content $jsonPath | ConvertFrom-Json

Write-Host "Uploading reference.json..."
$null = Set-AzureStorageBlobContent -Blob "reference.json" -CloudBlobContainer $confContainer.CloudBlobContainer -File $jsonPath -Context $storage.Context -Force

Write-Host "Uploading reference executable..."
$null = Set-AzureStorageBlobContent -Blob $reference.Definition.Executable -CloudBlobContainer $binContainer.CloudBlobContainer -File $executablePath -Context $storage.Context -Force

if ($inputPath) {
    if ($reference.Definition.BenchmarkContainerUri -ne "default") {
        Write-Error "Can not upload files non-default container."
        exit 1
    }
    $inputFile = Get-Item $inputPath
    $blobName = ($reference.Definition.BenchmarkDirectory + "/" + $reference.Definition.Category + "/" + $inputFile.Name).Replace("//", "/").Replace("//", "/")

    Write-Host "Retrieving input blob container..."
    $inputContainer = Get-AzureStorageContainer -Name "input" -Context $storage.Context -ErrorAction SilentlyContinue
    if (-not $inputContainer) {
        Write-Host "Container does not exist, creating new one..."
        $inputContainer = New-AzureStorageContainer -Name "input" -Permission Off -Context $storage.Context
    }
    Write-Host "Uploading reference input..."
    $null = Set-AzureStorageBlobContent -Blob $blobName -CloudBlobContainer $inputContainer.CloudBlobContainer -File $inputPath -Context $storage.Context -Force
}