
This repository holds test infrastructure and benchmarks used to test Z3. 

# Contents

- [Glossary](#glossary)
- [Structure of repository](#structure-of-repository)
- [Build and test](#build-and-test)
  - [Build requirements](#build-requirements)
  - [How to build](#how-to-build)
- [Architecture](#architecture)
  - [Storage](#storage)
    - [Configuration](#configuration)
    - [Table of experiments](#table-of-experiments)
    - [Experiment results](#experiment-results)
    - [Outputs](#outputs)
    - [Binaries](#binaries)
    - [Summaries](#summaries)
    - [Secrets](#secrets)
    - [Certificates](#certificates)
  - [Server-side components](#server-side-components)
    - [Running performance tests](#running-performance-tests)
    - [Requeueing benchmarks](#requeueing-benchmarks)
    - [Nightly Z3 performance tests](#nightly-z3-performance-tests)
        - [How to schedule nightly runs using Azure Batch Schedule](#how-to-schedule-nightly-runs-using-azure-batch-schedule)
        - [How to enable reports sent by e-mail](#how-to-enable-reports-sent-by-e-mail)
  - [Client applications](#client-applications)
    - [PerformanceTest.Management](#performancetestmanagement)
    - [Z3 Nightly Web Application](#z3-nightly-web-application)
        - [Experiment tags](#experiment-tags)
    - [Timeline and records builder](#timeline-and-records-builder)
    - [Import experiment from an obsolete data formats](#import-experiment-from-an-obsolete-data-formats)
- [Run and deploy](#run-and-deploy)

# Glossary

*Performance test* is measurement of execution of a target command line executable file for specific input file
with certain command line parameters.
For example, a performance test for Z3 is execution of `z3.exe` for the given parameters and certain smt2 file.

An input file is called *benchmark*.

An *experiment* is a set of performance tests for a single target executable, same command line parameters and run for multiple benchmarks located in the predefined directory.

Regular experiments allow to track how changes in the source codes affect the target executable performance on same set of benchmarks. 

A *domain* determines specific settings for a certain target executable, such as input file extensions, command line syntax, how to interpret input and output of program run,
how to analyse and aggregate multiple runs.


# Structure of repository

* `/PerformanceTests.sln` is a Visual Studio 2015 solution which contains following projects:
  * `PerformanceTests.Management` is a WPF application to view, manage and submit performance 
  experiments.
  * `NightlyWebApp` is ASP.NET web application that shows history of performance tests runs.
  * `NightlyRunner` is a command line application that submits performance tests when new Z3 nightly build is available on the github.
  See details [here](#nightly-z3-performance-tests).
  * `Summary` is a command line application that computes summary and records for specified experiment and then updates corresponding data in the Azure Storage. See details [here](#timeline-and-records-builder).
  * `AzurePerformanceTest` is a .NET class library holding the `AzureExperimentManager` class which exposes API to manage experiments based on Microsoft Azure.
  * `PerformanceTest` is a .NET class library containing abstract types for experiments management.
  * `Measurement` is a .NET class library allowing to measure process run time and memory usage.
  * `Z3Domain` is a .NET class library implementing Z3-specific analysis of the program execution.
  * `AzureWorker` is a command line application that runs on Azure Batch nodes to prepare and execute performance tests.
  * `AzurePerformanceTestCommons` is a .NET class library that contains functionality for Azure-based experiment management and is shared by `AzurePerformanceTest` and `AzureWorker`.

* `/ImportData.sln` is a Visual Studio 2015 solution which allows to import experiments results from old format to Azure storage.

* `/src/` contains Visual Studio projects included to the Visual Studio solutions.

* `/tests/` contain Visual Studio unit test projects included to the Visual Studio solutions.

* `/deployment/` contains deployment scripts.

# Build and test 

## Build requirements

* **Visual Studio 2015**

If you don't have Visual Studio 2015, you can install the free [Visual Studio 2015 Community](http://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx).


## How to build

Use Visual Studio or msbuild to build solutions `PerformanceTests.sln` and `ImportData.sln`.

Also there are helpful PowerShell scripts that build and deploy certain projects; 
see [Deployment scripts](#deployment-scripts).

# Architecture

The performance test infrastructure has Microsoft Azure-based client-server architecture which consists of following components:

1. [Storage](#storage) is based on Azure Storage Account and Key Vault and keeps following data:
    * Configuration and system files.
    * Table of completed and running experiments.
    * Results for each of the experiments.
    * Summaries and timelines for experiments.
    * Binaries that are tested.
    * Benchmark files.
    * Secrets.

2. [Server-side components](#server-side-components) 
use storage to prepare and run experiments, save results and build summary. 
These components run on Azure Batch and include:
    * *AzureWorker* runs an experiment and saves results.
    * *NightlyRunner* checks if there is new Z3 nightly build available and schedules an experiment for it.

3. [Client applications](#client-applications)
allow a user to manage experiments and analyze results. Two main applications are:
    * Windows application *PerformanceTest.Management* shows a list of experiments and results for each of the experiments,
    compares two experiments and exposes set of features to manage experiments.
    * Web application *NightlyWebApp* is intended to show history of experiments for Z3 nightly builds and perform statistical analysis of results.


## Storage

Data is stored in an Azure Storage Account using one Azure table, multiple blob containers and an Azure Key Vault for secrets.

### Configuration

Configuration of the performance test infrastructure is represented as a bunch of files located in the blob container `config`.

It includes:
* Executable and supporting files that are run on Azure nodes and perform management and measurement of performance
(`AzureWorker.exe`, `AzureWorker.exe.config` and DLLs).
* .NET class libraries (as DLLs) with experiment domains (i.e. types derived from `Measurement.Domain` class). 
There should be at least `Z3Domain.dll`. The types are loaded every time an experiment is running 
using [MEF](https://docs.microsoft.com/en-us/dotnet/framework/mef/index)
and therefore each domain type declaration must have an attribute `Export` with `Measurement.Domain` as a contract type.
* Definition of the reference experiment as `reference.json` file. 
Deployment of the test infrastructure requires a reference experiment to be provided. Reference experiment consists of a set of benchmarks located in the storage. 
They are measured on each of the machines before performance tests are started to determine performance normalization coefficient.

### Table of experiments

A table of experiments is stored as an Azure Table called `experiments`. Its structure is:

- `PartitionKey` must be `default` for all experiments.
- `RowKey` is an integer ID of an experiment. It is unique among experiments of the table.
- `BenchmarkContainerUri` is either a string `"default"` or a Shared Access Signature URL. In 
the former case, the `input` container of the table's Storage Account is a benchmark
container for this experiment. Otherwise, the URL points the benchmark container explicitly and it can belong to a different Storage Account. Note that SAS expires after some time.
- `BenchmarkDirectory` is a path to a directory within the benchmark container that contains benchmark files. An empty string indicates a root of the container. The folder separator must be `/`.
- `BenchmarkFileExtension` is the extension(s) of benchmark files, e.g., "smt2" for SMT-Lib version 2 files. It may contain multiple extensions concatenated through the pipe symbol, e.g. "smt|smt2".
- `BenchmarkTimeout` is a time limit in seconds per benchmark. If test runs for more than the given time span, it is stopped.
- `Category` is a folder within the `BenchmarkDirectory` to draw benchmarks from. 
If it is empty, all benchmarks of the benchmark directory are tested.
- `CompletedBenchmarks` is a number of completed benchmarks. Updated by the 
Azure Batch worker as tests complete.
- `Creator` keeps a custom name of one who have submitted the experiment.
- `DomainName` allows to identify and construct an instance of a `Domain` class that determines an additional analysis and results interpretation.
- `Executable` is a blob name in the `bin` container which contains either an executable file or a zip file with a main executable and supporting files. The executable will run for multiple specified benchmark files to measure its performance.
- `ExperimentTimeout` is a time limit in seconds per experiment. If time passed since the experiment submission exceeds this time span,  the experiment is stopped. 
Zero means no limits.
- `Flag` is either 'false' or 'true' and is switched by a user.
- `MemoryLimitMB` is the memory limit per benchmark in megabytes. Zero means no limit.
- `Note`
- `Parameters`
- `Submitted`
- `TotalBenchmarks`
- `TotalRuntime`
- `WorkerInformation`
- `AdaptiveRunMaxRepetitions`
- `AdaptiveRunMaxTimeInSeconds`

To enable automatic numbering of new experiments, in the table there is one row which contains next experiment ID to be assigned.
Its `PartitionKey` is `NextIDPartition` and `RowKey` is `NextIDRow`.

### Experiment results

Results of experiments are stored in the blob container `results`. 

For each of the experiments, there is a single blob named `{id}.csv.zip` where `{id}` is an experiment id. It is a compressed CSV table 
with rows corresponding to benchmarks. Rows are ordered by benchmark file name.

While an experiment is running, the table is extended with new rows as benchmarks complete. 
Due to infrastructure issues, there is a chance that there are more than one row per benchmark when the experiment is complete.
Duplicates can be resolved using PerformanceTest.Management application.

The application also allows to resubmit some of the benchmarks; it also leads to duplicates in this table.

Table structure is:

* `BenchmarkFileName` is path to a benchmark file that is passed as an argument to the target executable, 
relative to the benchmark directory and category specified in the experiment definition.
Path separator is `/`.
* `AcquireTime` is UTC time moment when the test started.
* `NormalizedRuntime` equals total processor time for this benchmark multiplied by performance coefficient for this machine,
which is based on the total processor time of the reference experiment.
* `TotalProcessorTime` (seconds) indicates the amount of time that the test has spent utilizing the CPU.
In case of multiple CPU cores were used, times for all cores are summed together so this value can exceed the wall clock time.
* `WallClockTime` (seconds) indicates the amount of real time elapsed between the test process started and exited.
* `PeakMemorySizeMB` (megabytes) is maximum amount of virtual memory used by the test run.
* `Status` indicates how the test completed. The status is finally determined by the experiment domain.
    * `Success` if successfully completed.
    * `OutOfMemory` if out-of-memory exception occurred, or the benchmark memory limit was exceeded,
    or the domain determined this (e.g. by exit code).
    * `Timeout` if wall clock time exceeded the benchmark time limit.
    * `Error` if the experiment domain considers the output or exit code as error.
    * `Bug` if the experiment domain considers the output or exit code as bug in the target executable.
    * `InfrastructureError` if the infrastructure had issues while running the test. 
* `ExitCode` contains process exit code, if status is neither memory out nor time out;
otherwise, it is empty.
* `StdOut` is either standard output of the test process or empty, if the output is too large and is stored in a separate blob.
* `StdOutExtStorageIdx` is empty, if `StdOut` contains the actual output; otherwise it contains a suffix that should be appended to 
the standard output blob name for the result, see [Outputs](#outputs) for more details.
* `StdErr` is either standard error of the test process or empty, if the error is too large and is stored in a separate blob.
* `StdErrExtStorageIdx` is empty, if `StdErr` contains the actual error; otherwise it contains a suffix that should be appended to 
the standard error blob name for the result, see [Outputs](#outputs) for more details.

Other columns depend on the experiment domain. The `Domain.Analyze` method applied to results of a benchmark 
can return custom properties which are included into this table. For the Z3 domain such properties are:

* `SAT`
* `UNSAT`
* `UNKNOWN`
* `TargetSAT`
* `TargetUNSAT`
* `TargetUNKNOWN`

### Outputs

As a part of an experiment, the target executable runs for each of the benchmarks.
Standard output and error produced by the process are saved either to the results table in
columns `StdOut` for standard output and `StdErr` for errors or in a separate blob,
if the text size exceeds 4KB. 

Blobs with outputs are located in the `output` blob container of the storage account.

Blob name is `E{id}F{benchmark}-stdout{suffix}` for standard output and
`E{id}F{benchmark}-stderr{suffix}` for standard error, 
where `{id}` is experiment id, `{benchmark}` is a benchmark file name (as given in the experiment
results table), `{suffix}` is value of the column `StdOutExtStorageIdx` of the experiment results table for output
and `StdErrExtStorageIdx` for errors. The suffixes allow the results table to contain duplicated results
for a benchmark each having different output/errors.


### Binaries

For each experiment, there is a blob in the blob container `bin` which contains
binaries to be tested. Same blob can be reused by multiple experiments.
The experiments table contains name of the associated blob in the column `Executable`.

Blob is either an executable file itself or a package containing an executable file and its supporting files.
Package can be of two kinds:
1. A zip file containing only one executable file which will run during an experiment and any number of other files.
2. A package following the 
[Open Packaging Conventions](https://msdn.microsoft.com/en-us/library/windows/desktop/dd742818(v=vs.85).aspx)
which can contain multiple files without restrictions on number of executable files
and has a specific relationship to distinguish the main executable file.

An executable file must have one of following extensions: `exe`, `bat`, `cmd`.

A blob with binaries can have an arbitrary name but PerformanceTest.Management application gives a name using this 
pattern: `{creator}.{file-name}.{date-time}{file-extension}`, where
`{creator}` is an escaped name of a user running the application,
`{file-name}` is name of a file or package without extension,
`{date-time}` is a moment when the executable was uploaded,
`{file-extension}` is the extension.

For example, the package `z3.zip` can be uploaded by the application as a blob with name `itis_Dmitry.z3.2017-06-16T12-12-14-1785.zip`.

Binaries for testing the Z3 nightly builds are named in accordance with the given rule where
the built zip file is used as a package and `Nightly` as creator.
For example, a nightly build result `z3-4.5.1.02161f2ff743-x86-win.zip` is uploaded as is with name
`Nightly.z3-4.5.1.02161f2ff743-x86-win.2017-06-16T08-33-02-8352.zip`.

A blob with binaries has two metadata attributes:
1. An attribute `creator` contains the original unescaped creator name.
2. An attribute `fileName` contains the original executable or package name.


### Summaries

To facilitate analysis of multiple experiments results, it is possible to build summaries and keep them in the
blob container `summary`. There are several kinds of summaries:

1. A timeline for series of experiments, which includes:
    - Summary of different experiment properties (errors, bugs, runtime etc) by experiments and benchmark categories.
    - Benchmark records: for each of the benchmarks, shows the best runtime and the experiment where it was achieved.
    - Benchmark records summarized by benchmark categories: shows total runtime and number of benchmarks for each of the categories.
2. Summary for an experiment statuses with comparison to another experiment. 
It lists following groups of benchmarks: errors, bugs, underperformers and dippers (compared to another experiment).


The timeline is represented as a zip file with 3 text files: `timeline.csv`, `records.csv` and `records_summary.csv`.
These files contain data described above in item 1.

Data for item 2 is stored in a blob with name `_statuses_{id}.csv.zip` (if has no comparison) or 
`_statuses_{id}_{refId}.csv.zip` (if experiment id is compared to refId). 
Such a blob is created on-demand when `AzureSummaryManager.GetStatusSummary()` method is invoked.


The Nightly web application uses the summaries to analyze experiments results.

[Timeline and records builder](#timeline-and-records-builder) allows to update summaries using the experiment results.


### Secrets

Azure Key vault allows to safely keep and get credentials for both the storage and batch account.
It is needed for AzureWorker which runs on Batch nodes and for client applications,
such as Nightly web application, NightlyRunner and Summary.

Such application require following settings to be provided in their configuration files:
* `AADApplicationId` is an identified of Azure Active Directory application, which must be created for a deployment of the 
performance test infrastructure.
* `AADApplicationCertThumbprint` is a thumbprint of the application certificate. 
Note that the application certificate must be installed on the machine where the client application runs.
* `KeyVaultUrl` is URL of the Azure Key vault.
* `ConnectionStringSecretId` is name of the secret which contains the connection string for a client application.
The connection string must contain standard storage account connection strings and additionally 
following properties, required to get an access to the Batch Account:
    - `BatchAccount`
    - `BatchURL`
    - `BatchAccessKey`

An example of the connection string:

```
DefaultEndpointsProtocol=https; AccountName=<<storageAccountName>>; AccountKey=<<storageAccountKey>>; BatchAccessKey=<<batchAccessKey>>; BatchURL=https://???.batch.azure.com; BatchAccount=<<batchAccountName>>;
```

### Certificates

In order to retrieve secrets from azure key vault, applications need to authenticate within azure active directory as service entities. For that Azure Active Directory (AAD) application ID and a corresponding certificate are required. Application ID is provided in configuration file (see above). Configuration file also contains a field for the thumbprint, using which certificate can be found, but the certificate itself needs to be installed on the machine separately.

To install certificates on machines in Azure batch, first add it (you'll need a pfx file) to the batch account on the account's "Certificates" blade, then add it on every batch pool's "Certificates" blade. That will ensure, that certificate will be installed on every machine added to these pools.

To install certificate for Azure Web App, upload the certificate on Web App's "SSL Certificates" blade, but don't create any SSL bindings. After that, go to the "Application Settings" blade and add a setting (if not there already) with a key "WEBSITE_LOAD_CERTIFICATES" and value "*" (without quotation marks). This will ensure, that certificates will be present on the server running web app despite not being required for SSL.

NB: in order to be able to use certificates, web app must be running on at least "Basic" service plan. If "Free" service plan is used, web app won't be able to access certificate and, therefore, key vault.

Certificates are valid only for limited periods of time (usually, a year), so from time to time they have to be updated.

You can create a self-signed certificate (for our purposes, self-signed is OK) with powershell:
```powershell
$startDate = Get-Date
$endDate = $startDate.AddYears(1)
$cert = New-SelfSignedCertificate -Subject "CN=<Name>" -CertStoreLocation Cert:\CurrentUser\My -Provider "Microsoft Enhanced RSA and AES Cryptographic Provider" -NotBefore $startDate -NotAfter $endDate -Type Custom -KeyExportPolicy ExportableEncrypted
```
After that you can export it to pfx file:
```powershell
$pwd = ConvertTo-SecureString -String '<Password>' -Force -AsPlainText
Export-PfxCertificate -cert $cert -FilePath "<Path>.pfx" -Password $pwd
```
To import a certificate from a pfx file:
```powershell
$pwd = ConvertTo-SecureString -String '<Password>' -Force -AsPlainText
$cert = Import-PfxCertificate -FilePath $certPfxPath -CertStoreLocation Cert:\CurrentUser\My -Password $pwd -Exportable
```
To create an AAD application authentifiable by that certificate
```powershell
$credValue = [System.Convert]::ToBase64String($cert.GetRawCertData())
$adapp = New-AzureRmADApplication -DisplayName $name -HomePage "https://<Name>" -IdentifierUris "https://<Name>" -CertValue $credValue -StartDate $cert.NotBefore -EndDate $cert.NotAfter
$sp = New-AzureRmADServicePrincipal -ApplicationId $adapp.ApplicationId
```
To add a certificate as credentials to an existing AAD application (e.g. when previous certificate is near expiration)
```powershell
$credValue = [System.Convert]::ToBase64String($cert.GetRawCertData())
$cred = New-AzureRmADAppCredential -ApplicationId "<Application ID>" -CertValue $credValue -StartDate $cert.NotBefore -EndDate $cert.NotAfter
```

You may want to store this certficate in azure key vault, in order not to lose it. To do this with powershell (unfortunately, it's impossible to do with azure portal)
```powershell
$pfx = [System.Convert]::ToBase64String($cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, '<Password>'))
$secret = ConvertTo-SecureString -String $pfx -AsPlainText –Force
$secretContentType = 'application/x-pkcs12'
Set-AzureKeyVaultSecret -VaultName '<KeyVaultName>' -Name '<NameOfSecret>' -SecretValue $secret -ContentType $secretContentType
```
To retrieve it from key vault after that
```powershell
$secretRetrieved = Get-AzureKeyVaultSecret -VaultName '<KeyVaultName>' -Name '<NameOfSecret>'
$pfxBytes = [System.Convert]::FromBase64String($secretRetrieved.SecretValueText)
[io.file]::WriteAllBytes("<Path>.pfx", $pfxBytes)
```

## Server-side components

### Running performance tests

Performance tests run using Azure Batch. One experiment corresponds to one Azure Batch job.

Job ID is `{storage}_exp{id}`, where `{storage}` is the storage account name and `{id}` is the experiment id.
Such naming rule allows same batch account to be shared between multiple storage accounts eliminating duplicate job 
identifiers.

The experiment definition must be already added to the [experiments table](#table-of-experiments) when the job is starting.

When created, the job has only job preparation and job manager tasks defined.
First, **job preparation tasks** start on each of the selected batch pool machines and copy required files to 
that machine. These include the [configuration](#configuration) files (AzureWorker.exe with supporting files,
reference experiment definition).

The **job manager** task runs `AzureWorker.exe --manage-tasks` which starts enumerating the input benchmarks and produces series of performance tests tasks, one task per experiment benchmark. 

In parallel, the manager task listens the Azure Storage queue where the performance test tasks put the results.
The queue is named `exp{id}` where `{id}` is the experiment id.
The job manager collects packs of results from the queue and updates the 
[experiment results table](#experiment-results), so it is the only writer to the table blob.

To be fault-tolerant, the manager task supports the case when it fails and restarts.
So it doesn't create new performance tests tasks for benchmarks that are either
in the experiment results table (if it is already existing) or associated with the already
created performance tests tasks.

For [nightly Z3 performance tests](#nightly-z3-performance-tests), the job manager makes additional steps when all benchmarks are tested:
1. Extends the [timeline and records](#summaries) to account the experiment results.
2. If needed, sends the report to e-mails listed in the AzureWorker.exe.config.


The **performance test task** runs `AzureWorker.exe --measure` which does the following:
1. Finds the performance normalization coefficient for this machine. For each machine it is computed only once
as ratio of the reference value to the total processor time for runs of a certain executable for the reference benchmarks.
The executable, the reference value and the reference benchmark files are described in the `reference.json` file of the `config` blob container.
The coefficient is then saved to a local file `normal.txt`.

2. Measures execution of the target executable for a benchmark file assigned to the task. If adaptive run is enabled
in the experiment definition, the test can run multiple times and the results are then aggregated:
    - if all runs succeeded and all exit codes are same, 
    it takes median processor and wall clock times and maximum used memory;
    - otherwise, the first unsuccessful result is returned.

3. If the executable output is too large, it is saved to the `output` blob container as described [above](#outputs).

4. Measurements are then queued to the Azure Storage queue.

If the test task fails, it restarts and runs the test again up to 5 times. 
If it still fails after that, the job manager will find that and set an infrastructure error as a result for the 
 benchmark associated with the failed test task.

![Running performance tests using Azure Batch](https://raw.githubusercontent.com/Z3Prover/PerformanceTest/gh-pages/Z3perftest-architecture.png)

To submit new experiment from code, use `AzurePerformanceTest.AzureExperimentManager.StartExperiment()` method.

### Requeueing benchmarks

When an experiment is complete, it is possible to run tests again for selected benchmarks of that experiment.
Existing results for those benchmarks remain in the experiment results table 
so there will appear duplicates when the tests complete.

In the `temp` container, a new blob is created containing list of requeued benchmarks. 

If the Batch job for the experiment exists, it is removed and new job is created with same name.
The job manager task for the new job is `AzureWorker.exe --manage-retry`.
The algorithm is same as when running new experiment but the difference here is that it runs tests for the given benchmarks though they are in the results table.

To resubmit some benchmarks of an existing experiment from code, use `AzurePerformanceTest.AzureExperimentManager.RestartBenchmarks()` method.

### Nightly Z3 performance tests

The .NET application `/src/NightlyRunner` allows to submit performance tests for the latest nightly build of Z3. 
It does the following:

1. Finds most recent x86 binary package at [https://github.com/Z3Prover/bin/tree/master/nightly](https://github.com/Z3Prover/bin/tree/master/nightly). 
If there are multiple files found, takes commit sha from the file names and looks to the commit history of the Z3 repository to determine which is most recent.
2. Finds the last nightly performance experiment.
3. If the most recent build differs from the last experiment executable, does the following:
  
    1. Uploads new x86 z3 binary package to the blob container `bin` and sets its metadata attribute to the original file name of the package.
    2. Submits [new performance experiment](#running-performance-tests).

When all benchmarks of the experiment complete, the [summary](#summaries) determined by the
parameter `SummaryName` in `NightlyRunner` configuration file (default name is `Z3Nightly`) is updated.

Note that if afterwards you manually change the experiment results (for example, resolve duplicates using UI application), 
you will need to manually update the summary using `Summary.exe` utility 
(see [Timeline and records builder](#timeline-and-records-builder)).


### How to schedule nightly runs using Azure Batch Schedule

1. Prepare Azure Batch Pool and choose an appropriate certificate to be installed on Batch nodes. This is 
required to enable access to the Azure Key Vault.

1. Check `NightlyRunner` tool settings. They are located in the `NightlyRunner.exe.config` file.

    * Parameters `Creator`, `BenchmarkDirectory`, `BenchmarkCategory`, `BenchmarkFileExtension`, `Parameters`, `Domain`, `ExperimentNote`, 
    `BenchmarkTimeoutSeconds`, `MemoryLimitMegabytes` define properties of the nightly performance test experiment
    to be submitted.
    * `AzureBatchPoolId` defines which Azure Batch pool to be used to run the experiment.
    * Parameters `GitHubOwner`, `GitHubZ3Repository`, `GitHubBinariesRepository`, `GitHubBinariesNightlyFolder`, `RegexExecutableFileName`, `RegexExecutableFileName_CommitGroup` define origin of the nightly build results and regular expression pattern for the built binary file name.
    * Parameters `ConnectionString`, `ConnectionStringSecretId`, `AADApplicationId`, `AADApplicationCertThumbprint`, `KeyVaultUrl` allow to connect to Azure Performance test infrastructure. 
        * If the `ConnectionString` is not empty, it must contain both storage account and batch account connection strings. In this case, all other parameters of this group are ignored. **Configuration file having the connection string must not be publicly available.**
        * Otherwise, other parameters must be provided so the program could access the Azure Key Vault to take the specified connection string. The machine must have the appropriate certificate installed. See also [secrets](#secrets) for more information.


1. Create Azure Batch Application package for `NightlyRunner`. 

    1. Open Batch account page at the Azure portal.
    1. Click `Features/Applications` and then click `Add`.
    1. Enter application id, for instance, `NightlyRunner`.
    1. Enter any version identifier.
    1. Compress NightlyRunner.exe, NightlyRunner.exe.config and all its \*.dll files to a zip file and select it as the Application package.
    1. Click `OK` to create the application.
    1. When the application created, open its properties and select the uploaded package as default version for the application.
  
1. Schedule execution of the application. Open PowerShell and use the following commands to create new schedule:

```powershell
Login-AzureRmAccount

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
$JobSpecification.PoolInformation.PoolId = ...pool id... # <-- enter pool id here

$Schedule = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSSchedule"
$Schedule.RecurrenceInterval = [TimeSpan]::FromDays(1)

$BatchContext = Get-AzureRmBatchAccountKeys 
New-AzureBatchJobSchedule -Id "NightlyRunSchedule" -Schedule $Schedule -JobSpecification $JobSpecification -BatchContext $BatchContext
```

### How to enable reports sent by e-mail

When an experiment for Z3 nightly build is complete, the job manager builds summary and, if enabled,
can send a report by e-mail.

`AzureWorker.exe.config` contains following parameters:

- `ReportRecipients` contains a string with e-mail addresses separated by semicolon `;`.
- `SmtpServerUrl` is an address of an SMTP server to send e-mails.
- `SendEmailCredentialsSecretId` contains name of a secret in the [Key Vault](#secrets)
which keeps user name and password divided by `:`. E.g. `user@foo.com:password`.
If the property value is an empty string, no e-mail is sent.



## Client applications

### PerformanceTest.Management

Windows application PerformanceTest.Management shows a list of experiments and results for each of the experiments, compares two experiments and exposes set of features to manage experiments.

It also allows to resolve duplicated benchmarks in experiment results, resubmit experiments, requeue some benchmarks of a completed
experiment.

### Z3 Nightly Web Application

Web application NightlyWebApp is intended to show history of experiments for Z3 nightly builds and perform statistical analysis of results.

The web application uses the Azure Key Vault to access storage account. The machine running the web application must have 
the appropriate certificate installed.
See also [secrets](#secrets) for more information about access configuration.

The web application can be configured using Web.config file. In Visual Studio, you can open Settings window for the project to change the configuration. 

The `SummaryName` property determines which timeline and records are downloaded from the `summary` blob container.
Default is `Z3Nightly`. Then the application filters only those experiments of the `experiments` table that are listed in the timeline. See more about summaries [here](#summaries).

#### Experiment tags

The web application identifies experiments either by ID or by tag.
Tags for a timeline experiments can be listed in a blob `{summaryName}.tags.csv` of the blob container `summary`;
e.g. `Z3Nightly.tags.csv`.

The file is a CSV table with two columns, ID and Name. First column is for experiment ID, second column contains tag name.

```
Id,Name
158,Experiment A
184,Experiment B
```

### Timeline and records builder

The .NET application `/src/Summary` allows to compute timeline entry and records for an experiment and then either append or replace
corresponding data in a given [summary blob](#summaries). If the given experiment is missing, the program fails.

Settings for the program should be edited in the `Summary.exe.config` file.
See also [secrets](#secrets) for more information about configuration.

For example, following command updates or adds summary for the experiment 100 in a blob `Z3Nightly.zip` of the container `summary`:

```
> Summary.exe 100 Z3Nightly
```

### Import experiment from an obsolete data formats

The `ImportTimelime.exe` command line application allows to import experiment list and results from 
local files collected when the performance tests were running on HPC.

1. Uploads experiment results to the `results` blob container.
2. Updates the experiments table so it contains the imported experiments definitions using same IDs.
Note that it silently replaces the existing experiments with same ID in the target storage account.
3. Updates timeline and records of `Z3Nightly.zip` in the `summary` blob container. 

# Run and deploy

## Deployment scripts

For the sake of convenience, a number of powershell scripts is situated in `/deployment` folder, that allow to streamline deployment processes. Most notable of those are:

1. `Deploy-Everything.ps1` - builds and deploys all the entities required to run performance tests of z3 in Azure. These include:
    * Resource group that contains everything else (if a resource group with the given name exists already, it will be used instead of creating a new one).
    * Storage account (if a storage account with the given name exists already, it will be used instead of creating a new one).
    * Batch account (if a batch account with the given name exists already, it will be used instead of creating a new one).
    * Key vault where keys to storage and batch are securely kept (if a key vault with the given name exists already, it will be used instead of creating a new one).
    * Nightly web application  (if a web application with the given name exists already, it will be updated instead of creating a new one).
    * Reference experiment settings and data (if provided).
    * Azure worker - application that measures performance in azure batch.
    * Nightly runner - application that starts nightly performance tests. Is scheduled to run in batch every 24 hours.
    * Azure Active Directory (AAD) application - an entity required to authenticate azure worker, nightly web app, and nightly runner in azure (if an AAD application with the given name exists already, you will be prompted to use it or create a new one).
    * A self-signed certificate as credentials for AAD application. Certificate (as base64-encoded pfx) and the password to its private key are also stored in the key vault.

    You should also use this script to update the certificate credentials for AAD application.

2. `Deploy-ReferenceExperiment.ps1` - deploys reference experiment.
3. `Update-AzureWorker.ps1` - builds and updates Azure worker in an existing deployment. AzureWorker.exe.config file is not updated so, that configuration is preserved.
4. `Update-NightlyRunner.ps1` - builds and updates Nightly runner in an existing deployment. NightlyRunner.exe.config file is not updated so, that configuration is preserved.
5. `Update-NightlyWebApp.ps1` - builds and updates Nightly web app in an existing deployment. Web.config file is not updated so, that configuration is preserved.

Other scripts located there are:
* `Build-AzureWorker.ps1` - builds AzureWorker.
* `Build-NightlyRunner.ps1` - builds Nightly runner.
* `Build-NightlyWebApp.ps1` - builds Nightly web app.
* `Deploy-AADApp.ps1` - creates a new Azure Active Directory application with certificate credentials. If AAD application with given name exists already, prompts user to either add a certificate credentials to existing application or create a new one.
* `Deploy-AzureWorker.ps1` - builds, configures, and deploys AzureWorker.
* `Deploy-Batch.ps1` - retrieves azure batch account with given name. If it doesn't exist, creates a new one. Associates a storage account with it. Adds a provided certificate to the batch account and all of its pools. If batch account has no pools, a new one is created.
* `Deploy-KeyVault.ps1` - retrieves azure key vault with given name. If it doesn't exist, creates a new one. Puts there a connection string to z3 performance testing environment and gives an AAD app permission to access it.
* `Deploy-NightlyRunner.ps1` - builds, configures, and deploys Nightly runner. Schedules nightly test runs.
* `Deploy-ResourceGroup.ps1` - retrieves azure resource group with given name. If it doesn't exist, creates a new one.
* `Deploy-Storage.ps1` - retrieves azure storage with given name. If it doesn't exist, creates a new one.
* `Deploy-WebApp.ps1` - builds, configures, and deploys Nightly web app.
* `New-AADApp.ps1` - creates a new Azure Active Directory application with certificate credentials.
* `New-Cert.ps1` - creates a new self-signed certificate, which can be used in z3 performance testing environment.

To use these scripts, open powershell console in the `/deployment` folder and log into Azure using `Login-AzureRmAccount` cmdlet. All scripts have complete built-in reference information, parameter specifications included, accessible via `Get-Help` cmdlet.


