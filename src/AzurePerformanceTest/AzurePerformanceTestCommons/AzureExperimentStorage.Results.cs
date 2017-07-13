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


        public async Task<AzureExperimentResults> GetResults(ExperimentID experimentId)
        {
            var result = await GetAzureExperimentResults(experimentId);
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
                string stdoutBlobId;
                do
                {
                    ++i;
                    stdoutBlobId = BlobNameForStdOut(result.ExperimentID, result.BenchmarkFileName, i.ToString());
                }
                while (!await UploadBlobAsync(outputContainer, stdoutBlobId, result.StdOut, UploadBlobMode.CreateNew));

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
                string stderrBlobId;
                do
                {
                    ++i;
                    stderrBlobId = BlobNameForStdErr(result.ExperimentID, result.BenchmarkFileName, i.ToString());
                }
                while (!await UploadBlobAsync(outputContainer, stderrBlobId, result.StdErr, UploadBlobMode.CreateNew));

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
            azureResult.NormalizedRuntime = b.NormalizedRuntime;
            azureResult.PeakMemorySizeMB = b.PeakMemorySizeMB;
            azureResult.Properties = new Dictionary<string, string>();
            foreach (var prop in b.Properties)
                azureResult.Properties.Add(prop.Key, prop.Value);
            azureResult.Status = b.Status;

            azureResult.StdOut = string.Empty;
            azureResult.StdOutExtStorageIdx = string.Empty;

            azureResult.StdErr = string.Empty;
            azureResult.StdErrExtStorageIdx = string.Empty;

            azureResult.TotalProcessorTime = b.TotalProcessorTime;
            azureResult.WallClockTime = b.WallClockTime;

            return azureResult;
        }

        public BenchmarkResult ParseAzureBenchmarkResult(AzureBenchmarkResult azureResult)
        {
            return new BenchmarkResult(
                azureResult.ExperimentID,
                azureResult.BenchmarkFileName,
                azureResult.AcquireTime,
                azureResult.NormalizedRuntime,
                azureResult.TotalProcessorTime,
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

        private static async Task<bool> UploadBlobAsync(CloudBlobContainer container, string blobName, Stream content, UploadBlobMode mode, string etag = null)
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
                return true;
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
            {
                return false;
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
        /// <returns>True, if results have been uploaded.
        /// False, if the precondition failed and nothing was uploaded.</returns>
        public async Task<bool> PutAzureExperimentResults(int expId, AzureBenchmarkResult[] results, UploadBlobMode mode, string etag = null)
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

        public async Task<Tuple<AzureBenchmarkResult[], string>> GetAzureExperimentResults(ExperimentID experimentId)
        {
            AzureBenchmarkResult[] results;

            string blobName = GetResultBlobName(experimentId);
            var blob = resultsContainer.GetBlobReference(blobName);
            try
            {
                using (MemoryStream zipStream = new MemoryStream(4 << 20))
                {
                    await blob.DownloadToStreamAsync(zipStream,
                        AccessCondition.GenerateEmptyCondition(),
                        new Microsoft.WindowsAzure.Storage.Blob.BlobRequestOptions
                        {
                            RetryPolicy = new Microsoft.WindowsAzure.Storage.RetryPolicies.ExponentialRetry(TimeSpan.FromMilliseconds(100), 10)
                        }, null);

                    zipStream.Position = 0;
                    using (ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
                    {
                        var entry = zip.GetEntry(GetResultsFileName(experimentId));
                        using (var tableStream = entry.Open())
                        {
                            results = AzureBenchmarkResult.LoadBenchmarks(experimentId, tableStream);
                            return Tuple.Create(results, blob.Properties.ETag);
                        }
                    }
                }
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
