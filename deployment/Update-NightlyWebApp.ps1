<#
 .SYNOPSIS
    Builds and updates Nightly web app in an existing deployment.

 .DESCRIPTION
    Builds and updates Nightly web app in an existing deployment. Web.config file is not updated so, that configuration is preserved.

 .PARAMETER name
    Name of the appName.

 .PARAMETER storageName
    Name of the web app in the deployment (if differs from default one).
#>
param(
 [Parameter(Mandatory=$True)]
 [string]
 $name,

 [string]
 $appName
)

$ErrorActionPreference = "Stop"

if (-not $appName) {
    $appName = $name.ToLowerInvariant()
}

$cpath = Get-Location
$cdir = $cpath.Path

if ([Environment]::Is64BitOperatingSystem) {
    $pfiles = ${env:PROGRAMFILES(X86)}
} else {
    $pfiles = $env:PROGRAMFILES
}
$publishModule = $pfiles + '\Microsoft Visual Studio 14.0\Common7\IDE\Extensions\Microsoft\Web Tools\Publish\Scripts\1.2.0\publish-module.psm1'
if (!(Test-Path $publishModule)) {
    Write-Error -Message 'ERROR: Failed to locate powershell publish-module for ASP.NET at ' + $publishModule
    exit 1
}
Import-Module $publishModule

#Create or check for existing
Write-Host "Retrieving WebApp..."
$app = Get-AzureRmWebApp -Name $appName -ResourceGroupName $name -ErrorAction SilentlyContinue
if(!$app)
{
    Write-Error "WebApp not found, update is impossible. Please, perform a complete deployment."
    exit 1
}
$newSettings = @{}
ForEach ($kvp in $app.SiteConfig.AppSettings) {
    $newSettings[$kvp.Name] = $kvp.Value
}

Write-Host "Retrieving publishing credentials..."
$siteConf = Invoke-AzureRmResourceAction -ResourceGroupName $name -ResourceType Microsoft.Web/sites/config -ResourceName $appName/publishingcredentials -Action list -ApiVersion 2015-08-01 -Force

Write-Host "Building NightlyWebApp..."
$null = .\Build-NightlyWebApp.ps1

Remove-Item (Join-Path $cdir "\NightlyWebApp\Web.config")


Write-Host "Publishing NightlyWebApp..."
$publishProperties = @{'WebPublishMethod' = 'MSDeploy';
                        'MSDeployServiceUrl' = "$appName.scm.azurewebsites.net:443";
                        'DeployIisAppPath' = $appName;
                        'Username' = $siteConf.properties.publishingUserName 
                        'Password' = $siteConf.properties.publishingPassword}

$null = Publish-AspNet -packOutput (Join-Path $cdir "NightlyWebApp") -publishProperties $publishProperties

Write-Host "Deleting Temporary Files..."
Remove-Item (Join-Path $cdir "\NightlyWebApp") -Recurse

$app