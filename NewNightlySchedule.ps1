Login-AzureRmAccount
# select right subscription
# $subscription = Get-AzureRmSubscription -SubscriptionId ... # ?
# Set-AzureRmContext -SubscriptionId ...


$NightlyApp = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSApplicationPackageReference"
$NightlyApp.ApplicationId = "NightlyRunner" # <-- check application id
# $NightlyApp.Version = "..."  # <-- uncomment to select specific application version
[Microsoft.Azure.Commands.Batch.Models.PSApplicationPackageReference[]] $AppRefs = @($NightlyApp)

$ManagerTask = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSJobManagerTask"
$ManagerTask.ApplicationPackageReferences = $AppRefs
$ManagerTask.Id = "NightlyRunTask"
# Following line depends on application id and version, check documentation for details.
$ManagerTask.CommandLine = "cmd /c %AZ_BATCH_APP_PACKAGE_NIGHTLYRUNNER%\NightlyRunner.exe"

$JobSpecification = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSJobSpecification"
$JobSpecification.JobManagerTask = $ManagerTask
$JobSpecification.PoolInformation = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSPoolInformation"
$JobSpecification.PoolInformation.PoolId = "z3-nightly" # ...pool id... # <-- enter pool id here

$Schedule = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSSchedule"
$Schedule.RecurrenceInterval = [TimeSpan]::FromDays(1)

$BatchContext = Get-AzureRmBatchAccountKeys -AccountName cz3
New-AzureBatchJobSchedule -Id "NightlyRunSchedule" -Schedule $Schedule -JobSpecification $JobSpecification -BatchContext $BatchContext
