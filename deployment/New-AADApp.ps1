<#
 .SYNOPSIS
    Creates a new Azure Active Directory application with certificate credentials.

 .DESCRIPTION


 .PARAMETER name
    Name of the application.

 .PARAMETER cert
    X509Certificate2 certificate.


 .OUTPUTS
    Service principal of the application.
#>
param(
 [Parameter(Mandatory=$True)]
 [string]
 $name,
 
 [Parameter(Mandatory=$True)]
 [System.Security.Cryptography.X509Certificates.X509Certificate2]
 $cert
)

$ErrorActionPreference = "Stop"

$credValue = [System.Convert]::ToBase64String($cert.GetRawCertData())

$adapp = New-AzureRmADApplication -DisplayName $name -HomePage ("https://" + $name) -IdentifierUris ("https://" + $name) -CertValue $credValue -StartDate $cert.NotBefore -EndDate $cert.NotAfter

$sp = New-AzureRmADServicePrincipal -ApplicationId $adapp.ApplicationId

$sp