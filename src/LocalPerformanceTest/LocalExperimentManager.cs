using Measurement;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using ExperimentID = System.Int32;

namespace PerformanceTest
{
    public sealed class LocalExperimentManager : ExperimentManager
    {
        public override string BatchPoolID { get; set; }
        public static LocalExperimentManager NewExperiments(string experimentsFolder, ReferenceExperiment reference, IDomainResolver domainResolver)
        {
            ExperimentDefinition def = MakeRelativeDefinition(experimentsFolder, reference.Definition);
            ReferenceExperiment relRef = new ReferenceExperiment(def, reference.Repetitions, reference.ReferenceValue);

            FileStorage storage = FileStorage.Open(experimentsFolder);
            storage.Clear();
            storage.SaveReferenceExperiment(relRef);
            LocalExperimentManager manager = new LocalExperimentManager(storage, domainResolver);
            return manager;
        }

        public static LocalExperimentManager OpenExperiments(string experimentsFolder, IDomainResolver domainResolver)
        {
            FileStorage storage = FileStorage.Open(experimentsFolder);
            LocalExperimentManager manager = new LocalExperimentManager(storage, domainResolver);
            return manager;
        }

        private static ExperimentDefinition MakeRelativeDefinition(string experimentsFolder, ExperimentDefinition def)
        {
            string relExec = Utils.MakeRelativePath(experimentsFolder, def.Executable);
            string relDirectory = Utils.MakeRelativePath(experimentsFolder, def.BenchmarkDirectory);
            return ExperimentDefinition.Create(relExec, def.BenchmarkContainerUri, relDirectory, def.BenchmarkFileExtension,
                def.Parameters, def.BenchmarkTimeout, def.ExperimentTimeout, def.DomainName,
                def.Category, def.MemoryLimitMB);
        }

        private readonly ReferenceExperiment reference;
        private readonly ConcurrentDictionary<ExperimentID, ExperimentInstance> runningExperiments;
        private readonly LocalExperimentRunner runner;
        private readonly FileStorage storage;
        private readonly AsyncLazy<double> asyncNormal;
        private int lastId = 0;

        private LocalExperimentManager(FileStorage storage, IDomainResolver domainResolver) 
        {
            if (storage == null) throw new ArgumentNullException("storage");
            this.storage = storage;
            this.reference = storage.GetReferenceExperiment();

            runningExperiments = new ConcurrentDictionary<ExperimentID, ExperimentInstance>();
            runner = new LocalExperimentRunner(storage.Location, domainResolver);
            lastId = storage.MaxExperimentId;

            asyncNormal = new AsyncLazy<double>(this.ComputeNormal);
        }

        public string Directory
        {
            get { return storage.Location; }
        }

        public override async Task<ExperimentID> StartExperiment(ExperimentDefinition definition, string creator = null, string note = null, string summaryName = null)
        {
            definition = MakeRelativeDefinition(storage.Location, definition);

            ExperimentID id = Interlocked.Increment(ref lastId);
            DateTime submitted = DateTime.Now;

            double normal = await asyncNormal;

            var results = runner.Enqueue(id, definition, normal);

            int benchmarksLeft = results.Length;
            BenchmarkResult[] benchmarks = new BenchmarkResult[results.Length];

            var resultsWithSave =
                results.Select((task, index) =>
                    task.ContinueWith(benchmark =>
                    {
                        int left = Interlocked.Decrement(ref benchmarksLeft);
                        Trace.WriteLine(String.Format("Benchmark {0} completed, {1} left", index, left));
                        if (benchmark.IsCompleted && !benchmark.IsFaulted)
                        {
                            benchmarks[index] = benchmark.Result;
                            if (left == 0)
                            {
                                storage.AddResults(id, benchmarks);
                                ExperimentInstance val;
                                runningExperiments.TryRemove(id, out val);
                            }                            
                            return benchmark.Result;
                        }
                        else throw benchmark.Exception;
                    }))
                .ToArray();

            ExperimentInstance experiment = new ExperimentInstance(id, definition, resultsWithSave);
            runningExperiments[id] = experiment;

            storage.AddExperiment(id, definition, submitted, creator, note);

            return id;
        }

        public override Task RestartBenchmarks(int id, IEnumerable<string> benchmarkNames, string newBenchmarkContainerUri = null)
        {
            throw new NotImplementedException();
        }

        public override Task<IEnumerable<ExperimentStatus>> GetStatus(IEnumerable<int> ids)
        {
            List<ExperimentStatus> status = new List<ExperimentStatus>();
            var experiments = storage.GetExperiments();
            foreach (var id in ids)
            {
                var st = GetStatus(id, experiments[id]);
                status.Add(st);
            }
            return Task.FromResult((IEnumerable<ExperimentStatus>)status);
        }
        
        public override Task DeleteExperiment (int id)
        {
            var deleteRow = storage.GetExperiments()[id];
            storage.RemoveExperimentRow(deleteRow);
            return Task.FromResult(0);
        }
        public override Task UpdateStatusFlag (int id, bool flag)
        {
            var newRow = storage.GetExperiments()[id];
            newRow.Flag = flag;
            storage.ReplaceExperimentRow(newRow);
            return Task.FromResult(0);
        }

