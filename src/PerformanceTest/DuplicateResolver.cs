using Measurement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public static class DuplicateResolver
    {
        /// <summary>
        /// If the returned results are removed from the given results then there is only one benchmark result for each of the file names.
        /// </summary>
        /// <returns>An array of results that are duplicates to be removed.
        /// Returns null, if operation was cancelled.</returns>
        public static BenchmarkResult[] Resolve(BenchmarkResult[] benchmarks, bool resolveTimeouts, bool resolveSameTime, bool resolveSlowest, bool resolveInErrors, Func<BenchmarkResult[], BenchmarkResult> choose)
        {
            if (benchmarks == null) throw new ArgumentNullException(nameof(benchmarks));
            if (choose == null) throw new ArgumentNullException(nameof(choose));

            if (benchmarks.Length <= 1) return new BenchmarkResult[0];

            // Queue contains duplicates grouped by filenames such that:
            // [1] All results within one queue element have same file name.
            // [2] Results of different queue elements have different file names.
            // [3] There are at least two items in each queue element.
            var duplicates = new Queue<List<BenchmarkResult>>();

            string prevFilename = benchmarks[0].BenchmarkFileName;
            bool isNew = true;
            List<BenchmarkResult> dups = null;

            for (int i = 1; i < benchmarks.Length; i++)
            {
                string nextFilename = benchmarks[i].BenchmarkFileName;
                if (nextFilename == prevFilename) // duplicate file name found
                {
                    if (isNew) // new duplicate
                    {
                        dups = new List<BenchmarkResult>();
                        duplicates.Enqueue(dups);
                        isNew = false;

                        dups.Add(benchmarks[i - 1]);
                    }
                    dups.Add(benchmarks[i]);
                }
                else // another file
                {
                    prevFilename = nextFilename;
                    isNew = true;
                    dups = null;
                }
            }

            List<BenchmarkResult> toRemove = new List<BenchmarkResult>();

            // Resolving duplicates
            while (duplicates.Count > 0)
            {
                var dupl = duplicates.Dequeue();
                BenchmarkResult pickItem = ResolveDuplicate(dupl.ToArray(), resolveTimeouts, resolveSameTime, resolveSlowest, resolveInErrors, choose);
                if (pickItem == null) return null; // consider this as cancellation

                foreach (var item in dupl)
                {
                    if (item != pickItem) toRemove.Add(item);
                }
            }

            return toRemove.ToArray();
        }

        /// <summary>
        /// Duplicates has at least two benchmark results.
        /// Returns a benchmark result that must be picked.
        /// Returns null, if cancelled.
        /// </summary>
        private static BenchmarkResult ResolveDuplicate(BenchmarkResult[] duplicates, bool resolveTimeouts, bool resolveSameTime, bool resolveSlowest, bool resolveInErrors, Func<BenchmarkResult[], BenchmarkResult> choose)
        {
            // Resolving manually.
            if (!resolveTimeouts && !resolveSameTime && !resolveSlowest && !resolveInErrors)
            {
                return choose(duplicates);
            }

            // Tries to resolve automatically using given rules.
            bool first = true;
            bool all_timeouts = true;
            bool all_ok = true;
            bool all_times_same = true;
            bool all_memouts = true;
            bool all_inerrors = true;
            double runtime = 0.0;
            double min_time = double.MaxValue;
            BenchmarkResult min_item = null;
            double max_time = double.MinValue;
            BenchmarkResult max_item = null;

            foreach (BenchmarkResult r in duplicates)
            {
                ResultStatus status = r.Status;
                double time = r.NormalizedRuntime;
                if (status != ResultStatus.Timeout && status != ResultStatus.InfrastructureError) { all_timeouts = false; }
                if (status != ResultStatus.Success && status != ResultStatus.InfrastructureError) { all_ok = false; }
                if (status != ResultStatus.OutOfMemory && status != ResultStatus.InfrastructureError) { all_memouts = false; }
                if (status != ResultStatus.InfrastructureError) { all_inerrors = false; }
                if (time < min_time)
                {
                    min_time = time;
                    min_item = r;
                }

                if (time > max_time)
                {
                    max_time = time;
                    max_item = r;
                }

                if (first)
                {
                    first = false; runtime = time;
                }
                else
                {
                    if (time != runtime) all_times_same = false;
                }
            }

            var benchmarks = duplicates;

            // remove in. errors duplicates if other exist
            if (resolveInErrors && !all_inerrors)
            {
                List<BenchmarkResult> notInErrors = new List<BenchmarkResult>();
                for (int i = benchmarks.Length; --i >= 0;)
                {
                    if (benchmarks[i].Status != ResultStatus.InfrastructureError)
                        notInErrors.Add(benchmarks[i]);
                }
                benchmarks = notInErrors.ToArray();
            }

            if (resolveInErrors && (all_inerrors || all_timeouts || all_ok && all_times_same))
            {
                return benchmarks[0];
            }
            else if (resolveSlowest && (all_ok || all_memouts))
            {
                return max_item;
            }
            else // manual resolution
            {
                return choose(benchmarks);
            }
        }
    }

}
