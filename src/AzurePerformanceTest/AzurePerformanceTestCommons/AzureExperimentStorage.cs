using Measurement;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


using ExperimentID = System.Int32;

namespace AzurePerformanceTest
{
    public partial class AzureExperimentStorage
    {
        // Storage account
        private CloudStorageAccount storageAccount;
        private CloudBlobClient blobClient;
        private CloudBlobContainer binContainer;
        private CloudBlobContainer resultsContainer;
        private CloudBlobContainer summaryContainer;
        private CloudBlobContainer outputContainer;
        private CloudBlobContainer configContainer;
        private CloudBlobContainer tempContainer;
        private CloudTableClient tableClient;
        private CloudTable experimentsTable;
        private CloudTable resultsTable;
        private CloudQueueClient queueClient;

        private readonly IRetryPolicy retryPolicy = new ExponentialRetry(TimeSpan.FromMilliseconds(250), 7);


        public const string KeyCreator = "creator";
        public const string KeyFileName = "fileName";

        private const string resultsContainerName = "results";
        private const string summaryContainerName = "summary";
        private const string binContainerName = "bin";
        private const string outputContainerName = "output";
        private const string configContainerName = "config";
        private const string tempContainerName = "temp";
        private const string experimentsTableName = "experiments";
        private const string resultsTableName = "data";


