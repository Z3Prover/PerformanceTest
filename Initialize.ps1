#----------------------------------------------------------------
#  Settings
#----------------------------------------------------------------

$SubscriptionName = "YourSubscriptionName"
$StorageAccountName = "yourstorageaccountname"
$Location = "West Europe"

$AzureWorkerPath = ""

# Reference experiment
$ReferenceExecutablePath = "PathToZippedExecutablePackage.zip"
$ReferenceDomainName = "Z3"
$ReferenceParameters = "model_validate=true -smt2 -file:{0}"
$ReferenceBenchmarkDirectory = "reference"
$ReferenceCategory = null
$ReferenceBenchmarkFileExtension = "smt2|smt"
$ReferenceMemoryLimitMB = 2048.0
$ReferenceBenchmarkTimeout = "00:20:00"
$ReferenceExperimentTimeout = "00:00:00"
$ReferenceRepetitions = 20
# Average runtime of the reference experiment on standard d2 v2 machine (seconds)
$ReferenceValue = 16.34375


#----------------------------------------------------------------
#  Script
#----------------------------------------------------------------

Add-AzureAccount
Select-AzureSubscription -SubscriptionName $SubscriptionName –Default

# Create a new storage account.
New-AzureStorageAccount –StorageAccountName $StorageAccountName -Location $Location
# Set a default storage account.
Set-AzureSubscription -CurrentStorageAccountName $StorageAccountName -SubscriptionName $SubscriptionName

#$context = New-AzureStorageContext -StorageAccountName $StorageAccountName -StorageAccountKey $key


#--------------------------------------------------------------------------------------
# Create storage account containers
Write-Host "Creating containers..."
New-AzureStorageContainer -Name "bin" -Permission Off
New-AzureStorageContainer -Name "config" -Permission Off
New-AzureStorageContainer -Name "input" -Permission Off
New-AzureStorageContainer -Name "output" -Permission Off
New-AzureStorageContainer -Name "results" -Permission Off

#---------------------------------------------------------------------------------------
# Initialize configuration
# Configuration contains binaries required to run benchmarks on Azure Batch nodes and 
# definition of a reference experiment.

Write-Host "Uploading reference executable from $ReferenceExecutablePath..."
$ReferenceExperimentBlobName = "reference.zip"
Set-AzureStorageBlobContent -Blob $ReferenceExperimentBlobName -Container "bin" -File $ReferenceExecutablePath

# Initialize reference experiment
$ReferenceJson = 
@{
  Definition = {
    Executable = $ReferenceExperimentBlobName;
    DomainName = $ReferenceDomainName;
    Parameters = $ReferenceParameters;
    BenchmarkContainerUri = 'default';
    BenchmarkDirectory = $ReferenceBenchmarkDirectory;
    Category = $ReferenceCategory;
    BenchmarkFileExtension = $ReferenceBenchmarkFileExtension;
    MemoryLimitMB = $ReferenceMemoryLimitMB;
    BenchmarkTimeout = $ReferenceBenchmarkTimeout;
    ExperimentTimeout = $ReferenceExperimentTimeout;
  };
  Repetitions = $ReferenceRepetitions;
  ReferenceValue = $ReferenceValue
} | ConvertTo-Json

$ReferenceJson >> "reference.json"
Set-AzureStorageBlobContent -Blob 'reference.json' -Container 'config' -File 'reference.json'

Write-Host "Uploading AzureWorker..."
foreach($file in Get-ChildItem $AzureWorkerPath -Include @("*.dll", "*.exe". "*.config") -File)
{
    Set-AzureStorageBlobContent -Blob $file.Name -Container 'config' -File $file.FullName 
}

# Initialize tables