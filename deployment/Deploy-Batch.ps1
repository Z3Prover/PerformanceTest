<#
 .SYNOPSIS
    Retrieves azure batch account with given name. If it doesn't exist, creates a new one.

 .DESCRIPTION
    Retrieves azure batch account with given name. If it doesn't exist, creates a new one. Associates a storage account with it. Adds a provided certificate to the batch account and all of its pools. If batch account has no pools, a new one is created.

 .PARAMETER batchName
    Name of the batch account.

 .PARAMETER resourceGroup
    Resourse group object.

 .PARAMETER storage
    Storage to associate with batch account.

 .PARAMETER cert
    Certificate to deploy in all pools of the batch account.

 .PARAMETER certPassword
    Password to the certificate's private key.

 .PARAMETER location
    Location of azure datacenter to use. Defaults to one of the resource group.


 .OUTPUTS
    Batch context.
#>
param(
 [Parameter(Mandatory=$True)]
 [string]
 $batchName,

 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.ResourceManager.Cmdlets.SdkModels.PSResourceGroup]
 $resourceGroup,

 [Microsoft.Azure.Commands.Management.Storage.Models.PSStorageAccount]
 $storage,
 
 [System.Security.Cryptography.X509Certificates.X509Certificate2]
 $cert,

 [string]
 $certPassword,

 [string]
 $location
)

$ErrorActionPreference = "Stop"

if (-not $location) {
    $location = $rg.Location
}

#Create or check for existing
$batch = Get-AzureRmBatchAccount -AccountName $batchName -ResourceGroupName $resourceGroup.ResourceGroupName -ErrorAction SilentlyContinue
if(!$batch)
{
    if ($storage) {
        $batch = New-AzureRmBatchAccount -AccountName $batchName -ResourceGroupName $resourceGroup.ResourceGroupName -Location $location -AutoStorageAccountId $storage.Id
    } else {
        $batch = New-AzureRmBatchAccount -AccountName $batchName -ResourceGroupName $resourceGroup.ResourceGroupName -Location $location
    }
}

$batch = Get-AzureRmBatchAccountKeys -AccountName $batch.AccountName -ResourceGroupName $resourceGroup.ResourceGroupName

$pools = Get-AzureBatchPool -BatchContext $batch
$conf = [Microsoft.Azure.Commands.Batch.Models.PSCloudServiceConfiguration]::new("3", "*")

if ($cert) {
    $allcerts = Get-AzureBatchCertificate -BatchContext $batch
    $alreadyThere = $false
    if ($allcerts) {
        for($i = 0; $i -lt $allcerts.Length; ++$i) {
            if ($allcerts[$i].Thumbprint -eq $cert.Thumbprint) {
                $certref = [Microsoft.Azure.Commands.Batch.Models.PSCertificateReference]::new($allcerts[$i])
                $alreadyThere = $true
            }
        }
    }
    if (-not $alreadyThere) {
        if ($certPassword) {
            New-AzureBatchCertificate -BatchContext $batch -RawData ($cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $certPassword)) -Password $certPassword
        } else {
            New-AzureBatchCertificate -BatchContext $batch -RawData $cert.GetRawCertData()
        }
        $batchCert = Get-AzureBatchCertificate -BatchContext $batch -Thumbprint $cert.Thumbprint -ThumbprintAlgorithm sha1
        $certref = [Microsoft.Azure.Commands.Batch.Models.PSCertificateReference]::new($batchCert)
    }
}

if (-not $pools) {
    New-AzureBatchPool -BatchContext $batch -Id pool -VirtualMachineSize standard_d2_v2 -CloudServiceConfiguration $conf -AutoScaleEvaluationInterval ([System.TimeSpan]::FromMinutes(5)) -AutoScaleFormula ('$TargetDedicated = (max($PendingTasks.GetSample(1)) > 0.0) ? ' + ($batch.CoreQuota / 2).ToString() + ' : 0') # -CertificateReferences $certref
    $pools = Get-AzureBatchPool -BatchContext $batch
}

if ($cert) {
    for ($i = 0; $i -lt $pools.Length; ++$i) {
        $needCert = $true
        for ($j = 0; $j -lt $pools[$i].CertificateReferences.Count; ++$j) {
            if ($pools[$i].CertificateReferences[$j].Thumbprint -eq $cert.Thumbprint) {
                $needCert = false
                break
            }
        }
        if ($needCert) {
            if(-not $pools[$i].CertificateReferences) {
                $pools[$i].CertificateReferences = New-Object 'System.Collections.Generic.List[Microsoft.Azure.Commands.Batch.Models.PSCertificateReference]'
            }
            $pools[$i].CertificateReferences.Add($certref)
            Set-AzureBatchPool -Pool $pools[$i] -BatchContext $batch
        }
    }
}

$batch