        public AzureExperimentStorage(string storageAccountName, string storageAccountKey) : this(String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", storageAccountName, storageAccountKey))
        {
        }

        public AzureExperimentStorage(string storageConnectionString)
        {
            var cs = new StorageAccountConnectionString(storageConnectionString);
            var tokenCredential = new TokenCredential("https://storage.azure.com");
            var storageCredential = new StorageCredentials(tokenCredential);
            storageAccount = new CloudStorageAccount(storageCredential, cs.AccountName, "core.windows.net", true);

            /// storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            blobClient = storageAccount.CreateCloudBlobClient();
            binContainer = blobClient.GetContainerReference(binContainerName);
            outputContainer = blobClient.GetContainerReference(outputContainerName);
            configContainer = blobClient.GetContainerReference(configContainerName);
            resultsContainer = blobClient.GetContainerReference(resultsContainerName);
            tempContainer = blobClient.GetContainerReference(tempContainerName);
            summaryContainer = blobClient.GetContainerReference(summaryContainerName);

            tableClient = storageAccount.CreateCloudTableClient();
            experimentsTable = tableClient.GetTableReference(experimentsTableName);
            resultsTable = tableClient.GetTableReference(resultsTableName);

            queueClient = storageAccount.CreateCloudQueueClient();

            var cloudEntityCreationTasks = new Task[] {
                binContainer.CreateIfNotExistsAsync(),
                outputContainer.CreateIfNotExistsAsync(),
                configContainer.CreateIfNotExistsAsync(),
                resultsTable.CreateIfNotExistsAsync(),
                experimentsTable.CreateIfNotExistsAsync(),
                resultsContainer.CreateIfNotExistsAsync(),
                tempContainer.CreateIfNotExistsAsync(),
                summaryContainer.CreateIfNotExistsAsync()
            };
            Task.WaitAll(cloudEntityCreationTasks);

            DefaultBenchmarkStorage = new AzureBenchmarkStorage(storageConnectionString, AzureBenchmarkStorage.DefaultContainerName);
        }

        public AzureBenchmarkStorage DefaultBenchmarkStorage { get; private set; }

        public CloudBlobContainer TempBlobContainer { get { return tempContainer; } }

        public string StorageName { get { return storageAccount.Credentials.AccountName; } }


        public IEnumerable<CloudBlockBlob> ListAzureWorkerBlobs()
        {
            return configContainer.ListBlobs().Select(listItem => listItem as CloudBlockBlob);
        }

        public async Task SaveReferenceExperiment(ReferenceExperiment reference)
        {
            string json = JsonConvert.SerializeObject(reference, Formatting.Indented);
            var blob = configContainer.GetBlockBlobReference("reference.json");
            await blob.UploadTextAsync(json);
        }

        public async Task<ReferenceExperiment> GetReferenceExperiment()
        {
            var blob = configContainer.GetBlockBlobReference("reference.json");
            try
            {
                string content = await blob.DownloadTextAsync();
                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.ContractResolver = new PrivatePropertiesResolver();
                ReferenceExperiment reference = JsonConvert.DeserializeObject<ReferenceExperiment>(content, settings);
                return reference;
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == 404) // Not found
                {
                    return null;
                }
                throw;
            }
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

        public async Task<Dictionary<ExperimentID, ExperimentEntity>> GetExperiments(ExperimentManager.ExperimentFilter? filter = default(ExperimentManager.ExperimentFilter?))
        {
            var dict = new Dictionary<ExperimentID, ExperimentEntity>();
            TableQuery<ExperimentEntity> query = new TableQuery<ExperimentEntity>();
            List<string> experimentFilters = new List<string>();
            experimentFilters.Add(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, ExperimentEntity.PartitionKeyDefault));

            if (filter.HasValue)
            {
                if (filter.Value.BenchmarkContainerEquals != null)
                    experimentFilters.Add(TableQuery.GenerateFilterCondition("BenchmarkContainer", QueryComparisons.Equal, filter.Value.BenchmarkContainerEquals));
                if (filter.Value.CategoryEquals != null)
                    experimentFilters.Add(TableQuery.GenerateFilterCondition("Category", QueryComparisons.Equal, filter.Value.CategoryEquals));
                if (filter.Value.ExecutableEquals != null)
                    experimentFilters.Add(TableQuery.GenerateFilterCondition("Executable", QueryComparisons.Equal, filter.Value.ExecutableEquals));
                if (filter.Value.ParametersEquals != null)
                    experimentFilters.Add(TableQuery.GenerateFilterCondition("Parameters", QueryComparisons.Equal, filter.Value.ParametersEquals));
                if (filter.Value.NotesEquals != null)
                    experimentFilters.Add(TableQuery.GenerateFilterCondition("Note", QueryComparisons.Equal, filter.Value.NotesEquals));
                if (filter.Value.CreatorEquals != null)
                    experimentFilters.Add(TableQuery.GenerateFilterCondition("Creator", QueryComparisons.Equal, filter.Value.CreatorEquals));
            }

            if (experimentFilters.Count > 0)
            {
                string finalFilter = experimentFilters[0];
                for (int i = 1; i < experimentFilters.Count; ++i)
                    finalFilter = TableQuery.CombineFilters(finalFilter, TableOperators.And, experimentFilters[i]);

                query = query.Where(finalFilter);
            }

            TableContinuationToken continuationToken = null;

            do
            {
                TableQuerySegment<ExperimentEntity> tableQueryResult =
                    await experimentsTable.ExecuteQuerySegmentedAsync(query, continuationToken);

                continuationToken = tableQueryResult.ContinuationToken;
                foreach (var e in tableQueryResult.Results)
                    dict.Add(int.Parse(e.RowKey, System.Globalization.CultureInfo.InvariantCulture), e);
            } while (continuationToken != null);

            return dict;
        }

        public CloudBlockBlob GetExecutableReference(string name)
        {
            return binContainer.GetBlockBlobReference(name);
        }

        public async Task<IReadOnlyDictionary<string, string>> GetExecutableMetadata(string name)
        {
            var blob = binContainer.GetBlobReference(name);
            await blob.FetchAttributesAsync(AccessCondition.GenerateEmptyCondition(), new BlobRequestOptions { RetryPolicy = retryPolicy }, null);
            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(blob.Metadata);
        }

        public string GetExecutableSasUri(string name)
        {
            var blob = binContainer.GetBlobReference(name);
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(48),
                Permissions = SharedAccessBlobPermissions.Read
            };
            string signature = blob.GetSharedAccessSignature(sasConstraints);
            return blob.Uri + signature;
        }

