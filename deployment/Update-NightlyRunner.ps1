<#
 .SYNOPSIS
    Builds and updates Nightly runner in an existing deployment.

 .DESCRIPTION
    Builds and updates Nightly runner in an existing deployment. NightlyRunner.exe.config file is not updated so, that configuration is preserved.

 .PARAMETER name
    Name of the deployment.

 .PARAMETER batchName
    Name of the batch account in the deployment (if differs from default one).
#>
param(
 [Parameter(Mandatory=$True)]
 [string]
 $name,

 [string]
 $batchName
)

$ErrorActionPreference = "Stop"

if (-not $batchName) {
    $batchName = $name.ToLowerInvariant()
}

$cpath = Get-Location
$cdir = $cpath.Path

$batchAccount = Get-AzureRmBatchAccountKeys -AccountName $batchName -ResourceGroupName $name -ErrorAction SilentlyContinue
if(!$batchAccount)
{
    Write-Error "Batch account not found, update is impossible. Please, perform a complete deployment."
    exit 1
}

Write-Host "Retrieving the last version of the application package..."
$application = Get-AzureRmBatchApplication -AccountName $batchAccount.AccountName -ResourceGroupName $name -ApplicationId "NightlyRunner"
$package = Get-AzureRmBatchApplicationPackage -AccountName $batchAccount.AccountName -ResourceGroupName $name -ApplicationId "NightlyRunner" -ApplicationVersion $application.DefaultVersion
$null = Invoke-WebRequest -Uri $package.StorageUrl -OutFile "oldNightlyRunner.zip"
$null = Expand-Archive "oldNightlyRunner.zip" -DestinationPath "oldNightlyRunner" -Force

Write-Host "Building NightlyRunner..."
$null = .\Build-NightlyRunner.ps1
$null = mkdir "NightlyRunner" -Force
Copy-Item ..\src\NightlyRunner\bin\Release\*.exe .\NightlyRunner
Copy-Item ..\src\NightlyRunner\bin\Release\*.dll .\NightlyRunner
Copy-Item .\oldNightlyRunner\*.config .\NightlyRunner


Write-Host "Zipping NightlyRunner..."
$zip = Compress-Archive -Path ".\NightlyRunner\*" -DestinationPath ".\NightlyRunner.zip" -Force

Write-Host "Creating Application Package..."
$now = Get-Date
$version = $now.Year.ToString() + "-" + $now.Month.ToString() + "-" + $now.Day.ToString()
$zipPath = (Join-Path $cdir "\NightlyRunner.zip")
$null = New-AzureRmBatchApplicationPackage -AccountName $batchAccount.AccountName -ResourceGroupName $name -ApplicationId "NightlyRunner" -ApplicationVersion $version -FilePath $zipPath -Format "zip"
$null = Set-AzureRmBatchApplication -AccountName $batchAccount.AccountName -ResourceGroupName $name -ApplicationId "NightlyRunner" -DefaultVersion $version

Write-Host "Deleting Temporary Files"
Remove-Item (Join-Path $cdir "\NightlyRunner") -Recurse
Remove-Item (Join-Path $cdir "\oldNightlyRunner") -Recurse
Remove-Item (Join-Path $cdir "\oldNightlyRunner.zip")
Remove-Item $zipPath