using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ExperimentID = System.Int32;


namespace AzurePerformanceTest
{
    public partial class AzureExperimentStorage
    {
        const int MaxStdOutLength = 4096;
        const int MaxStdErrLength = 4096;


        public async Task<AzureExperimentResults> GetResults(ExperimentID experimentId, ExperimentManager.BenchmarkFilter f = null)
        {
            var result = await GetAzureExperimentResults(experimentId, f);
            AzureBenchmarkResult[] azureResults = result.Item1;
            string etag = result.Item2;
            return new AzureExperimentResults(this, experimentId, azureResults, etag);
        }


        public Task PutResult(ExperimentID expId, BenchmarkResult result)
        {
            var queue = GetResultsQueueReference(expId);
            return PutResult(expId, result, queue, outputContainer);
        }

        public static async Task PutResult(ExperimentID expId, BenchmarkResult result, CloudQueue resultsQueue, CloudBlobContainer outputContainer)
        {
            var result2 = await PrepareBenchmarkResult(result, outputContainer);
            using (MemoryStream ms = new MemoryStream())
            {
                //ms.WriteByte(1);//signalling that this message contains a result
                (new BinaryFormatter()).Serialize(ms, result2);
                await resultsQueue.AddMessageAsync(new CloudQueueMessage(ms.ToArray()));
            }
        }

        public string GetOutputQueueSASUri(ExperimentID expId, TimeSpan lifetime)
        {
            var queue = GetResultsQueueReference(expId);
            SharedAccessQueuePolicy sasConstraints = new SharedAccessQueuePolicy
            {
                SharedAccessExpiryTime = DateTime.UtcNow + lifetime,
                Permissions = SharedAccessQueuePermissions.Add
            };
            string signature = queue.GetSharedAccessSignature(sasConstraints);
            return queue.Uri + signature;
        }

        public string GetOutputContainerSASUri(TimeSpan lifetime)
        {
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTime.UtcNow + lifetime,
                Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List | SharedAccessBlobPermissions.Create | SharedAccessBlobPermissions.Write
            };
            string signature = outputContainer.GetSharedAccessSignature(sasConstraints);
            return outputContainer.Uri + signature;
        }