        /// <summary>
        /// Returns the uploaded blob name.
        /// </summary>
        public async Task<string> UploadNewExecutable(Stream source, string fileName, string creator)
        {
            if (!source.CanSeek) throw new ArgumentException("Source stream must allow seeking", nameof(source));

            string fileNameNoExt = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string esc_creator = AzureUtils.ToBinaryPackBlobName(creator);
            string packageName;

            const string packageNameFormat = "{0}.{1}.{2:yyyy-MM-ddTHH-mm-ss-ffff}{3}";

            do
            {
                source.Position = 0;
                packageName = string.Format(packageNameFormat, esc_creator, fileNameNoExt, DateTime.UtcNow, extension);
            } while (!await TryUploadNewExecutableAsBlob(source, packageName, creator, fileName));

            return packageName;
        }

        /// <summary>
        /// If the blob was successfully uploaded, returns true.
        /// If the blob already exists, returns false.
        /// Otherwise throws an exception.
        /// </summary>
        private async Task<bool> TryUploadNewExecutableAsBlob(Stream source, string blobName, string creator, string originalFileName)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (blobName == null) throw new ArgumentNullException("blobName");

            CloudBlockBlob blob = binContainer.GetBlockBlobReference(blobName);
            try
            {
                await blob.UploadFromStreamAsync(source, AccessCondition.GenerateIfNotExistsCondition(),
                    new BlobRequestOptions() { RetryPolicy = retryPolicy }, null);

                blob.Metadata.Add(KeyCreator, StripNonAscii(creator));
                blob.Metadata.Add(KeyFileName, StripNonAscii(originalFileName));
                await blob.SetMetadataAsync(AccessCondition.GenerateEmptyCondition(), new BlobRequestOptions { RetryPolicy = retryPolicy }, null);

                return true;
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict)
            {
                return false;
            }
        }

        public async Task<Tuple<string, DateTimeOffset?>> TryFindRecentExecutableBlob(string creator)
        {
            string prefix = AzureUtils.ToBinaryPackBlobName(creator);
            string asciiCreator = StripNonAscii(creator);

            BlobContinuationToken token = null;
            List<CloudBlob> best = new List<CloudBlob>();
            do
            {
                var segment = await binContainer.ListBlobsSegmentedAsync(prefix, true, BlobListingDetails.Metadata, null,
                    token, new BlobRequestOptions { RetryPolicy = retryPolicy }, null);

                CloudBlob bestBlob = null;
                foreach (var item in segment.Results)
                {
                    CloudBlob blob = item as CloudBlob;
                    if (blob == null) continue;

                    string blobCreator;
                    if (!blob.Metadata.TryGetValue(KeyCreator, out blobCreator) || blobCreator != asciiCreator) continue;


                    if (bestBlob == null || bestBlob.Properties.LastModified < blob.Properties.LastModified)
                        bestBlob = blob;
                }

                if (bestBlob != null) best.Add(bestBlob);
                token = segment.ContinuationToken;
            } while (token != null);

            if (best.Count == 0) return null;
            CloudBlob resultBlob = best[0];
            for (int i = 1; i < best.Count; i++)
            {
                var blob = best[i];
                if (resultBlob.Properties.LastModified < blob.Properties.LastModified)
                    resultBlob = blob;
            }
            return Tuple.Create(resultBlob.Name, resultBlob.Properties.LastModified);
        }

        private static string StripNonAscii(string s)
        {
            if (s == null) return null;
            string s2 = Regex.Replace(s, @"[^\u0000-\u007F]+", string.Empty);
            return s2;
        }

        public async Task<bool> DeleteExecutable(string executableName)
        {
            if (executableName == null) throw new ArgumentNullException("executableName");
            CloudBlockBlob blob = binContainer.GetBlockBlobReference(executableName);
            try
            {
                return await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, AccessCondition.GenerateEmptyCondition(),
                    new BlobRequestOptions() { RetryPolicy = retryPolicy }, null);
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict)
            {
                return false;
            }
        }

        private static string GetResultsFileName(int expId)
        {
            return String.Format("{0}.csv", expId);
        }

        private static string GetResultBlobName(ExperimentID expId)
        {
            return String.Format("{0}.csv.zip", expId);
        }

        private Measure.LimitsStatus StatusFromString(string status)
        {
            return (Measure.LimitsStatus)Enum.Parse(typeof(Measure.LimitsStatus), status);
        }

        /// <summary>
        /// Adds new entry to the experiments table
        /// </summary>
        /// <param name="experiment"></param>
        /// <param name="submitted"></param>
        /// <param name="creator"></param>
        /// <param name="note"></param>
        /// <returns>ID of newly created entry</returns>
        public async Task<int> AddExperiment(ExperimentDefinition experiment, DateTime submitted, string creator, string note, string workerInformation)
        {
            TableQuery<NextExperimentIDEntity> idEntityQuery = QueryForNextId();

            bool idChanged;
            int id = -1;

            do
            {
                idChanged = false;

                var list = (await experimentsTable.ExecuteQuerySegmentedAsync(idEntityQuery, null)).ToList();

                NextExperimentIDEntity nextId = null;

                if (list.Count == 0)
                {
                    nextId = new NextExperimentIDEntity();
                    id = 1;
                    nextId.Id = 2;

                    idChanged = !(await TryInsertTableEntity(experimentsTable, nextId));
                }
                else
                {
                    nextId = list[0];
                    id = nextId.Id;
                    nextId.Id = id + 1;

                    idChanged = !(await TryUpdateTableEntity(experimentsTable, nextId));
                }
            } while (idChanged);

            var row = new ExperimentEntity(id);
            row.Submitted = submitted;
            row.Executable = experiment.Executable;
            row.DomainName = experiment.DomainName;
            row.Parameters = experiment.Parameters;
            row.BenchmarkContainerUri = experiment.BenchmarkContainerUri;
            row.BenchmarkDirectory = experiment.BenchmarkDirectory;
            row.BenchmarkFileExtension = experiment.BenchmarkFileExtension;
            row.Category = experiment.Category;
            row.BenchmarkTimeout = experiment.BenchmarkTimeout.TotalSeconds;
            row.ExperimentTimeout = experiment.ExperimentTimeout.TotalSeconds;
            row.MemoryLimitMB = experiment.MemoryLimitMB;
            row.GroupName = experiment.GroupName;
            row.Note = note;
            row.Creator = creator;
            row.WorkerInformation = workerInformation;
            row.AdaptiveRunMaxRepetitions = experiment.AdaptiveRunMaxRepetitions;
            row.AdaptiveRunMaxTimeInSeconds = experiment.AdaptiveRunMaxTimeInSeconds;

            TableOperation insertOperation = TableOperation.Insert(row);
            await experimentsTable.ExecuteAsync(insertOperation);
            return id;
        }

        /// <summary>
        /// Deletes experiments table entry, output blobs, results blob.
        /// </summary>
        public async Task DeleteExperiment(ExperimentID id)
        {
            // Removes the output blobs
            var removeOutputs = Task.Run(async () =>
            {
                string prefix = BlobNamePrefix(id);
                BlobContinuationToken token = null;

                do
                {
                    var segment = await outputContainer.ListBlobsSegmentedAsync(prefix, true, BlobListingDetails.None, null,
                        token, new BlobRequestOptions { RetryPolicy = retryPolicy }, null);

                    segment.Results
                        .AsParallel()
                        .ForAll(r =>
                        {
                            CloudBlob blob = r as CloudBlob;
                            if (blob != null)
                            {
                                blob.DeleteIfExists(DeleteSnapshotsOption.IncludeSnapshots, options: new BlobRequestOptions { RetryPolicy = retryPolicy });
                            }
                        });

                    token = segment.ContinuationToken;
                } while (token != null);
            });

            // Removes the results table blob
            var removeResults = Task.Run(async () =>
            {
                string resultsBlobName = GetResultBlobName(id);
                var resultsBlob = resultsContainer.GetBlobReference(resultsBlobName);
                await resultsBlob.DeleteIfExistsAsync(
                      DeleteSnapshotsOption.IncludeSnapshots, AccessCondition.GenerateEmptyCondition(),
                      new BlobRequestOptions { RetryPolicy = retryPolicy }, null);
            });


            // Removes the row from the experiments table
            var removeEntity = Task.Run(async () =>
            {
                var exp = await GetExperiment(id);
                TableOperation deleteOperation = TableOperation.Delete(exp);
                await experimentsTable.ExecuteAsync(deleteOperation, new TableRequestOptions { RetryPolicy = retryPolicy }, null);
            });

            await Task.WhenAll(removeOutputs, removeResults, removeEntity);
        }

        private static TableQuery<NextExperimentIDEntity> QueryForNextId()
        {
            string idEntityFilter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, NextExperimentIDEntity.NextIDPartition),
                TableOperators.And,
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, NextExperimentIDEntity.NextIDRow));
            TableQuery<NextExperimentIDEntity> idEntityQuery = new TableQuery<NextExperimentIDEntity>().Where(idEntityFilter);
            return idEntityQuery;
        }

        public async Task<ExperimentEntity> GetExperiment(ExperimentID id)
        {
            TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

            return await FirstExperimentInQuery(query);
        }

        public async Task UpdateNote(int id, string note)
        {
            TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

            bool changed = false;
            do
            {
                ExperimentEntity experiment = await FirstExperimentInQuery(query);
                experiment.Note = note;

                changed = !(await TryUpdateTableEntity(experimentsTable, experiment));
            } while (changed);
        }

        public async Task SetBenchmarksTotal(int id, int total)
        {
            TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

            bool changed = false;
            do
            {
                ExperimentEntity experiment = await FirstExperimentInQuery(query);
                experiment.TotalBenchmarks = total;

                changed = !(await TryUpdateTableEntity(experimentsTable, experiment));
            } while (changed);
        }

        public async Task SetBenchmarksDone(int id, int done)
        {
            TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

            bool changed = false;
            do
            {
                ExperimentEntity experiment = await FirstExperimentInQuery(query);
                experiment.CompletedBenchmarks = done;

                changed = !(await TryUpdateTableEntity(experimentsTable, experiment));
            } while (changed);
        }

        public async Task SetTotalRuntime(int id, double totalRuntime)
        {
            TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

            bool changed = false;
            do
            {
                ExperimentEntity experiment = await FirstExperimentInQuery(query);
                experiment.TotalRuntime = totalRuntime;

                changed = !(await TryUpdateTableEntity(experimentsTable, experiment));
            } while (changed);
        }

        //public async Task IncreaseCompletedBenchmarks(int id, int completedBenchmarksRaise)
        //{
        //    TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

        //    bool changed = false;
        //    do
        //    {
        //        ExperimentEntity experiment = await FirstExperimentInQuery(query);
        //        experiment.CompletedBenchmarks += completedBenchmarksRaise;

        //        changed = !(await TryUpdateTableEntity(experimentsTable, experiment));
        //    } while (changed);
        //}

        public async Task UpdateStatusFlag(ExperimentID id, bool flag)
        {
            TableQuery<ExperimentEntity> query = ExperimentPointQuery(id);

            bool changed = false;
            do
            {
                ExperimentEntity experiment = await FirstExperimentInQuery(query);
                experiment.Flag = flag;

                changed = !(await TryUpdateTableEntity(experimentsTable, experiment));
            } while (changed);
        }

        private async Task<ExperimentEntity> FirstExperimentInQuery(TableQuery<ExperimentEntity> query)
        {
            var list = (await experimentsTable.ExecuteQuerySegmentedAsync(query, null, new TableRequestOptions { RetryPolicy = retryPolicy }, null)).ToList();

            if (list.Count == 0)
                throw new ArgumentException("Experiment with given ID not found");

            var experiment = list[0];
            return experiment;
        }

        private static TableQuery<ExperimentEntity> ExperimentPointQuery(int id)
        {
            string experimentEntityFilter = TableQuery.CombineFilters(
                               TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, ExperimentEntity.PartitionKeyDefault),
                               TableOperators.And,
                               TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, ExperimentEntity.ExperimentIDToString(id)));
            TableQuery<ExperimentEntity> query = new TableQuery<ExperimentEntity>().Where(experimentEntityFilter);
            return query;
        }

        private static async Task<bool> TryInsertTableEntity(CloudTable table, ITableEntity entity)
        {
            try
            {
                await table.ExecuteAsync(TableOperation.Insert(entity));
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == 409) // The specified entity already exists.
                {
                    //Someone inserted entity before us
                    return false;
                }
                else
                {
                    throw;
                }
            }
            return true;
        }

        private static async Task<bool> TryUpdateTableEntity(CloudTable table, ITableEntity entity)
        {
            try
            {
                await table.ExecuteAsync(TableOperation.InsertOrReplace(entity), null, new OperationContext { UserHeaders = new Dictionary<string, string> { { "If-Match", entity.ETag } } });
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode == 412) // The update condition specified in the request was not satisfied.
                {
                    //Someone modified entity before us
                    return false;
                }
                else
                {
                    throw;
                }
            }
            return true;
        }

        private static async Task ReplaceTableEntities(CloudTable table, IEnumerable<ITableEntity> entities)
        {
            await Task.WhenAll(
                Group(entities, azureStorageBatchSize)
                .Select(batch =>
                {
                    TableBatchOperation opsBatch = new TableBatchOperation();
                    foreach (var item in batch)
                        opsBatch.Replace(item);
                    return table.ExecuteBatchAsync(opsBatch);
                }));
        }
        public Stream DownloadExecutable(string exBlobName)
        {
            var blob = binContainer.GetBlobReference(exBlobName);
            return new LazyBlobStream(blob);
        }
    }

    public class LazyBlobStream : Stream
    {
        private CloudBlob blob;
        private Stream stream = null;
        public LazyBlobStream(CloudBlob blob)
        {
            this.blob = blob;
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                if (stream == null) return 0;
                else return stream.Position;
            }

            set
            {
                if (value != 0) throw new NotSupportedException("Cannot seek to custom position");
                if (stream != null)
                {
                    stream.Dispose();
                    stream = null;
                }
            }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (stream == null)
                stream = blob.OpenRead();

            return stream.Read(buffer, offset, count);
        }

        bool disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                if (stream != null)
                {
                    stream.Dispose();
                    stream = null;
                }
            }

            disposed = true;

            base.Dispose(disposing);
        }
    }

    public class ExperimentEntity : TableEntity
    {
        public static string ExperimentIDToString(ExperimentID id)
        {
            return id.ToString();//.PadLeft(6, '0');
        }
        public const string PartitionKeyDefault = "default";

        public ExperimentEntity(string id)
        {
            this.PartitionKey = PartitionKeyDefault;
            this.RowKey = id;
        }
        public ExperimentEntity(int id)
        {
            this.PartitionKey = PartitionKeyDefault;
            this.RowKey = ExperimentIDToString(id);
        }
        public ExperimentEntity() { }
        public DateTime Submitted { get; set; }
        public string Executable { get; set; }
        public string DomainName { get; set; }
        public string Parameters { get; set; }
        public string BenchmarkContainerUri { get; set; }
        public string BenchmarkDirectory { get; set; }
        public string Category { get; set; }
        public string BenchmarkFileExtension { get; set; }
        /// <summary>
        /// MegaBytes.
        /// </summary>
        public double MemoryLimitMB { get; set; }
        /// <summary>
        /// Seconds.
        /// </summary>
        public double BenchmarkTimeout { get; set; }
        /// <summary>
        /// Seconds.
        /// </summary>
        public double ExperimentTimeout { get; set; }

        /// <summary>
        /// Maximum number of repetitions of short benchmarks (1 for turning adaptivity off)
        /// </summary>
        public int AdaptiveRunMaxRepetitions { get; set; }

        /// <summary>
        /// Maximum total duration of adaptive runs in seconds
        /// </summary>
        public double AdaptiveRunMaxTimeInSeconds { get; set; }
        public string Note { get; set; }
        public string Creator { get; set; }
        public string WorkerInformation { get; set; }
        public bool Flag { get; set; }
        public string GroupName { get; set; }
        public int TotalBenchmarks { get; set; }
        public int CompletedBenchmarks { get; set; }
        /// <summary>
        /// Seconds.
        /// </summary>
        public double TotalRuntime { get; set; }
    }

    public class NextExperimentIDEntity : TableEntity
    {
        public int Id { get; set; }
        public const string NextIDPartition = "NextIDPartition";
        public const string NextIDRow = "NextIDRow";

        public NextExperimentIDEntity()
        {
            this.PartitionKey = NextIDPartition;
            this.RowKey = NextIDRow;
        }
    }
}
