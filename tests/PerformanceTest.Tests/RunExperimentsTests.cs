using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerformanceTest;
using System.Threading.Tasks;
using System.Linq;
using static Measurement.Measure;
using System.IO;
using System.Diagnostics;
using Measurement;

namespace UnitTests
{
    [TestClass]
    public class RunExperimentsTests
    {
        private static IDomainResolver domainResolver = new DomainResolver(new[] { Domain.Default, new Z3Domain() });

        private static ExperimentManager NewManager()
        {
            ReferenceExperiment reference = new ReferenceExperiment(
                    ExperimentDefinition.Create("LinearEquationSolver.exe", ExperimentDefinition.LocalDiskContainerUri, "reference", "csv", "{0} 10", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(0), "default"),
                    1, 0.17);
            ExperimentManager manager = LocalExperimentManager.NewExperiments("measure" + Guid.NewGuid(), reference, domainResolver);
            return manager;
        }

        private static ExperimentManager OpenManager(ExperimentManager old)
        {
            if (old is LocalExperimentManager)
            {
                LocalExperimentManager local = (LocalExperimentManager)old;
                ExperimentManager manager = LocalExperimentManager.OpenExperiments(local.Directory, domainResolver);
                return manager;
            }else
            {
                throw new ArgumentException("Unsupported type of manager");
            }
        }

        [TestCleanup]
        public void Clear()
        {
            foreach (string dir in Directory.GetDirectories(".", "measure*", SearchOption.TopDirectoryOnly)) {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("Failed to clear measure: " + ex.Message);
                }
            }
        }

        [TestMethod]
        public async Task RunExperiment()
        {
            ExperimentDefinition def = ExperimentDefinition.Create("LinearEquationSolver.exe", ExperimentDefinition.LocalDiskContainerUri, "benchmarks_1", "csv", "{0}", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(0), "default");

            ExperimentManager manager = NewManager();
            var expId = await manager.StartExperiment(def);

            var results = (await manager.GetResults(expId)).Benchmarks;
            Assert.AreEqual(1, results.Length, "Number of completed benchmarks");

            var res = results[0];
            Assert.AreEqual(0, res.ExitCode, "exit code");
            Assert.AreEqual(ResultStatus.Success, res.Status, "status");
            Assert.IsTrue(res.CPUTime.TotalSeconds < 1, "Total runtime");
        }

        [TestMethod]
        public async Task RunExperimentsWithCategory()
        {
            ExperimentDefinition def = ExperimentDefinition.Create("LinearEquationSolver.exe", ExperimentDefinition.LocalDiskContainerUri, "benchmarks_2", "csv", "{0} 1000", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(0), "default", 
                category: "IdentitySquare");

            ExperimentManager manager = NewManager();
            var expId = await manager.StartExperiment(def);

            var results = (await manager.GetResults(expId)).Benchmarks;
            Assert.AreEqual(3, results.Length, "Number of completed benchmarks");

            foreach (var res in results)
            {
                Assert.AreEqual(0, res.ExitCode, "exit code");
                Assert.AreEqual(ResultStatus.Success, res.Status, "status");
                Assert.IsTrue(res.CPUTime.TotalSeconds < 10, "Total runtime");
            }
        }

        [TestMethod]
        public async Task FindExperiments()
        {
            ExperimentDefinition def1 = ExperimentDefinition.Create("LinearEquationSolver.exe", ExperimentDefinition.LocalDiskContainerUri, "benchmarks_2", "csv", "{0} 1", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(0), "default", category: "IdentitySquare");
            ExperimentDefinition def2 = ExperimentDefinition.Create("LinearEquationSolver.exe", ExperimentDefinition.LocalDiskContainerUri, "benchmarks_2", "csv", "{0} 2", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(0), "default", category: "IdentitySquare");

            ExperimentManager manager = NewManager();

            var e1 = await manager.StartExperiment(def1);
            var e2 = await manager.StartExperiment(def1);
            var e4 = await manager.StartExperiment(def2);

            var r1 = await Task.WhenAll(manager.GetResults(e1));
            var r2 = await Task.WhenAll(manager.GetResults(e2));
            var r4 = await Task.WhenAll(manager.GetResults(e4));

            var loaded = (await manager.FindExperiments()).ToArray();
            Assert.AreEqual(3, loaded.Length, "Number of found experiments (same manager)");

            var manager2 = OpenManager(manager);
            var loaded2 = (await manager2.FindExperiments()).ToArray();
            Assert.AreEqual(3, loaded2.Length, "Number of found experiments (reloaded)");

            var loaded3 = (await manager2.FindExperiments(new ExperimentManager.ExperimentFilter { ParametersEquals = "{0} 2" })).ToArray();
            Assert.AreEqual(1, loaded3.Length, "Number of found experiments (reloaded, filtered)");
        }

        //[TestMethod]
        //public async Task RunExperimentsAndGetIntermediateStatus()
        //{
        //    ExperimentDefinition def = ExperimentDefinition.Create("LinearEquationSolver.exe", "benchmarks_2", "csv", "{0} 10000", TimeSpan.FromSeconds(10),
        //        category: "IdentitySquare");

        //    ExperimentManager manager = new LocalExperimentManager();

        //    var expId = await manager.StartExperiment(def);

        //    var results = await manager.Result(expId);

        //    Assert.AreEqual(3, results.Length, "Number of completed benchmarks");

        //    foreach (var res in results)
        //    {
        //        Assert.AreEqual(0, res.Measurements.ExitCode, "exit code");
        //        Assert.AreEqual(CompletionStatus.Success, res.Measurements.Status, "status");
        //        Assert.IsTrue(res.Measurements.TotalProcessorTime.TotalSeconds < 10, "Total runtime");
        //    }
        //}
    }
}
