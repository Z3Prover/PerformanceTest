<#
 .SYNOPSIS
    Creates a new self-signed certificate, which can be used in z3 performance testing environment.

 .DESCRIPTION


 .PARAMETER name
    Name of the certificate.

 .PARAMETER password
    Password for the private key of the certificate.

 .PARAMETER startDate
    Start date of the validity of the certificate. Defaults to time of the creation of the certificate.

 .PARAMETER startDate
    End date of the validity of the certificate. Defaults to startDate + 1 year.


 .OUTPUTS
    X509Certificate2 certificate object.
#>
param(
 [Parameter(Mandatory=$True)]
 [string]
 $name,

 [string]
 $password,

 [DateTime]
 $startDate,

 [DateTime]
 $endDate
)

$ErrorActionPreference = "Stop"

if (-not $startDate) {
    $startDate = Get-Date
}
if (-not $endDate) {
    $endDate = $startDate.AddYears(1)
}
$cert = New-SelfSignedCertificate -Subject ("CN=" + $name) -CertStoreLocation Cert:\CurrentUser\My -Provider "Microsoft Enhanced RSA and AES Cryptographic Provider" -NotBefore $startDate -NotAfter $endDate -Type Custom -KeyExportPolicy ExportableEncrypted

if ($password) {
    $pwd = ConvertTo-SecureString -String $password -Force -AsPlainText
    $null = Export-PfxCertificate -cert $cert -FilePath (".\" + $name + ".pfx") -Password $pwd
}
#Remove-Item -Path ("Cert:\CurrentUser\My\" + $cert.Thumbprint) -DeleteKey
$cert