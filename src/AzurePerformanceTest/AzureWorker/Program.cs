using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Measurement;
using System.IO;
using PerformanceTest;
using AzureWorker.Properties;
using Microsoft.Azure.Batch.Auth;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Azure.Batch;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Diagnostics;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Threading;
using AzurePerformanceTest;
using System.Globalization;

namespace AzureWorker
{
    class Program
    {
        static readonly TimeSpan ExtraTimeForOverhead = TimeSpan.FromSeconds(900);

        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                //TODO: Proper help
                Console.WriteLine("Not enough arguments.");
                return 1;
            }

            var subArgs = args.Skip(1).ToArray();
            switch (args[0])
            {
                case "--measure":
                    Measure(subArgs).Wait();
                    return 0;
                case "--reference-run":
                    RunReference(subArgs).Wait();
                    return 0;
                case "--manage-tasks":
                    ManageTasks(subArgs).Wait();
                    return 0;
                case "--manage-retry":
                    ManageRetry(subArgs).Wait();
                    return 0;
                default:
                    Console.WriteLine("Incorrect first parameter.");
                    return 1;
            }
        }

        const string PerformanceCoefficientFileName = "normal.txt";
        const string SharedDirEnvVariableName = "AZ_BATCH_NODE_SHARED_DIR";
        const string JobIdEnvVariableName = "AZ_BATCH_JOB_ID";
        const string InfrastructureErrorPrefix = "INFRASTRUCTURE ERROR: ";

        static async Task ManageRetry(string[] args)
        {
            int experimentId = int.Parse(args[0], CultureInfo.InvariantCulture);
            string benchmarkListBlobId = args[1];
            string benchmarkContainerUri = null;
            if (args.Length > 2)
            {
                benchmarkContainerUri = args[2];
            }

            string jobId = Environment.GetEnvironmentVariable(JobIdEnvVariableName);

            var secretStorage = new SecretStorage(Settings.Default.AADApplicationId, Settings.Default.AADApplicationCertThumbprint, Settings.Default.KeyVaultUrl);
            BatchConnectionString credentials = new BatchConnectionString(await secretStorage.GetSecret(Settings.Default.ConnectionStringSecretId));
            Console.WriteLine("Retrieved credentials.");


            var batchCred = new BatchSharedKeyCredentials(credentials.BatchURL, credentials.BatchAccountName, credentials.BatchAccessKey);

            var storage = new AzureExperimentStorage(credentials.WithoutBatchData().ToString());

            var expInfo = await storage.GetExperiment(experimentId);

            if (benchmarkContainerUri == null)
            {
                if (expInfo.BenchmarkContainerUri != ExperimentDefinition.DefaultContainerUri)
                {
                    throw new ArgumentException("New URI for non-default benchmark container was not provided.");
                }
                else
                {
                    benchmarkContainerUri = ExperimentDefinition.DefaultContainerUri;
                }
            }

            AzureBenchmarkStorage benchmarkStorage = CreateBenchmarkStorage(benchmarkContainerUri, storage);

            var queue = await storage.CreateResultsQueue(experimentId);
            Console.WriteLine("Created queue");

            // We can't tell bad results we got during previous runs on the same experiment from bad results
            // we got during this run when job manager crashed, so we put them all into 'good' list.
            // 'Fresh' (and, therefore, duplicate) bad results will be removed during deduplication.
            goodResults = (await storage.GetAzureExperimentResults(experimentId)).Item1.ToList();
            Console.WriteLine("Fetched existing results");
            Domain domain = ResolveDomain(expInfo.DomainName);


            string benchmarksPath = CombineBlobPath(expInfo.BenchmarkDirectory, expInfo.Category);
            var benchmarkListBlob = storage.TempBlobContainer.GetBlockBlobReference(benchmarkListBlobId);
            string[] benchmarkList = (await benchmarkListBlob.DownloadTextAsync()).Split('\n')
                .SelectMany(s =>
                {
                    s = s.Trim();
                    if (string.IsNullOrEmpty(s))
                        return new string[] { };
                    else
                        return new string[] { benchmarksPath + s };
                }).ToArray();
            totalBenchmarksToProcess = benchmarkList.Length;
            totalBenchmarks = expInfo.TotalBenchmarks;
            Console.WriteLine("Retrieved list of benchmarks to re-process. Total: {0}.", totalBenchmarksToProcess);
            var collectionTask = CollectResults(experimentId, storage);
            Console.WriteLine("Started collection thread.");

            using (BatchClient batchClient = BatchClient.Open(batchCred))
            {
                //not all experiments started
                ODATADetailLevel detailLevel = new ODATADetailLevel();
                detailLevel.SelectClause = "id,displayName";

                Console.WriteLine("Listing existing tasks.");
                var processedBlobs = new SortedSet<string>(batchClient.JobOperations.ListTasks(jobId, detailLevel)
                    .SelectMany(t =>
                    {
                        int id;
                        if (int.TryParse(t.Id, out id))
                        {
                            // we put benchmark file first
                            return new string[] { t.DisplayName };
                        }
                        return new string[] { };
                    }));
                Console.WriteLine("Done!");

                string outputQueueUri = storage.GetOutputQueueSASUri(experimentId, TimeSpan.FromHours(48));
                string outputContainerUri = storage.GetOutputContainerSASUri(TimeSpan.FromHours(48));
                string[] blobsToProcess = benchmarkList.Where(b => !processedBlobs.Contains(b)).ToArray();

                if (blobsToProcess.Length > 0)
                {
                    var starterTask = StartTasksForSegment(expInfo.BenchmarkTimeout.ToString(), experimentId, expInfo.Executable, expInfo.Parameters, expInfo.MemoryLimitMB, expInfo.DomainName, outputQueueUri, outputContainerUri, null, null, jobId, batchClient, blobsToProcess, benchmarksPath, 0, benchmarkStorage, expInfo.AdaptiveRunMaxRepetitions, expInfo.AdaptiveRunMaxTimeInSeconds);

                    await starterTask;
                    Console.WriteLine("Finished starting tasks");
                }

                MonitorTasksUntilCompletion(experimentId, jobId, collectionTask, batchClient);
            }

            Console.WriteLine("Deleting blob with benchmark list.");
            await benchmarkListBlob.DeleteIfExistsAsync();
            Console.WriteLine("Closing.");
        }

        static async Task ManageTasks(string[] args)
        {
            int experimentId = int.Parse(args[0], CultureInfo.InvariantCulture);
            string summaryName = null;
            if (args.Length > 1)
                summaryName = args[1];
            //Console.WriteLine(String.Format("Params are:\n id: {0}\ncontainer: {8}\ndirectory:{9}\ncategory: {1}\nextensions: {10}\ndomain: {11}\nexec: {2}\nargs: {3}\ntimeout: {4}\nmemlimit: {5}\noutlimit: {6}\nerrlimit: {7}", experimentId, benchmarkCategory, executable, arguments, timeout, memoryLimit, outputLimit, errorLimit, benchmarkContainerUri, benchmarkDirectory, extensionsString, domainString));

            string jobId = Environment.GetEnvironmentVariable(JobIdEnvVariableName);

            var secretStorage = new SecretStorage(Settings.Default.AADApplicationId, Settings.Default.AADApplicationCertThumbprint, Settings.Default.KeyVaultUrl);
            BatchConnectionString credentials = new BatchConnectionString(await secretStorage.GetSecret(Settings.Default.ConnectionStringSecretId));
            Console.WriteLine("Retrieved credentials.");


            var batchCred = new BatchSharedKeyCredentials(credentials.BatchURL, credentials.BatchAccountName, credentials.BatchAccessKey);

            var storage = new AzureExperimentStorage(credentials.WithoutBatchData().ToString());

            var expInfo = await storage.GetExperiment(experimentId);

            string benchmarkContainerUri = expInfo.BenchmarkContainerUri;// args[1];
            string benchmarkDirectory = expInfo.BenchmarkDirectory;// args[2];
            string benchmarkCategory = expInfo.Category;// args[3];
            string extensionsString = expInfo.BenchmarkFileExtension; //args[4];
            string domainString = expInfo.DomainName;// args[5];
            string executable = expInfo.Executable;// args[6];
            string arguments = expInfo.Parameters;// args[7];
            double timeout = expInfo.BenchmarkTimeout;// TimeSpan.FromSeconds(double.Parse(args[8]));
            double memoryLimit = expInfo.MemoryLimitMB;// 0; // no limit
            int maxRepetitions = expInfo.AdaptiveRunMaxRepetitions;
            double maxTime = expInfo.AdaptiveRunMaxTimeInSeconds;

            //long? outputLimit = null;
            //long? errorLimit = null;
            //if (args.Length > 9)
            //{
            //    memoryLimit = double.Parse(args[9]);
            //    if (args.Length > 10)
            //    {
            //        outputLimit = args[10] == "null" ? null : (long?)long.Parse(args[10]);
            //        if (args.Length > 11)
            //        {
            //            errorLimit = args[11] == "null" ? null : (long?)long.Parse(args[11]);
            //        }
            //    }
            //}

            AzureBenchmarkStorage benchmarkStorage = CreateBenchmarkStorage(benchmarkContainerUri, storage);


            var queue = await storage.CreateResultsQueue(experimentId);
            Console.WriteLine("Created queue");

            await FetchSavedResults(experimentId, storage);
            Console.WriteLine("Fetched existing results");
            var collectionTask = CollectResults(experimentId, storage);
            Console.WriteLine("Started collection thread.");
            Domain domain = ResolveDomain(domainString);
            SortedSet<string> extensions;
            if (string.IsNullOrEmpty(extensionsString))
                extensions = new SortedSet<string>(domain.BenchmarkExtensions.Distinct());
            else
                extensions = new SortedSet<string>(extensionsString.Split('|').Select(s => s.Trim().TrimStart('.')).Distinct());

            using (BatchClient batchClient = BatchClient.Open(batchCred))
            {
                if (expInfo.TotalBenchmarks <= 0)
                {
                    //not all experiments started
                    ODATADetailLevel detailLevel = new ODATADetailLevel();
                    detailLevel.SelectClause = "id,displayName";

                    Console.WriteLine("Listing existing tasks.");
                    var processedBlobs = new SortedSet<string>(batchClient.JobOperations.ListTasks(jobId, detailLevel)
                        .SelectMany(t =>
                        {
                            int id;
                            if (int.TryParse(t.Id, out id))
                            {
                                // we put benchmark file first
                                return new string[] { t.DisplayName };
                            }
                            return new string[] { };
                        }));
                    Console.WriteLine("Done!");

                    BlobContinuationToken continuationToken = null;
                    BlobResultSegment resultSegment = null;

                    List<Task> starterTasks = new List<Task>();
                    int totalBenchmarks = 0;
                    string benchmarksPath = CombineBlobPath(benchmarkDirectory, benchmarkCategory);
                    string outputQueueUri = storage.GetOutputQueueSASUri(experimentId, TimeSpan.FromHours(48));
                    string outputContainerUri = storage.GetOutputContainerSASUri(TimeSpan.FromHours(48));
                    do
                    {
                        resultSegment = await benchmarkStorage.ListBlobsSegmentedAsync(benchmarksPath, continuationToken);
                        Console.WriteLine("Got some blobs");
                        string[] blobNamesToProcess = resultSegment.Results.SelectMany(item =>
                        {
                            var blob = item as CloudBlockBlob;
                            if (blob == null || processedBlobs.Contains(blob.Name))
                                return new string[] { };

                            var nameParts = blob.Name.Split('/');
                            var shortnameParts = nameParts[nameParts.Length - 1].Split('.');
                            if (shortnameParts.Length == 1 && !extensions.Contains(""))
                                return new string[] { };
                            var ext = shortnameParts[shortnameParts.Length - 1];
                            if (!extensions.Contains(ext))
                                return new string[] { };

                            return new string[] { blob.Name };
                        }).ToArray();
                        starterTasks.Add(StartTasksForSegment(timeout.ToString(), experimentId, executable, arguments, memoryLimit, domainString, outputQueueUri, outputContainerUri, null, null, jobId, batchClient, blobNamesToProcess, benchmarksPath, totalBenchmarks, benchmarkStorage, maxRepetitions, maxTime));

                        continuationToken = resultSegment.ContinuationToken;
                        totalBenchmarks += blobNamesToProcess.Length;
                    }
                    while (continuationToken != null);

                    await storage.SetTotalBenchmarks(experimentId, totalBenchmarks);
                    Program.totalBenchmarks = totalBenchmarks;
                    totalBenchmarksToProcess = totalBenchmarks;

                    await Task.WhenAll(starterTasks.ToArray());
                    Console.WriteLine("Finished starting tasks");
                }
                else
                {
                    Program.totalBenchmarks = expInfo.TotalBenchmarks;
                    totalBenchmarksToProcess = expInfo.TotalBenchmarks;
                }

                MonitorTasksUntilCompletion(experimentId, jobId, collectionTask, batchClient);

                if (summaryName != null && expInfo.Creator == "Nightly")
                {
                    Trace.WriteLine(string.Format("Building summary for experiment {0} and summary name {1}...", experimentId, summaryName));
                    AzureSummaryManager manager = new AzureSummaryManager(credentials.WithoutBatchData().ToString(), MEFDomainResolver.Instance);
                    await AppendSummaryAndSendReport(summaryName, experimentId, domain, manager);
                }
                else
                {
                    Trace.WriteLine("No summary requested.");
                }
                Console.WriteLine("Closing.");
            }
        }
        private static async Task AppendSummaryAndSendReport(string summaryName, int experimentId, Domain domain, AzureSummaryManager manager)
        {
            Trace.WriteLine("Building summary...");

            ExperimentSummary[] summaries = await manager.Update(summaryName, experimentId);
            if (summaries.Length == 0) return;

            try
            {
                var secretStorage = new SecretStorage(Settings.Default.AADApplicationId, Settings.Default.AADApplicationCertThumbprint, Settings.Default.KeyVaultUrl);
                string credentials = await secretStorage.GetSecret(Settings.Default.SendEmailCredentialsSecretId);

                var sendMail = new SendMail(credentials, Settings.Default.SmtpServerUrl);
                await sendMail.SendReport(manager, summaries[0], summaries.Length > 1 ? summaries[1] : null, Settings.Default.ReportRecipients, Settings.Default.LinkPage);

                Trace.WriteLine("Done.");
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Can't send email: " + ex.Message);
                return;
            }
        }
        private static void MonitorTasksUntilCompletion(int experimentId, string jobId, Task collectionTask, BatchClient batchClient)
        {
            // Monitoring tasks
            ODATADetailLevel failedMonitorLevel = new ODATADetailLevel();
            failedMonitorLevel.FilterClause = "(state eq 'completed') and (executionInfo/exitCode ne 0)";
            failedMonitorLevel.SelectClause = "id,displayName,executionInfo";
            ODATADetailLevel completedMonitorLevel = new ODATADetailLevel();
            completedMonitorLevel.FilterClause = "(state eq 'completed')";
            failedMonitorLevel.SelectClause = "id";
            do
            {
                Console.WriteLine("Fetching failed tasks...");
                badResults = batchClient.JobOperations.ListTasks(jobId, failedMonitorLevel)
                    .Select(task => new AzureBenchmarkResult
                    {
                        AcquireTime = task.ExecutionInformation.StartTime ?? DateTime.MinValue,
                        BenchmarkFileName = task.DisplayName,
                        ExitCode = task.ExecutionInformation.ExitCode,
                        ExperimentID = experimentId,
                        StdErr = InfrastructureErrorPrefix + task.ExecutionInformation.FailureInformation.Message,
                        StdErrExtStorageIdx = "",
                        StdOut = "",
                        StdOutExtStorageIdx = "",
                        NormalizedRuntime = 0,
                        PeakMemorySizeMB = 0,
                        Properties = new Dictionary<string, string>(),
                        Status = ResultStatus.InfrastructureError,
                        TotalProcessorTime = TimeSpan.Zero,
                        WallClockTime = TimeSpan.Zero
                    }).ToList();
                Console.WriteLine("Done fetching failed tasks. Got {0}.", badResults.Count);
                Console.WriteLine("Fetching completed tasks...");
                completedTasksCount = batchClient.JobOperations.ListTasks(jobId, completedMonitorLevel).Count();
                Console.WriteLine("Done fetching completed tasks. Got {0}.", completedTasksCount);
            }
            while (!collectionTask.Wait(30000));
        }

        static List<AzureBenchmarkResult> goodResults = new List<AzureBenchmarkResult>();
        static List<AzureBenchmarkResult> badResults = new List<AzureBenchmarkResult>();
        static int completedTasksCount = 0;
        static int totalBenchmarks = -1;
        static int totalBenchmarksToProcess = -1;

        static async Task FetchSavedResults(int experimentId, AzureExperimentStorage storage)
        {
            var results = (await storage.GetAzureExperimentResults(experimentId)).Item1;
            goodResults = new List<AzureBenchmarkResult>();
            badResults = new List<AzureBenchmarkResult>();
            foreach (var r in results)
            {
                if (r.StdErr.StartsWith(InfrastructureErrorPrefix) || (!string.IsNullOrEmpty(r.StdErrExtStorageIdx) && Utils.StreamToString(storage.ParseAzureBenchmarkResult(r).StdErr, false).StartsWith(InfrastructureErrorPrefix)))
                    badResults.Add(r);
                else
                    goodResults.Add(r);
            }
        }

        static async Task CollectResults(int experimentId, AzureExperimentStorage storage)
        {
            Console.WriteLine("Started collection.");
            var queue = storage.GetResultsQueueReference(experimentId);
            List<AzureBenchmarkResult> results = new List<AzureBenchmarkResult>();// (await storage.GetAzureExperimentResults(experimentId)).ToList();
            int processedBenchmarks = 0;// goodResults.Count + badResults.Count;// results.Count;

            var formatter = new BinaryFormatter();
            bool completed = false;
            do
            {
                completed = totalBenchmarksToProcess != -1 && completedTasksCount >= totalBenchmarksToProcess;
                var messages = queue.GetMessages(32, TimeSpan.FromMinutes(5));
                int messageCount = messages.Count();
                completed = completed && messageCount == 0;
                foreach (CloudQueueMessage message in messages)
                {
                    using (var ms = new MemoryStream(message.AsBytes))
                    {
                        goodResults.Add((AzureBenchmarkResult)formatter.Deserialize(ms));
                    }
                }
                int oldCount = results.Count;
                results = goodResults.Concat(badResults).ToList();
                var tuple = SortCountUniqueNamesAndRemoveExactDuplicates(results);
                processedBenchmarks = tuple.Item1;
                results = tuple.Item2;
                await storage.PutAzureExperimentResults(experimentId, results.ToArray(), AzureExperimentStorage.UploadBlobMode.CreateOrReplace);
                int completedBenchmarks = totalBenchmarks == -1 ? processedBenchmarks : totalBenchmarks - totalBenchmarksToProcess + completedTasksCount;
                await storage.SetCompletedBenchmarks(experimentId, completedBenchmarks);
                Console.WriteLine("Setting completed benchmarks to {0}.\nTotal benchmarks: {1}\nProcessed benchmarks: {2}\nTotal to process: {3}\nCompleted tasks: {4}\nMessage count: {5}", completedBenchmarks, totalBenchmarks, processedBenchmarks, totalBenchmarksToProcess, completedTasksCount, messageCount);
                foreach (CloudQueueMessage message in messages)
                {
                    queue.DeleteMessage(message);
                }
                if (oldCount == results.Count)
                    Thread.Sleep(500);
            }
            while (!completed);
            await storage.DeleteResultsQueue(experimentId);

            var totalRuntime = results.Sum(r => r.NormalizedRuntime);
            await storage.SetTotalRuntime(experimentId, totalRuntime);
            Console.WriteLine("Collected all results.");
        }

        private static bool AreStringDictionariesEqual(Dictionary<string, string> x, Dictionary<string, string> y)
        {
            if (x.Count != y.Count)
                return false;

            foreach (var pair in x)
            {
                if (!y.ContainsKey(pair.Key) || y[pair.Key] != pair.Value)
                    return false;
            }

            return true;
        }

        class AzureBenchmarkResultsComparer : IEqualityComparer<AzureBenchmarkResult>
        {
            public static bool AreAzureBenchmarkResultsEqual(AzureBenchmarkResult x, AzureBenchmarkResult y)
            {
                return x.NormalizedRuntime == y.NormalizedRuntime
                    && x.TotalProcessorTime == y.TotalProcessorTime
                    && x.WallClockTime == y.WallClockTime
                    && x.AcquireTime == y.AcquireTime
                    && x.BenchmarkFileName == y.BenchmarkFileName
                    && x.ExitCode == y.ExitCode
                    && x.ExperimentID == y.ExperimentID
                    && x.PeakMemorySizeMB == y.PeakMemorySizeMB
                    && AreStringDictionariesEqual(x.Properties, y.Properties)
                    && x.Status == y.Status
                    && x.StdErr == y.StdErr
                    && x.StdErrExtStorageIdx == y.StdErrExtStorageIdx
                    && x.StdOut == y.StdOut
                    && x.StdOutExtStorageIdx == y.StdOutExtStorageIdx;
            }

            public bool Equals(AzureBenchmarkResult x, AzureBenchmarkResult y)
            {
                return AreAzureBenchmarkResultsEqual(x, y);
            }

            public int GetHashCode(AzureBenchmarkResult obj)
            {
                return obj.NormalizedRuntime.GetHashCode() ^ obj.AcquireTime.GetHashCode();
            }
        }

        private static Tuple<int, List<AzureBenchmarkResult>> SortCountUniqueNamesAndRemoveExactDuplicates(List<AzureBenchmarkResult> results)
        {
            if (results.Count == 0)
                return new Tuple<int, List<AzureBenchmarkResult>>(0, results);

            var groups = results.GroupBy(r => r.BenchmarkFileName).ToList();
            groups.Sort((a, b) => string.Compare(a.Key, b.Key));
            int uniqueNameCount = groups.Count;
            var comparer = new AzureBenchmarkResultsComparer();
            var distinct = groups.SelectMany(g => g.Distinct(comparer)).ToList();
            return new Tuple<int, List<AzureBenchmarkResult>>(uniqueNameCount, distinct);
        }

        private static string CombineBlobPath(string benchmarkDirectory, string benchmarkCategory)
        {
            string benchmarksPath;
            if (string.IsNullOrEmpty(benchmarkDirectory))
                benchmarksPath = benchmarkCategory;
            else if (string.IsNullOrEmpty(benchmarkCategory))
                benchmarksPath = benchmarkDirectory;
            else
            {
                var benchmarksDirClear = benchmarkDirectory.TrimEnd('/');
                var benchmarksCatClear = benchmarkCategory.TrimStart('/');
                benchmarksPath = benchmarksDirClear + "/" + benchmarksCatClear;
            }
            benchmarksPath = benchmarksPath.TrimEnd('/');
            if (benchmarksPath.Length > 0)
                benchmarksPath = benchmarksPath + "/";
            return benchmarksPath;
        }

        private static async Task StartTasksForSegment(string timeout, int experimentId, string executable, string arguments, double memoryLimit, string domainName, string queueUri, string containerUri, long? outputLimit, long? errorLimit, string jobId, BatchClient batchClient, IEnumerable<string> blobNamesToProcess, string blobFolderPath, int startTaskId, AzureBenchmarkStorage benchmarkStorage, int maxRepetitions, double maxTime)
        {
            List<CloudTask> tasks = new List<CloudTask>();
            int blobNo = startTaskId;
            int blobFolderPathLength = blobFolderPath.Length;
            foreach (string blobName in blobNamesToProcess)
            {
                string taskId = blobNo.ToString();
                string[] parts = blobName.Split('/');
                string shortName = parts[parts.Length - 1];
                string taskCommandLine = String.Format("cmd /c %" + SharedDirEnvVariableName + "%\\%" + JobIdEnvVariableName + "%\\AzureWorker.exe --measure {0} \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\" \"{8}\" \"{9}\" \"{10}\" \"{11}\" \"{12}\" \"{13}\"", experimentId, blobName.Substring(blobFolderPathLength), executable, arguments, shortName, timeout, domainName, queueUri, containerUri, maxRepetitions, maxTime, memoryLimit, NullableLongToString(outputLimit), NullableLongToString(errorLimit));
                var resourceFile = new ResourceFile(benchmarkStorage.GetBlobSASUri(blobName), shortName);
                CloudTask task = new CloudTask(taskId, taskCommandLine);
                task.ResourceFiles = new List<ResourceFile> { resourceFile };
                task.Constraints = new TaskConstraints();
                task.Constraints.MaxWallClockTime = TimeSpan.FromSeconds(double.Parse(timeout, CultureInfo.InvariantCulture)) + ExtraTimeForOverhead;
                task.DisplayName = blobName;
                tasks.Add(task);

                ++blobNo;
            }
            if (tasks.Count > 0)
            {
                Console.WriteLine("Starting tasks...");
                await batchClient.JobOperations.AddTaskAsync(jobId, tasks);
                Console.WriteLine("Started some tasks");
            }
        }

        static string NullableLongToString(long? number)
        {
            return number == null ? "null" : number.Value.ToString();
        }

        static async Task Measure(string[] args)
        {
            int experimentId = int.Parse(args[0], CultureInfo.InvariantCulture);
            string benchmarkId = args[1];
            string executable = args[2];
            string arguments = args[3];
            string targetFile = args[4];
            TimeSpan timeout = TimeSpan.FromSeconds(double.Parse(args[5], CultureInfo.InvariantCulture));
            string domainName = args[6];
            Uri outputQueueUri = new Uri(args[7]);
            Uri outputBlobContainerUri = new Uri(args[8]);
            int maxRepetitions = int.Parse(args[9], CultureInfo.InvariantCulture);
            double maxTime = double.Parse(args[10], CultureInfo.InvariantCulture);
            double memoryLimit = 0; // no limit
            long? outputLimit = null;
            long? errorLimit = null;
            //if (args.Length > 6)
            //{
            //    workerInfo = args[6];
            if (args.Length > 11)
            {
                memoryLimit = double.Parse(args[11], CultureInfo.InvariantCulture);
                if (args.Length > 12)
                {
                    outputLimit = args[12] == "null" ? null : (long?)long.Parse(args[12], CultureInfo.InvariantCulture);
                    if (args.Length > 13)
                    {
                        errorLimit = args[13] == "null" ? null : (long?)long.Parse(args[13], CultureInfo.InvariantCulture);
                    }
                }
            }
            //}
            double normal = 1.0;

            string workerDir = Path.Combine(Environment.GetEnvironmentVariable(SharedDirEnvVariableName), Environment.GetEnvironmentVariable(JobIdEnvVariableName));
            executable = Path.Combine(workerDir, "exec", executable);
            string normalFilePath = Path.Combine(workerDir, PerformanceCoefficientFileName);
            if (File.Exists(normalFilePath))
            {
                normal = double.Parse(File.ReadAllText(normalFilePath), CultureInfo.InvariantCulture);
                Trace.WriteLine(string.Format("Normal found within file: {0}", normal));
            }
            else
            {
                Trace.WriteLine("Normal not found within file, computing.");
                normal = await RunReference(new string[] { });
            }

            Domain domain = ResolveDomain(domainName);
            BenchmarkResult result = LocalExperimentRunner.RunBenchmark(
                experimentId,
                executable,
                arguments,
                benchmarkId,
                Path.GetFullPath(targetFile),
                0,
                timeout,
                memoryLimit,
                outputLimit,
                errorLimit,
                domain,
                normal,
                maxRepetitions,
                maxTime);

            await AzureExperimentStorage.PutResult(experimentId, result, new CloudQueue(outputQueueUri), new CloudBlobContainer(outputBlobContainerUri));
        }

        static Task<double> RunReference(string[] args)
        {
            string workerDir = Path.Combine(Environment.GetEnvironmentVariable(SharedDirEnvVariableName), Environment.GetEnvironmentVariable(JobIdEnvVariableName));
            string normalFilePath = Path.Combine(workerDir, PerformanceCoefficientFileName);
            string refJsonPath = Path.Combine(workerDir, "reference.json");
            if (!File.Exists(refJsonPath))
            {
                //no reference experiment
                Trace.WriteLine("Reference.json not found, assuming normal 1.0.");
                File.WriteAllText(normalFilePath, "1.0");
                return Task.FromResult(1.0);
            }
            var exp = ParseReferenceExperiment(refJsonPath);

            var pathForBenchmarks = Path.Combine(workerDir, "refdata", "data");
            var execPath = Path.Combine(workerDir, "refdata", exp.Definition.Executable);

            Domain domain = ResolveDomain(exp.Definition.DomainName);
            string[] benchmarks = Directory.EnumerateFiles(pathForBenchmarks).Select(fn => Path.Combine(pathForBenchmarks, fn)).ToArray();
            Trace.WriteLine(string.Format("Found {0} benchmarks in folder {1}", benchmarks.Length, pathForBenchmarks));
            BenchmarkResult[] results = new BenchmarkResult[benchmarks.Length];
            for (int i = 0; i < benchmarks.Length; ++i)
            {
                Trace.WriteLine(string.Format("Procssing reference file {0}", benchmarks[i]));
                results[i] = LocalExperimentRunner.RunBenchmark(
                    -1,
                    execPath,
                    exp.Definition.Parameters,
                    "ref",
                    benchmarks[i],
                    exp.Repetitions,
                    exp.Definition.BenchmarkTimeout,
                    exp.Definition.MemoryLimitMB,
                    null,
                    null,
                    domain,
                    1.0);
            }

            var totalRuntime = results.Sum(r => r.NormalizedRuntime);
            double normal = exp.ReferenceValue / totalRuntime;

            File.WriteAllText(normalFilePath, normal.ToString());
            return Task.FromResult(normal);
        }

        static AzureBenchmarkStorage CreateBenchmarkStorage(string uri, AzureExperimentStorage experimentStorage)
        {
            if (uri == ExperimentDefinition.DefaultContainerUri)
                return experimentStorage.DefaultBenchmarkStorage;
            else
                return new AzureBenchmarkStorage(uri);
        }

        private static ReferenceExperiment ParseReferenceExperiment(string filename)
        {
            string content = File.ReadAllText(filename);
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ContractResolver = new PrivatePropertiesResolver();
            ReferenceExperiment reference = JsonConvert.DeserializeObject<ReferenceExperiment>(content, settings);
            return reference;
        }

        private static Domain ResolveDomain(string domainName)
        {
            var domainResolver = MEFDomainResolver.Instance;
            return domainResolver.GetDomain(domainName);
        }

        internal class PrivatePropertiesResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty prop = base.CreateProperty(member, memberSerialization);
                prop.Writable = true;
                return prop;
            }
        }
    }
}
