<#
 .SYNOPSIS
    Downloads nuget.exe to current folder if not already present.
#>
if (-not (Test-Path 'nuget.exe')) {
    $null = Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile 'nuget.exe'
}