        public async Task DeleteOutputs(AzureBenchmarkResult azureResult)
        {
            var stdoutBlobName = BlobNameForStdOut(azureResult.ExperimentID, azureResult.BenchmarkFileName, azureResult.StdOutExtStorageIdx);
            var stdoutBlob = outputContainer.GetBlockBlobReference(stdoutBlobName);

            var stderrBlobName = BlobNameForStdOut(azureResult.ExperimentID, azureResult.BenchmarkFileName, azureResult.StdErrExtStorageIdx);
            var stderrBlob = outputContainer.GetBlockBlobReference(stderrBlobName);

            await Task.WhenAll(
                stdoutBlob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, AccessCondition.GenerateEmptyCondition(), new BlobRequestOptions { RetryPolicy = retryPolicy }, null),
                stderrBlob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, AccessCondition.GenerateEmptyCondition(), new BlobRequestOptions { RetryPolicy = retryPolicy }, null));
        }

        private Task<AzureBenchmarkResult> PrepareBenchmarkResult(BenchmarkResult result)
        {
            return PrepareBenchmarkResult(result, outputContainer);
        }

        public static async Task<AzureBenchmarkResult> PrepareBenchmarkResult(BenchmarkResult result, CloudBlobContainer outputContainer)
        {
            AzureBenchmarkResult azureResult = ToAzureBenchmarkResult(result);

            if (result.StdOut.Length > MaxStdOutLength)
            {
                int i = -1;
                string etag;
                string stdoutBlobId;
                do
                {
                    ++i;
                    stdoutBlobId = BlobNameForStdOut(result.ExperimentID, result.BenchmarkFileName, i.ToString());
                    etag = await UploadBlobAsync(outputContainer, stdoutBlobId, result.StdOut, UploadBlobMode.CreateNew);
                } while (etag == null); // until we find blob name for which there is no existing blob

                Trace.WriteLine(string.Format("Uploaded stdout for experiment {0}", result.ExperimentID));
                azureResult.StdOut = null;
                azureResult.StdOutExtStorageIdx = i.ToString();
            }
            else
            {
                if (result.StdOut.Length > 0)
                {
                    long pos = 0;
                    if (result.StdOut.CanSeek)
                    {
                        pos = result.StdOut.Position;
                        result.StdOut.Seek(0, SeekOrigin.Begin);
                    }
                    using (StreamReader sr = new StreamReader(result.StdOut))
                    {
                        azureResult.StdOut = await sr.ReadToEndAsync();
                    }
                    if (result.StdOut.CanSeek)
                    {
                        result.StdOut.Seek(pos, SeekOrigin.Begin);
                    }
                }
                else
                    azureResult.StdOut = "";
                azureResult.StdOutExtStorageIdx = "";
            }
            if (result.StdErr.Length > MaxStdErrLength)
            {
                int i = -1;
                string etag;
                string stderrBlobId;
                do
                {
                    ++i;
                    stderrBlobId = BlobNameForStdErr(result.ExperimentID, result.BenchmarkFileName, i.ToString());
                    etag = await UploadBlobAsync(outputContainer, stderrBlobId, result.StdErr, UploadBlobMode.CreateNew);
                }
                while (etag == null);

                Trace.WriteLine(string.Format("Uploaded stderr for experiment {0}", result.ExperimentID));
                azureResult.StdErr = null;
                azureResult.StdErrExtStorageIdx = i.ToString();
            }
            else
            {
                if (result.StdErr.Length > 0)
                {
                    long pos = 0;
                    if (result.StdErr.CanSeek)
                    {
                        pos = result.StdErr.Position;
                        result.StdErr.Seek(0, SeekOrigin.Begin);
                    }
                    using (StreamReader sr = new StreamReader(result.StdErr))
                    {
                        azureResult.StdErr = await sr.ReadToEndAsync();
                    }
                    if (result.StdErr.CanSeek)
                    {
                        result.StdErr.Seek(pos, SeekOrigin.Begin);
                    }
                }
                else
                    azureResult.StdErr = "";
                azureResult.StdErrExtStorageIdx = "";
            }

            return azureResult;
        }

        public static AzureBenchmarkResult ToAzureBenchmarkResult(BenchmarkResult b)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));

            AzureBenchmarkResult azureResult = new AzureBenchmarkResult();
            azureResult.AcquireTime = b.AcquireTime;
            azureResult.BenchmarkFileName = b.BenchmarkFileName;
            azureResult.ExitCode = b.ExitCode;
            azureResult.ExperimentID = b.ExperimentID;
            azureResult.NormalizedCPUTime = b.NormalizedCPUTime;
            azureResult.CPUTime = b.CPUTime;
            azureResult.WallClockTime = b.WallClockTime;
            azureResult.PeakMemorySizeMB = b.PeakMemorySizeMB;
            azureResult.Properties = new Dictionary<string, string>();
            foreach (var prop in b.Properties)
                azureResult.Properties.Add(prop.Key, prop.Value);
            azureResult.Status = b.Status;

            azureResult.StdOut = string.Empty;
            azureResult.StdOutExtStorageIdx = string.Empty;

            azureResult.StdErr = string.Empty;
            azureResult.StdErrExtStorageIdx = string.Empty;

            return azureResult;
        }

        public BenchmarkResult ParseAzureBenchmarkResult(AzureBenchmarkResult azureResult)
        {
            return new BenchmarkResult(
                azureResult.ExperimentID,
                azureResult.BenchmarkFileName,
                azureResult.AcquireTime,
                azureResult.NormalizedCPUTime,
                azureResult.CPUTime,
                azureResult.WallClockTime,
                azureResult.PeakMemorySizeMB,
                azureResult.Status,
                azureResult.ExitCode,
                string.IsNullOrEmpty(azureResult.StdOutExtStorageIdx) ? Utils.StringToStream(azureResult.StdOut) : new LazyBlobStream(outputContainer.GetBlobReference(BlobNameForStdOut(azureResult.ExperimentID, azureResult.BenchmarkFileName, azureResult.StdOutExtStorageIdx))),
                string.IsNullOrEmpty(azureResult.StdErrExtStorageIdx) ? Utils.StringToStream(azureResult.StdErr) : new LazyBlobStream(outputContainer.GetBlobReference(BlobNameForStdErr(azureResult.ExperimentID, azureResult.BenchmarkFileName, azureResult.StdErrExtStorageIdx))),
                new ReadOnlyDictionary<string, string>(azureResult.Properties)
                );
        }

        private static string BlobNamePrefix(int experimentID)
        {
            return String.Concat("E", experimentID.ToString(), "F");
        }

        private static string BlobNameForStdErr(int experimentID, string benchmarkFileName, string index)
        {
            return String.Concat(BlobNamePrefix(experimentID), benchmarkFileName, "-stderr", index);
        }

        private static string BlobNameForStdOut(int experimentID, string benchmarkFileName, string index)
        {
            return String.Concat(BlobNamePrefix(experimentID), benchmarkFileName, "-stdout", index);
        }

        /// <summary>
        /// If blob uploaded, returns its etag; otherwise, if precondition failed for the mode, returns null.
        /// </summary>
        private static async Task<string> UploadBlobAsync(CloudBlobContainer container, string blobName, Stream content, UploadBlobMode mode, string etag = null)
        {
            if (mode == UploadBlobMode.ReplaceExact && etag == null)
                throw new ArgumentException("Etag must be provided when using ReplaceExact mode");

            try
            {
                var stdoutBlob = container.GetBlockBlobReference(blobName);

                AccessCondition accessCondition;
                switch (mode)
                {
                    case UploadBlobMode.CreateNew:
                        accessCondition = AccessCondition.GenerateIfNotExistsCondition();
                        break;
                    case UploadBlobMode.CreateOrReplace:
                        accessCondition = AccessCondition.GenerateEmptyCondition();
                        break;
                    case UploadBlobMode.ReplaceExact:
                        accessCondition = AccessCondition.GenerateIfMatchCondition(etag);
                        break;
                    default:
                        throw new ArgumentException("Unknown mode");
                }

                await stdoutBlob.UploadFromStreamAsync(content,
                    accessCondition,
                    new BlobRequestOptions()
                    {
                        RetryPolicy = new Microsoft.WindowsAzure.Storage.RetryPolicies.ExponentialRetry(TimeSpan.FromMilliseconds(100), 14)
                    },
                    null);
                return stdoutBlob.Properties.ETag;
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
            {
                return null;
            }
            catch (StorageException ex)
            {
                Trace.WriteLine(string.Format("Failed to upload text to the blob: {0}, blob name: {1}, response code: {2}", ex.Message, blobName, ex.RequestInformation.HttpStatusCode));
                throw;
            }
        }

        public async Task<CloudQueue> CreateResultsQueue(ExperimentID id)
        {
            var reference = queueClient.GetQueueReference(QueueNameForExperiment(id));
            await reference.CreateIfNotExistsAsync();
            return reference;
        }

        public CloudQueue GetResultsQueueReference(ExperimentID id)
        {
            return queueClient.GetQueueReference(QueueNameForExperiment(id));
        }

        public async Task DeleteResultsQueue(ExperimentID id)
        {
            var reference = queueClient.GetQueueReference(QueueNameForExperiment(id));
            await reference.DeleteIfExistsAsync();
        }

        private string QueueNameForExperiment(ExperimentID id)
        {
            return "exp" + id.ToString();
        }

        /// <summary>
        /// Puts the benchmark results of the given experiment to the storage.
        /// </summary>
        /// <param name="results">All results must have same experiment id.
        /// <returns>Blob etag, if results have been uploaded.
        /// Null, if the precondition failed and nothing was uploaded.</returns>
        public async Task<string> PutAzureExperimentResults(int expId, AzureBenchmarkResult[] results, UploadBlobMode mode, string etag = null)
        {
            string fileName = GetResultsFileName(expId);
            using (MemoryStream zipStream = new MemoryStream())
            {
                using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    var entry = zip.CreateEntry(fileName);
                    AzureBenchmarkResult.SaveBenchmarks(results, entry.Open());
                }

                zipStream.Position = 0;
                return await UploadBlobAsync(resultsContainer, GetResultBlobName(expId), zipStream, mode, etag);
            }
        }

        protected AzureBenchmarkResult[] GetFromCache(ExperimentID id, CloudBlob blob, ExperimentManager.BenchmarkFilter f = null)
        {
            DateTime before = DateTime.Now;
            AzureBenchmarkResult[] res = null;

            try
            {
                blob.FetchAttributes();
                string dir = Path.Combine(Path.GetTempPath(), "z3nightly-results");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, GetResultsFileName(id));
                if (File.Exists(file) &&
                    blob.Properties.LastModified.HasValue &&
                    File.GetLastWriteTimeUtc(file) > blob.Properties.LastModified.Value)
                    using (var stream = new FileStream(file, FileMode.Open))
                    {
                        res = AzureBenchmarkResult.LoadBenchmarks(id, stream, f);
                        Debug.Print("Job #{0}: cache hit, load time: {1:n2} sec", id, (DateTime.Now - before).TotalSeconds);
                    }
            }
            catch (Exception ex)
            {
                Debug.Print("Exception caught while reading from cache: " + ex.Message);
                Debug.Print("Stack Trace: " + ex.StackTrace);
            }

            return res;
        }

        protected async Task<AzureBenchmarkResult[]> GetFromStorage(ExperimentID id, CloudBlob blob, ExperimentManager.BenchmarkFilter f = null)
        {
            using (MemoryStream zipStream = new MemoryStream(1 << 16))
            {
                await blob.DownloadToStreamAsync(zipStream,
                    AccessCondition.GenerateEmptyCondition(),
                    new Microsoft.WindowsAzure.Storage.Blob.BlobRequestOptions
                    {
                        RetryPolicy = new Microsoft.WindowsAzure.Storage.RetryPolicies.ExponentialRetry(TimeSpan.FromMilliseconds(100), 25)
                    }, null);

                AzureBenchmarkResult[] res = null;
                zipStream.Position = 0;
                using (ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    string rfn = GetResultsFileName(id);
                    var entry = zip.GetEntry(rfn);
                    res = AzureBenchmarkResult.LoadBenchmarks(id, entry.Open(), f);

                    DateTime before = DateTime.Now;
                    try
                    {
                        // If possible, save to cache.
                        string dir = Path.Combine(Path.GetTempPath(), "z3nightly-results");
                        Directory.CreateDirectory(dir);
                        string filename = Path.Combine(dir, rfn);

                        using (FileStream file = File.Open(filename, FileMode.OpenOrCreate, FileAccess.Write))
                        using (var e = entry.Open())
                            await e.CopyToAsync(file);
                    }
                    catch (Exception ex)
                    {
                        Debug.Print("Exception caught while saving to cache: {0}", ex.Message);
                        Debug.Print("Stack Trace: " + ex.StackTrace);
                    }
                    Debug.Print("Job #{0}: cache save time: {1:n2} sec", id, (DateTime.Now - before).TotalSeconds);
                }

                return res;
            }
        }

        public async Task<Tuple<AzureBenchmarkResult[], string>> GetAzureExperimentResults(ExperimentID experimentId, ExperimentManager.BenchmarkFilter f = null)
        {
            string blobName = GetResultBlobName(experimentId);
            CloudBlob blob = resultsContainer.GetBlobReference(blobName);
            try
            {
                AzureBenchmarkResult[] r = GetFromCache(experimentId, blob, f);
                r = r ?? await GetFromStorage(experimentId, blob, f);
                return Tuple.Create(r, blob.Properties.ETag);
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 404) // Not found == no results
            {
                return Tuple.Create(new AzureBenchmarkResult[0], (string)null);
            }
        }

        public enum UploadBlobMode
        {
            /// <summary>The blob must not exist and will be created.</summary>
            CreateNew,
            /// <summary>If the blob exists, it will be replaced; otherwise, it will be created.</summary>
            CreateOrReplace,
            /// <summary>The blob must exist and have certain etag.</summary>
            ReplaceExact
        }
    }
}
