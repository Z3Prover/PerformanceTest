using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch;
using Azure.Identity;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.Batch.Common;
using System.IO;
using System.Diagnostics;
using PerformanceTest;
using Measurement;
using System.Threading;

using ExperimentID = System.Int32;
using Azure.Core;

namespace AzurePerformanceTest
{
    public class AzureExperimentManager : ExperimentManager
    {
        const int MaxTaskRetryCount = 1;
        const string DefaultPoolID = "z3-main";

        AzureExperimentStorage storage;
        BatchSharedKeyCredentials batchCreds1;
        BatchTokenCredentials batchCreds2;

        protected AzureExperimentManager(AzureExperimentStorage storage, string batchUrl, string batchAccName, string batchKey)
        {
            this.storage = storage;
            this.batchCreds1 = new BatchSharedKeyCredentials(batchUrl, batchAccName, batchKey);            
            this.BatchPoolID = DefaultPoolID;
        }

        protected AzureExperimentManager(AzureExperimentStorage storage, string batchUrl, string managedClientId, string[] scopes)
        {
            this.storage = storage;
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = managedClientId });
            Azure.Core.AccessToken token = credential.GetToken(new Azure.Core.TokenRequestContext(scopes), new System.Threading.CancellationToken());
            this.batchCreds1 = null;
            this.batchCreds2 = new BatchTokenCredentials(batchUrl, token.Token);
            this.BatchPoolID = DefaultPoolID;
        }

        protected AzureExperimentManager(AzureExperimentStorage storage)
        {
            this.storage = storage;
            this.batchCreds1 = null;
            this.batchCreds2 = null;
            this.BatchPoolID = DefaultPoolID;
        }

        public static async Task<AzureExperimentManager> New(AzureExperimentStorage storage, ReferenceExperiment reference, string batchUrl, string batchAccName, string batchKey)
        {
            await storage.SaveReferenceExperiment(reference);
            return new AzureExperimentManager(storage, batchUrl, batchAccName, batchKey);
        }

        public static AzureExperimentManager Open(AzureExperimentStorage storage, string batchUrl, string batchAccName, string batchKey)
        {
            return new AzureExperimentManager(storage, batchUrl, batchAccName, batchKey);
        }

        public static AzureExperimentManager Open(string connectionString)
        {
            var cs = new BatchConnectionString(connectionString);
            string batchAccountName = cs.BatchAccountName;
            string batchUrl = cs.BatchURL;
            string batchAccessKey = cs.BatchAccessKey;

            cs.RemoveKeys(BatchConnectionString.KeyBatchAccount, BatchConnectionString.KeyBatchURL, BatchConnectionString.KeyBatchAccessKey);
            string storageConnectionString = cs.ToString();

            AzureExperimentStorage storage = new AzureExperimentStorage(storageConnectionString);
            if (batchAccountName != null)
                return Open(storage, batchUrl, batchAccountName, batchAccessKey);
            return OpenWithoutStart(storage);
        }

        /// <summary>
        /// Creates a manager in a mode when it can open data but not start new experiments.
        /// </summary>
        public static AzureExperimentManager OpenWithoutStart(AzureExperimentStorage storage)
        {
            return new AzureExperimentManager(storage);
        }

        public AzureExperimentStorage Storage { get { return storage; } }

        public override string BatchPoolID { get; set; }

        public bool CanStart
        {
            get { return batchCreds1 != null || batchCreds2 != null; }
        }

        public override async Task DeleteExperiment(ExperimentID id)
        {
            await StopJob(id);

            // Removing experiment entity, results and outputs.
            await storage.DeleteExperiment(id);
        }

        /// If connection to the batch client is established and experiment is running, deletes the experiment job.
        private async Task StopJob(int id)
        {
            var jobId = BuildJobId(id);
            BatchClient bc;
            try
            {
                bc = BatchOpen();
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to open batch client when tried to stop the job: " + ex.Message);
                return;
            }


            try
            {
                await bc.JobOperations.DeleteJobAsync(jobId);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to delete the job " + jobId + ": " + ex.Message);
            }
            finally
            {
                bc.Dispose();
            }
        }

        public override Task DeleteExecutable(string executableName)
        {
            if (executableName == null) throw new ArgumentNullException("executableName");
            return storage.DeleteExecutable(executableName);
        }

        public override async Task<Experiment> TryFindExperiment(int id)
        {
            try
            {
                var e = await storage.GetExperiment(id);
                return ExperimentFromEntity(id, e);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public override async Task<IEnumerable<Experiment>> FindExperiments(ExperimentFilter? filter = default(ExperimentFilter?))
        {
            IEnumerable<KeyValuePair<int, ExperimentEntity>> experiments = await storage.GetExperiments(filter);

            return
                experiments
                .OrderByDescending(q => q.Value.Submitted)
                .Select(e => ExperimentFromEntity(e.Key, e.Value));
        }

        private Experiment ExperimentFromEntity(int id, ExperimentEntity entity)
        {
            var totalRuntime = TimeSpan.FromSeconds(entity.TotalRuntime);
            ExperimentDefinition def = DefinitionFromEntity(entity);
            ExperimentStatus status = new ExperimentStatus(
                id, def.Category, entity.Submitted, entity.Creator, entity.Note,
                entity.Flag, entity.CompletedBenchmarks, entity.TotalBenchmarks, totalRuntime, entity.WorkerInformation);
            return new Experiment { Definition = def, Status = status };
        }

        public override async Task<ExperimentExecutionState[]> GetExperimentJobState(IEnumerable<int> ids)
        {
            if (!CanStart) return null;

            try
            {
                using (var bc = BatchOpen())            
                {
                    List<ExperimentExecutionState> states = new List<ExperimentExecutionState>();
                    foreach (var expId in ids)
                    {
                        var jobId = BuildJobId(expId);
                        try
                        {
                            var job = await bc.JobOperations.GetJobAsync(jobId);
                            if (job.State == null) states.Add(ExperimentExecutionState.NotFound);
                            switch (job.State.Value)
                            {
                                case JobState.Active:
                                case JobState.Disabling:
                                case JobState.Disabled:
                                case JobState.Enabling:
                                    states.Add(ExperimentExecutionState.Active);
                                    break;
                                case JobState.Completed:
                                    var jmTask = await job.GetTaskAsync(job.JobManagerTask.Id);
                                    if (!jmTask.ExecutionInformation.ExitCode.HasValue || jmTask.ExecutionInformation.ExitCode.Value != 0)
                                        states.Add(ExperimentExecutionState.Failed);
                                    else
                                        states.Add(ExperimentExecutionState.Completed);
                                    break;
                                case JobState.Terminating:
                                case JobState.Deleting:
                                    states.Add(ExperimentExecutionState.Terminated);
                                    break;
                                default:
                                    throw new InvalidOperationException("Unexpected job status");
                            }
                        }
                        catch (BatchException batchExc) when (batchExc.RequestInformation != null && batchExc.RequestInformation.HttpStatusCode.HasValue && batchExc.RequestInformation.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            states.Add(ExperimentExecutionState.NotFound);
                        }
                    }
                    return states.ToArray();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Failed to get job status: " + ex);
                return null;
            }
        }

        BatchClient BatchOpen()
        {
            return batchCreds1 != null ? BatchClient.Open(batchCreds1) : BatchClient.Open(batchCreds2);
        }

        public override async Task<string[]> GetExperimentPoolId(IEnumerable<int> ids)
        {
            if (!CanStart) return null;
            try
            {
                using (var bc = BatchOpen())
                {
                    List<string> pools = new List<string>();
                    foreach (var expId in ids)
                    {
                        var jobId = BuildJobId(expId);
                        try
                        {
                            var job = await bc.JobOperations.GetJobAsync(jobId);
                            if (job.PoolInformation == null) pools.Add("Not Found");
                            else pools.Add(job.PoolInformation.PoolId);
                        }
                        catch (BatchException batchExc) when (batchExc.RequestInformation != null && batchExc.RequestInformation.HttpStatusCode.HasValue && batchExc.RequestInformation.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            pools.Add("Not Found");
                        }
                    }
                    return pools.ToArray();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Failed to get pool id: " + ex);
                return null;
            }
        }
        private ExperimentDefinition DefinitionFromEntity(ExperimentEntity experimentEntity)
        {
            return ExperimentDefinition.Create(
                experimentEntity.Executable,
                experimentEntity.BenchmarkContainerUri,
                experimentEntity.BenchmarkDirectory,
                experimentEntity.BenchmarkFileExtension,
                experimentEntity.Parameters,
                TimeSpan.FromSeconds(experimentEntity.BenchmarkTimeout),
                TimeSpan.FromSeconds(experimentEntity.ExperimentTimeout),
                experimentEntity.DomainName,
                experimentEntity.Category,
                experimentEntity.MemoryLimitMB,
                experimentEntity.AdaptiveRunMaxRepetitions,
                experimentEntity.AdaptiveRunMaxTimeInSeconds);
        }

        public override async Task<ExperimentResults> GetResults(ExperimentID id, BenchmarkFilter f = null)
        {
            return await storage.GetResults(id, f);
        }

        public override async Task<IEnumerable<ExperimentStatus>> GetStatus(IEnumerable<ExperimentID> ids)
        {
            // todo: can be done in a more efficient way
            var req = ids.Select(id => storage.GetExperiment(id));
            var exps = await Task.WhenAll(req);
            return exps.Select(entity => ExperimentFromEntity(int.Parse(entity.RowKey, System.Globalization.CultureInfo.InvariantCulture), entity).Status);
        }

        public async Task<PoolDescription[]> GetAvailablePools()
        {
            if (!CanStart) throw new InvalidOperationException("Cannot start experiment since the manager is in read mode");

            var result = await Task.Run(() =>
            {
                using (var bc = BatchOpen())
                {
                    var pools = bc.PoolOperations.ListPools();
                    var descr = pools.Select(p => new PoolDescription
                    {
                        Id = p.Id,
                        AllocationState = p.AllocationState,
                        PoolState = p.State,
                        DedicatedNodes = p.CurrentDedicatedComputeNodes ?? 0,
                        VirtualMachineSize = p.VirtualMachineSize,
                        RunningJobs = 0,
                        AutoScaleFormula = p.AutoScaleFormula
                    }).ToArray();

                    ODATADetailLevel detailLevel = new ODATADetailLevel();
                    detailLevel.FilterClause = "state eq 'active'";
                    detailLevel.SelectClause = "poolInfo";

                    var jobPools =
                        bc.JobOperations.ListJobs(detailLevel)
                        .Select(j => j.PoolInformation.PoolId);

                    Dictionary<string, int> count = new Dictionary<string, ExperimentID>();
                    foreach (var poolId in jobPools)
                    {
                        int n;
                        if (count.TryGetValue(poolId, out n))
                            count[poolId] = n + 1;
                        else
                            count[poolId] = 1;
                    }

                    foreach (var pool in descr)
                    {
                        int n;
                        if (count.TryGetValue(pool.Id, out n))
                            pool.RunningJobs = n;
                    }

                    return descr;
                }
            });
            return result;
        }

        private void AddStarterTask(int id, string summaryName, CloudJob job, bool isRetry = false, string newBenchmarkContainerUri = ExperimentDefinition.DefaultContainerUri)
        {
            string taskId = "taskStarter";
            string taskCommandLine = !isRetry ?
                string.Format("cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\%AZ_BATCH_JOB_ID%\\AzureWorker.exe --manage-tasks {0} \"{1}\"", id, summaryName ?? "") :
                string.Format("cmd /c %AZ_BATCH_NODE_SHARED_DIR%\\%AZ_BATCH_JOB_ID%\\AzureWorker.exe --manage-retry {0} \"{1}\" \"{2}\"", id, summaryName, newBenchmarkContainerUri);
            job.JobManagerTask = new JobManagerTask(taskId, taskCommandLine);
            job.JobManagerTask.AllowLowPriorityNode = true;            
            //job.JobManagerTask.OutputFiles = job.JobManagerTask.OutputFiles ?? new List<OutputFile>();

            //foreach (string fn in new string[] { "stdout.txt", "stderr.txt" })
            //{
            //    string pre = AzureExperimentStorage.BlobNamePrefix(id);
            //    string bn = pre + "/" + fn;
            //    OutputFileUploadOptions uopt = new OutputFileUploadOptions(OutputFileUploadCondition.TaskCompletion);
            //    string outputContainerUri = storage.GetOutputContainerSASUri(TimeSpan.FromHours(72));
            //    OutputFileBlobContainerDestination oc = new OutputFileBlobContainerDestination(outputContainerUri, bn);
            //    OutputFileDestination od = new OutputFileDestination(oc);
            //    job.JobManagerTask.OutputFiles.Add(new OutputFile("$AZ_BATCH_TASK_DIR/" + fn, od, uopt));
            //}

            job.Commit();
        }

        public override async Task<ExperimentID> StartExperiment(ExperimentDefinition definition, string creator = null, string note = null, string summaryName = null)
        {
            if (!CanStart) throw new InvalidOperationException("Cannot start experiment since the manager is in read mode");

            var refExp = await storage.GetReferenceExperiment();
            var poolId = this.BatchPoolID;
            int id;

            using (var bc = BatchOpen())
            {
                var pool = await bc.PoolOperations.GetPoolAsync(poolId);
                id = await storage.AddExperiment(definition, DateTime.Now, creator, note, string.Format("{0} (pool: {1})", pool.VirtualMachineSize, poolId));
                CloudJob job = bc.JobOperations.CreateJob();
                job.Id = BuildJobId(id);
                job.OnAllTasksComplete = OnAllTasksComplete.TerminateJob;
                job.PoolInformation = new PoolInformation { PoolId = poolId };
                job.JobPreparationTask = new JobPreparationTask
                {
                    CommandLine = "cmd /c (robocopy %AZ_BATCH_TASK_WORKING_DIR% %AZ_BATCH_NODE_SHARED_DIR%\\%AZ_BATCH_JOB_ID% /e /purge) ^& IF %ERRORLEVEL% LEQ 1 exit 0",
                    ResourceFiles = new List<ResourceFile>(),
                    WaitForSuccess = true
                };

                SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
                {
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(48),
                    Permissions = SharedAccessBlobPermissions.Read
                };

                foreach (CloudBlockBlob blob in storage.ListAzureWorkerBlobs())
                {
                    string sasBlobToken = blob.GetSharedAccessSignature(sasConstraints);
                    string blobSasUri = String.Format("{0}{1}", blob.Uri, sasBlobToken);
                    job.JobPreparationTask.ResourceFiles.Add(ResourceFile.FromUrl(blobSasUri, blob.Name));
                }

                string executableFolder = "exec";
                job.JobPreparationTask.ResourceFiles.Add(ResourceFile.FromUrl(storage.GetExecutableSasUri(definition.Executable), Path.Combine(executableFolder, definition.Executable)));

                AzureBenchmarkStorage benchStorage = storage.DefaultBenchmarkStorage;
                if (refExp != null)
                {
                    string refContentFolder = "refdata";
                    string refBenchFolder = Path.Combine(refContentFolder, "data");
                    var refExpExecUri = storage.GetExecutableSasUri(refExp.Definition.Executable);
                    job.JobPreparationTask.ResourceFiles.Add(ResourceFile.FromUrl(refExpExecUri, Path.Combine(refContentFolder, refExp.Definition.Executable)));
                    if (refExp.Definition.BenchmarkContainerUri != ExperimentDefinition.DefaultContainerUri)
                        benchStorage = new AzureBenchmarkStorage(refExp.Definition.BenchmarkContainerUri);

                    Domain refdomain;
                    if (refExp.Definition.DomainName == "Z3")
                        refdomain = new Z3Domain();
                    else
                        throw new InvalidOperationException("Reference experiment uses unknown domain.");

                    SortedSet<string> extensions;
                    if (string.IsNullOrEmpty(refExp.Definition.BenchmarkFileExtension))
                        extensions = new SortedSet<string>(refdomain.BenchmarkExtensions.Distinct());
                    else
                        extensions = new SortedSet<string>(refExp.Definition.BenchmarkFileExtension.Split('|').Select(s => s.Trim().TrimStart('.')).Distinct());

                    foreach (CloudBlockBlob blob in benchStorage.ListBlobs(refExp.Definition.BenchmarkDirectory, refExp.Definition.Category))
                    {
                        string[] parts = blob.Name.Split('/');
                        string shortName = parts[parts.Length - 1];
                        var shortnameParts = shortName.Split('.');
                        if (shortnameParts.Length == 1 && !extensions.Contains(""))
                            continue;
                        var ext = shortnameParts[shortnameParts.Length - 1];
                        if (!extensions.Contains(ext))
                            continue;

                        job.JobPreparationTask.ResourceFiles.Add(ResourceFile.FromUrl(benchStorage.GetBlobSASUri(blob), Path.Combine(refBenchFolder, shortName)));
                    }
                }

                job.Constraints = new JobConstraints();

                if (definition.ExperimentTimeout != TimeSpan.Zero)
                    job.Constraints.MaxWallClockTime = definition.ExperimentTimeout;

                job.Constraints.MaxTaskRetryCount = MaxTaskRetryCount;

                AddStarterTask(id, summaryName, job, false, benchStorage.GetContainerSASUri());
            }

            return id;
        }

        public async Task<bool> Reinforce(int id, ExperimentDefinition def)
        {
            if (!CanStart) throw new InvalidOperationException("Cannot start experiment since the manager is in read mode");

            var refExp = await storage.GetReferenceExperiment();
            ExperimentEntity ee = await storage.GetExperiment(id);
            string poolId = "z3-nightly";

            Regex re = new Regex(@"^.*\(pool: ([^ ]*)\)$");
            Match m = re.Match(ee.WorkerInformation);
            if (m.Success)
                poolId = m.Groups[1].Value;

            using (var bc = BatchOpen())
            {
                var pool = await bc.PoolOperations.GetPoolAsync(poolId);
                CloudJob job = bc.JobOperations.CreateJob();
                string jid_prefix = BuildJobId(id);
                string jid = "";
                bool have_jid = false;
                int cnt = 1;
                while (!have_jid)
                {
                    try
                    {
                        jid = String.Format("{0}-{1}", jid_prefix, cnt++);
                        bc.JobOperations.GetJob(jid);
                    } catch (BatchException) {
                        have_jid = true;
                    }
                }

                job.Id = jid;
                job.OnAllTasksComplete = OnAllTasksComplete.TerminateJob;
                job.PoolInformation = new PoolInformation { PoolId = poolId };
                job.JobPreparationTask = new JobPreparationTask
                {
                    CommandLine = "cmd /c (robocopy %AZ_BATCH_TASK_WORKING_DIR% %AZ_BATCH_NODE_SHARED_DIR%\\%AZ_BATCH_JOB_ID% /e /purge) ^& IF %ERRORLEVEL% LEQ 1 exit 0",
                    ResourceFiles = new List<ResourceFile>(),
                    WaitForSuccess = true
                };

                SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
                {
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(48),
                    Permissions = SharedAccessBlobPermissions.Read
                };

                foreach (CloudBlockBlob blob in storage.ListAzureWorkerBlobs())
                {
                    string sasBlobToken = blob.GetSharedAccessSignature(sasConstraints);
                    string blobSasUri = String.Format("{0}{1}", blob.Uri, sasBlobToken);
                    job.JobPreparationTask.ResourceFiles.Add(ResourceFile.FromUrl(blobSasUri, blob.Name));
                }

                string executableFolder = "exec";
                job.JobPreparationTask.ResourceFiles.Add(ResourceFile.FromUrl(storage.GetExecutableSasUri(def.Executable), Path.Combine(executableFolder, def.Executable)));

                AzureBenchmarkStorage benchStorage = benchStorage = storage.DefaultBenchmarkStorage;
                if (refExp != null)
                {
                    string refContentFolder = "refdata";
                    string refBenchFolder = Path.Combine(refContentFolder, "data");
                    var refExpExecUri = storage.GetExecutableSasUri(refExp.Definition.Executable);
                    job.JobPreparationTask.ResourceFiles.Add(ResourceFile.FromUrl(refExpExecUri, Path.Combine(refContentFolder, refExp.Definition.Executable)));
                    if (refExp.Definition.BenchmarkContainerUri != ExperimentDefinition.DefaultContainerUri)
                        benchStorage = new AzureBenchmarkStorage(refExp.Definition.BenchmarkContainerUri);

                    Domain refdomain;
                    if (refExp.Definition.DomainName == "Z3")
                        refdomain = new Z3Domain();
                    else
                        throw new InvalidOperationException("Reference experiment uses unknown domain.");

                    SortedSet<string> extensions;
                    if (string.IsNullOrEmpty(refExp.Definition.BenchmarkFileExtension))
                        extensions = new SortedSet<string>(refdomain.BenchmarkExtensions.Distinct());
                    else
                        extensions = new SortedSet<string>(refExp.Definition.BenchmarkFileExtension.Split('|').Select(s => s.Trim().TrimStart('.')).Distinct());

                    foreach (CloudBlockBlob blob in benchStorage.ListBlobs(refExp.Definition.BenchmarkDirectory, refExp.Definition.Category))
                    {
                        string[] parts = blob.Name.Split('/');
                        string shortName = parts[parts.Length - 1];
                        var shortnameParts = shortName.Split('.');
                        if (shortnameParts.Length == 1 && !extensions.Contains(""))
                            continue;
                        var ext = shortnameParts[shortnameParts.Length - 1];
                        if (!extensions.Contains(ext))
                            continue;

                        job.JobPreparationTask.ResourceFiles.Add(ResourceFile.FromUrl(benchStorage.GetBlobSASUri(blob), Path.Combine(refBenchFolder, shortName)));
                    }
                }

                job.Constraints = new JobConstraints();

                if (def.ExperimentTimeout != TimeSpan.Zero)
                    job.Constraints.MaxWallClockTime = def.ExperimentTimeout;

                job.Constraints.MaxTaskRetryCount = MaxTaskRetryCount;
                string summaryName = ee.Creator != "Nightly" ? "" : "Z3Nightly";
                AddStarterTask(id, summaryName, job, false, benchStorage.GetContainerSASUri());
            }

            return true;
        }

        public async override Task RestartBenchmarks(int id, IEnumerable<string> benchmarkNames, string newBenchmarkContainerUri = null)
        {
            if (!CanStart) throw new InvalidOperationException("Cannot start experiment since the manager is in read mode");

            var exp = await storage.GetExperiment(id);
            if (newBenchmarkContainerUri == null)
            {
                if (exp.BenchmarkContainerUri != ExperimentDefinition.DefaultContainerUri)
                    throw new ArgumentException("No newBenchmarkContainerUri provided, but experiment uses a non-default container.");
                else
                    newBenchmarkContainerUri = ExperimentDefinition.DefaultContainerUri;
            }
            var refExp = await storage.GetReferenceExperiment();
            var poolId = this.BatchPoolID;

            var jobId = BuildJobId(id);

            string tempBlobName = Guid.NewGuid().ToString();
            await storage.TempBlobContainer.GetBlockBlobReference(tempBlobName).UploadTextAsync(string.Join("\n", benchmarkNames));

            using (var bc = BatchOpen())
            {
                //var pool = await bc.PoolOperations.GetPoolAsync(poolId);
                try
                {
                    await bc.JobOperations.DeleteJobAsync(jobId);
                }
                catch (BatchException batchExc) when (batchExc.RequestInformation != null && batchExc.RequestInformation.HttpStatusCode.HasValue && batchExc.RequestInformation.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    //Not found - nothing to delete
                }

                CloudJob job = bc.JobOperations.CreateJob();
                job.Id = jobId;
                job.OnAllTasksComplete = OnAllTasksComplete.TerminateJob;
                job.PoolInformation = new PoolInformation { PoolId = poolId };
                job.JobPreparationTask = new JobPreparationTask
                {
                    CommandLine = "cmd /c (robocopy %AZ_BATCH_TASK_WORKING_DIR% %AZ_BATCH_NODE_SHARED_DIR%\\%AZ_BATCH_JOB_ID% /e /purge) ^& IF %ERRORLEVEL% LEQ 1 exit 0",
                    ResourceFiles = new List<ResourceFile>(),
                    WaitForSuccess = true
                };

                SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
                {
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(48),
                    Permissions = SharedAccessBlobPermissions.Read
                };

                foreach (CloudBlockBlob blob in storage.ListAzureWorkerBlobs())
                {
                    string sasBlobToken = blob.GetSharedAccessSignature(sasConstraints);
                    string blobSasUri = String.Format("{0}{1}", blob.Uri, sasBlobToken);
                    job.JobPreparationTask.ResourceFiles.Add(ResourceFile.FromUrl(blobSasUri, blob.Name));
                }

                string executableFolder = "exec";
                job.JobPreparationTask.ResourceFiles.Add(ResourceFile.FromUrl(storage.GetExecutableSasUri(exp.Executable), Path.Combine(executableFolder, exp.Executable)));

                if (refExp != null)
                {
                    string refContentFolder = "refdata";
                    string refBenchFolder = Path.Combine(refContentFolder, "data");
                    var refExpExecUri = storage.GetExecutableSasUri(refExp.Definition.Executable);
                    job.JobPreparationTask.ResourceFiles.Add(ResourceFile.FromUrl(refExpExecUri, Path.Combine(refContentFolder, refExp.Definition.Executable)));
                    AzureBenchmarkStorage benchStorage;
                    if (refExp.Definition.BenchmarkContainerUri == ExperimentDefinition.DefaultContainerUri)
                        benchStorage = storage.DefaultBenchmarkStorage;
                    else
                        benchStorage = new AzureBenchmarkStorage(refExp.Definition.BenchmarkContainerUri);

                    Domain refdomain;
                    if (refExp.Definition.DomainName == "Z3")
                        refdomain = new Z3Domain();
                    else
                        throw new InvalidOperationException("Reference experiment uses unknown domain.");

                    SortedSet<string> extensions;
                    if (string.IsNullOrEmpty(refExp.Definition.BenchmarkFileExtension))
                        extensions = new SortedSet<string>(refdomain.BenchmarkExtensions.Distinct());
                    else
                        extensions = new SortedSet<string>(refExp.Definition.BenchmarkFileExtension.Split('|').Select(s => s.Trim().TrimStart('.')).Distinct());

                    foreach (CloudBlockBlob blob in benchStorage.ListBlobs(refExp.Definition.BenchmarkDirectory, refExp.Definition.Category))
                    {
                        string[] parts = blob.Name.Split('/');
                        string shortName = parts[parts.Length - 1];
                        var shortnameParts = shortName.Split('.');
                        if (shortnameParts.Length == 1 && !extensions.Contains(""))
                            continue;
                        var ext = shortnameParts[shortnameParts.Length - 1];
                        if (!extensions.Contains(ext))
                            continue;

                        job.JobPreparationTask.ResourceFiles.Add(ResourceFile.FromUrl(benchStorage.GetBlobSASUri(blob), Path.Combine(refBenchFolder, shortName)));
                    }
                }

                job.Constraints = new JobConstraints();

                job.Constraints.MaxTaskRetryCount = MaxTaskRetryCount;

                AddStarterTask(id, tempBlobName, job, true, newBenchmarkContainerUri);

                bool failedToCommit = false;
                int tryBackAwayMultiplier = 1;
                int tryNo = 0;
                do
                {
                    try
                    {
                        failedToCommit = false;
                        await job.CommitAsync();
                    }
                    catch (BatchException batchExc) when (batchExc.RequestInformation != null && batchExc.RequestInformation.HttpStatusCode.HasValue && batchExc.RequestInformation.HttpStatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        if (tryNo == 7)//arbitrarily picked constant
                            throw;

                        ++tryNo;
                        failedToCommit = true;
                        await Task.Run(() => System.Threading.Thread.Sleep(tryBackAwayMultiplier * 500));
                        tryBackAwayMultiplier = tryBackAwayMultiplier * 2;
                    }
                }
                while (failedToCommit);
            }
        }

        private string BuildJobId(int experimentId)
        {
            return this.storage.StorageName + "_exp" + experimentId.ToString();
        }

        public override async Task UpdateNote(int id, string note)
        {
            await storage.UpdateNote(id, note);
        }

        public override async Task UpdateStatusFlag(ExperimentID id, bool flag)
        {
            await storage.UpdateStatusFlag(id, flag);
        }
    }

    public sealed class PoolDescription
    {
        public string Id { get; set; }

        public AllocationState? AllocationState { get; set; }

        public PoolState? PoolState { get; set; }

        public string VirtualMachineSize { get; set; }

        public int DedicatedNodes { get; set; }

        public int RunningJobs { get; set; }

        public string AutoScaleFormula { get; set; }
    }
}
