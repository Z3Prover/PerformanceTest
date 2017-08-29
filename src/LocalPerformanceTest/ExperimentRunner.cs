using Measurement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using ExperimentID = System.Int32;

namespace PerformanceTest
{
    public class LocalExperimentRunner
    {
        private readonly LimitedConcurrencyLevelTaskScheduler scheduler;
        private readonly TaskFactory factory;
        private readonly string rootFolder;
        private readonly IDomainResolver domainResolver;

        public LocalExperimentRunner(string rootFolder, IDomainResolver domainResolver)
        {
            if (rootFolder == null) new ArgumentNullException("rootFolder");
            if (domainResolver == null) new ArgumentNullException("domainResolver");

            scheduler = new LimitedConcurrencyLevelTaskScheduler(1);
            factory = new TaskFactory(scheduler);
            this.rootFolder = rootFolder;
            this.domainResolver = domainResolver;
        }

        public TaskFactory TaskFactory { get { return factory; } }

        public Task<BenchmarkResult>[] Enqueue(ExperimentID id, ExperimentDefinition experiment, double normal, int repetitions = 0)
        {
            if (experiment == null) throw new ArgumentNullException("experiment");
            return RunExperiment(id, experiment, normal, repetitions);
        }

        private Task<BenchmarkResult>[] RunExperiment(ExperimentID id, ExperimentDefinition experiment, double normal, int repetitions = 0)
        {
            if (experiment == null) throw new ArgumentNullException("experiment");
            if (factory == null) throw new ArgumentNullException("factory");

            Domain domain = domainResolver.GetDomain(experiment.DomainName);

            string executable;
            if (Path.IsPathRooted(experiment.Executable)) executable = experiment.Executable;
            else executable = Path.Combine(rootFolder, experiment.Executable);
            if (!File.Exists(executable)) throw new ArgumentException("Executable not found");

            string benchmarkFolder = string.IsNullOrEmpty(experiment.Category) ? experiment.BenchmarkDirectory : Path.Combine(experiment.BenchmarkDirectory, experiment.Category);
            if (!Path.IsPathRooted(benchmarkFolder))
            {
                benchmarkFolder = Path.Combine(rootFolder, benchmarkFolder);
            }
            var benchmarks = Directory.GetFiles(benchmarkFolder, "*." + experiment.BenchmarkFileExtension, SearchOption.AllDirectories);

            var results = new List<Task<BenchmarkResult>>(256);
            foreach (string benchmarkFile in Directory.GetFiles(benchmarkFolder, "*." + experiment.BenchmarkFileExtension, SearchOption.AllDirectories))
            {
                var task =
                    factory.StartNew(_benchmark =>
                    {
                        string inputFullPath = (string)_benchmark;
                        string inputRelativePath = Utils.MakeRelativePath(benchmarkFolder, inputFullPath);
                        Trace.WriteLine("Running benchmark " + Path.GetFileName(inputRelativePath));

                        string args = experiment.Parameters;
                        return RunBenchmark(id, executable, experiment.Parameters, inputRelativePath, inputFullPath,
                            repetitions, experiment.BenchmarkTimeout, experiment.MemoryLimitMB, null, null, domain,
                            normal);
                    }, benchmarkFile, TaskCreationOptions.LongRunning);
                results.Add(task);
            }

            return results.ToArray();
        }

        public static BenchmarkResult RunBenchmark(int experimentId, string executable, string args, string inputDisplayName, string inputFullPath, int repetitions, TimeSpan timeOut, double memLimitMB, long? ouputLimit, long? errorLimit, Domain domain, double normal, int maxRepetitions = 10, double maxTimeInSeconds = 10)
        {
            if (domain == null) throw new ArgumentNullException("domain");
            if (args != null)
                args = args.Replace("{0}", inputFullPath);
            else
                args = "";

            DateTime acq = DateTime.Now;
            int maxCount = repetitions == 0 ? maxRepetitions : repetitions;
            TimeSpan maxTime = TimeSpan.FromSeconds(maxTimeInSeconds);

            int count = 0;
            List<ProcessRunMeasure> measures = new List<ProcessRunMeasure>();
            TimeSpan total = TimeSpan.FromSeconds(0);
            ProcessRunAnalysis analysis = null;
            ProcessRunMeasure m;
            do
            {
                m = ProcessMeasurer.Measure(executable, args, timeOut, memLimitMB, ouputLimit, errorLimit, domain);
                measures.Add(m);
                count++;
                total += m.WallClockTime;

                if (analysis == null) // analyzed only once, repetitions are for more confident run time
                {
                    analysis = domain.Analyze(inputFullPath, m);
                }
            } while ((repetitions != 0 || total < maxTime) && count < maxCount && m.Limits == Measure.LimitsStatus.WithinLimits);

            ProcessRunMeasure finalMeasure = Utils.AggregateMeasures(measures.ToArray());
            Trace.WriteLine(String.Format("Done in {0} (aggregated by {1} runs)", finalMeasure.WallClockTime, count));

            var performanceIndex = normal * finalMeasure.TotalProcessorTime.TotalSeconds;
            var result = new BenchmarkResult(
                experimentId, inputDisplayName,
                acq, performanceIndex,
                finalMeasure.TotalProcessorTime, finalMeasure.WallClockTime, finalMeasure.PeakMemorySizeMB,
                analysis.Status,
                finalMeasure.ExitCode, finalMeasure.StdOut, finalMeasure.StdErr,
                analysis.OutputProperties);
            return result;
        }
    }
}