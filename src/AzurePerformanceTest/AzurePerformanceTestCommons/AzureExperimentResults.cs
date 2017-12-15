using Measurement;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AzurePerformanceTest.AzureExperimentStorage;

namespace AzurePerformanceTest
{
    public class AzureExperimentResults : ExperimentResults
    {
        private readonly int expId;
        private readonly Dictionary<BenchmarkResult, AzureBenchmarkResult> externalOutputs;
        private readonly AzureExperimentStorage storage;
        /// <summary>Etag of the blob which contained the given result. Null means that the blob didn't exist and no results.</summary>
        private string etag;

        public AzureExperimentResults(AzureExperimentStorage storage, int expId, AzureBenchmarkResult[] results, string etag) : base(expId, Parse(results, storage))
        {
            this.expId = expId;
            this.storage = storage;
            this.etag = etag;

            var benchmarks = Benchmarks;
            externalOutputs = new Dictionary<BenchmarkResult, AzureBenchmarkResult>();
            for (int i = 0; i < results.Length; i++)
            {
                var r = results[i];
                if (!string.IsNullOrEmpty(r.StdOutExtStorageIdx) || !string.IsNullOrEmpty(r.StdErrExtStorageIdx))
                    externalOutputs.Add(benchmarks[i], r);
            }
        }

        public override async Task<bool> TryDelete(IEnumerable<BenchmarkResult> toRemove)
        {
            if (toRemove == null) throw new ArgumentNullException(nameof(toRemove));

            var benchmarks = Benchmarks;
            var removeSet = new HashSet<BenchmarkResult>(toRemove);
            if (removeSet.Count == 0) return true;

            int n = benchmarks.Length;
            List<AzureBenchmarkResult> newAzureResults = new List<AzureBenchmarkResult>(n);
            List<BenchmarkResult> newResults = new List<BenchmarkResult>(n);
            List<AzureBenchmarkResult> deleteOuts = new List<AzureBenchmarkResult>();
            for (int i = 0; i < n; i++)
            {
                var b = benchmarks[i];
                if (!removeSet.Contains(b)) // remains
                {
                    var azureResult = ToAzureResult(b, TryGetExternalOutput(b));
                    newAzureResults.Add(azureResult);
                    newResults.Add(b);
                }
                else // to be removed
                {
                    removeSet.Remove(b);

                    AzureBenchmarkResult ar;
                    if (externalOutputs.TryGetValue(b, out ar))
                    {
                        deleteOuts.Add(ar);
                    }
                }
            }
            if (removeSet.Count != 0) throw new ArgumentException("Some of the given results to remove do not belong to the experiment results");

            // Updating blob with results table
            string newEtag = await Upload(newAzureResults.ToArray());
            if (newEtag == null) return false;
            etag = newEtag;

            // Update benchmarks array
            Replace(newResults.ToArray());
            (await storage.GetExperiment(ExperimentId)).CompletedBenchmarks = Benchmarks.Count();

            // Deleting blobs with output
            foreach (var ar in deleteOuts)
            {
                try
                {
                    var _ = storage.DeleteOutputs(ar);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(string.Format("Exception when deleting output: {0}", ex));
                }
            }

            return true;
        }