        public override Task UpdateNote (int id, string note)
        {
            var newRow = storage.GetExperiments()[id];
            newRow.Note = note;
            storage.ReplaceExperimentRow(newRow);
            return Task.FromResult(0);
        }
        public override async Task<ExperimentResults> GetResults(int id)
        {
            ExperimentInstance experiment;
            if (runningExperiments.TryGetValue(id, out experiment))
            {
                //return experiment.Results;
                return new ExperimentResults(id, await Task.WhenAll(experiment.Results));
            }
            return new ExperimentResults(id, storage.GetResults(id).ToArray());
        }

        public override Task<Experiment> TryFindExperiment(int id)
        {
            ExperimentEntity entity;
            if (!storage.GetExperiments().TryGetValue(id, out entity)) return null;

            ExperimentDefinition def = RowToDefinition(entity);
            ExperimentStatus status = GetStatus(id, entity);
            return Task.FromResult(new Experiment { Definition = def, Status = status });
        }

        public override Task<IEnumerable<Experiment>> FindExperiments(ExperimentFilter? filter = default(ExperimentFilter?))
        {
            IEnumerable<KeyValuePair<int, ExperimentEntity>> experiments =
                storage.GetExperiments()
                .ToArray();

            if (filter.HasValue)
            {
                experiments =
                    experiments
                    .Where(q =>
                    {
                        var id = q.Key;
                        var e = q.Value;
                        return (filter.Value.BenchmarkContainerEquals == null || e.BenchmarkDirectory == filter.Value.BenchmarkContainerEquals) &&
                                    (filter.Value.CategoryEquals == null || e.Category == null || e.Category.Contains(filter.Value.CategoryEquals)) &&
                                    (filter.Value.ExecutableEquals == null || e.Executable == null || e.Executable == filter.Value.ExecutableEquals) &&
                                    (filter.Value.ParametersEquals == null || e.Parameters == null || e.Parameters == filter.Value.ParametersEquals) &&
                                    (filter.Value.NotesEquals == null || e.Note == null || e.Note.Contains(filter.Value.NotesEquals)) &&
                                    (filter.Value.CreatorEquals == null || e.Creator == null || e.Creator.Contains(filter.Value.CreatorEquals));
                    })
                    .OrderByDescending(q => q.Value.Submitted);
            }

            var results = experiments.Select(kvp =>
                 {
                     int id = kvp.Key;
                     ExperimentEntity expRow = kvp.Value;
                     ExperimentDefinition def = RowToDefinition(expRow);
                     ExperimentStatus status = GetStatus(id, expRow);
                     return new Experiment { Definition = def, Status = status };
                 });
            return Task.FromResult(results);
        }

        private ExperimentStatus GetStatus(int id, ExperimentEntity expRow)
        {
            int done, total;
            ExperimentInstance experiment;
            TimeSpan totalRuntime;
            if (runningExperiments.TryGetValue(id, out experiment))
            {
                total = experiment.Results.Length;
                done = experiment.Results.Count(t => t.IsCompleted);
                totalRuntime = done > 0 ? TimeSpan.FromTicks(experiment.Results.Sum(r => r.Result.TotalProcessorTime.Ticks)) : TimeSpan.FromSeconds(0);
            }
            else
            {
                var results = storage.GetResults(id);
                done = total = results.Length;
                totalRuntime = done > 0 ? TimeSpan.FromTicks(results.Sum(r => r.TotalProcessorTime.Ticks)) : TimeSpan.FromSeconds(0);
            }
            return new ExperimentStatus(id, expRow.Category, expRow.Submitted, expRow.Creator, expRow.Note, expRow.Flag, done, total, totalRuntime, GetWorkerInformation());
        }
        private async Task<double> ComputeNormal()
        {
            var benchmarks = await Task.WhenAll(runner.Enqueue(-1, reference.Definition, 1.0, reference.Repetitions));
            var m = benchmarks.Sum(b => b.TotalProcessorTime.TotalSeconds);
            double n = reference.ReferenceValue / m;
            Trace.WriteLine(String.Format("Median reference duration: {0}, normal: {1}", m, n));
            return n;
        }

        private static ExperimentDefinition RowToDefinition(ExperimentEntity row)
        {
            return ExperimentDefinition.Create(
                row.Executable, row.BenchmarkContainerUri, row.BenchmarkDirectory,
                row.BenchmarkFileExtension, row.Parameters,
                TimeSpan.FromSeconds(row.BenchmarkTimeout),
                TimeSpan.FromSeconds(row.ExperimentTimeout),
                row.DomainName, row.Category, row.MemoryLimitMB);
        }

        public override Task DeleteExecutable(string executableName)
        {
            throw new NotImplementedException();
        }

        public override Task<ExperimentExecutionState[]> GetExperimentJobState(IEnumerable<int> ids)
        {
            throw new NotImplementedException();
        }
        public override Task<string[]> GetExperimentPoolId(IEnumerable<int> ids)
        {
            throw new NotImplementedException();
        }

        private string GetWorkerInformation()
        {
            return "";
        }
    }
    

    public class ExperimentInstance
    {
        private readonly ExperimentID id;
        private readonly ExperimentDefinition def;

        private Task<BenchmarkResult>[] results;

        public ExperimentInstance(ExperimentID id, ExperimentDefinition def, Task<BenchmarkResult>[] results)
        {
            if (def == null) throw new ArgumentNullException("def");
            this.id = id;
            this.def = def;

            this.results = results;
        }

        public ExperimentDefinition Definition { get { return def; } }

        public Task<BenchmarkResult>[] Results { get { return results; } }
    }

}