        public override async Task<Dictionary<BenchmarkResult, BenchmarkResult>> TryUpdateStatus(IEnumerable<BenchmarkResult> toModify, ResultStatus status)
        {
            if (toModify == null) throw new ArgumentNullException(nameof(toModify));

            var mod = new Dictionary<BenchmarkResult, BenchmarkResult>();
            foreach (var oldRes in toModify) mod.Add(oldRes, null);
            if (mod.Count == 0) return mod;

            int n = Benchmarks.Length;
            var newBenchmarks = (BenchmarkResult[])Benchmarks.Clone();
            var newAzureBenchmarks = new AzureBenchmarkResult[n];

            for (int i = 0; i < n; i++)
            {
                var b = newBenchmarks[i];
                AzureBenchmarkResult azureResult;

                if (mod.ContainsKey(b))
                {
                    if (b.Status != status) // updating status of this result
                    {
                        newBenchmarks[i] = new BenchmarkResult(b.ExperimentID, b.BenchmarkFileName,
                            b.AcquireTime, b.NormalizedCPUTime, b.CPUTime, b.WallClockTime,
                            b.PeakMemorySizeMB,
                            status, // <-- new status
                            b.ExitCode, b.StdOut, b.StdErr, b.Properties);

                        azureResult = ToAzureResult(newBenchmarks[i], TryGetExternalOutput(b));
                        mod[b] = newBenchmarks[i];
                    }
                    else // status is as required already
                    {
                        azureResult = ToAzureResult(b, TryGetExternalOutput(b));
                        mod.Remove(b);
                    }
                }
                else // result doesn't change
                {
                    azureResult = ToAzureResult(b, TryGetExternalOutput(b));
                }

                newAzureBenchmarks[i] = azureResult;
            }

            if (mod.Count == 0) return new Dictionary<BenchmarkResult, BenchmarkResult>(); // no changes
            foreach (var item in mod)
                if (item.Value == null) throw new ArgumentException("Some of the given results to update do not belong to the experiment results");

            string newEtag = await Upload(newAzureBenchmarks);
            if (newEtag == null) return null;

            // Update benchmarks array
            etag = newEtag;
            Replace(newBenchmarks.ToArray());
            foreach (var item in externalOutputs.ToArray())
            {
                BenchmarkResult oldB = item.Key;
                BenchmarkResult newB;
                if (!mod.TryGetValue(oldB, out newB)) continue;

                AzureBenchmarkResult ar;
                if (externalOutputs.TryGetValue(oldB, out ar))
                {
                    externalOutputs.Remove(oldB);
                    externalOutputs.Add(newB, ar);
                }
            }

            return mod;
        }

        private static AzureBenchmarkResult ToAzureResult(BenchmarkResult b, AzureBenchmarkResult externalOutput)
        {
            AzureBenchmarkResult azureResult = AzureExperimentStorage.ToAzureBenchmarkResult(b);

            if (externalOutput != null)
            {
                azureResult.StdOut = externalOutput.StdOut;
                azureResult.StdOutExtStorageIdx = externalOutput.StdOutExtStorageIdx;

                azureResult.StdErr = externalOutput.StdErr;
                azureResult.StdErrExtStorageIdx = externalOutput.StdErrExtStorageIdx;
            }
            else
            {
                b.StdOut.Position = 0;
                azureResult.StdOut = Utils.StreamToString(b.StdOut, true);
                azureResult.StdOutExtStorageIdx = string.Empty;

                b.StdErr.Position = 0;
                azureResult.StdErr = Utils.StreamToString(b.StdErr, true);
                azureResult.StdErrExtStorageIdx = string.Empty;
            }

            return azureResult;
        }

        private AzureBenchmarkResult TryGetExternalOutput(BenchmarkResult b)
        {
            AzureBenchmarkResult azureResult;
            if (externalOutputs.TryGetValue(b, out azureResult)) return azureResult;
            return null;
        }

        /// <summary>
        /// If uploaded, returns etag for the results table.
        /// Otherwise, if precondition failed, returns null.
        /// </summary>
        private async Task<string> Upload(AzureBenchmarkResult[] newAzureBenchmarks)
        {
            string newEtag;
            if (etag != null) // blob already exists
                newEtag = await storage.PutAzureExperimentResults(expId, newAzureBenchmarks, UploadBlobMode.ReplaceExact, etag);
            else // blob didn't exist
                newEtag = await storage.PutAzureExperimentResults(expId, newAzureBenchmarks, UploadBlobMode.CreateNew);
            return newEtag;
        }

        private static BenchmarkResult[] Parse(AzureBenchmarkResult[] results, AzureExperimentStorage storage)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (results == null) throw new ArgumentNullException(nameof(results));

            int n = results.Length;
            BenchmarkResult[] benchmarks = new BenchmarkResult[n];
            for (int i = 0; i < n; i++)
            {
                benchmarks[i] = storage.ParseAzureBenchmarkResult(results[i]);
            }
            return benchmarks;
        }
    }
}
