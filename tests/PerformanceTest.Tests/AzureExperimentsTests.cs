using AzurePerformanceTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Tests
{
    [TestClass]
    public class AzureExperimentsTests
    {
        private static string storageConnectionString;
        private const int TestExperimentId = 1000;

        [ClassInitialize]
        public static void Init(TestContext context)
        {
            if (File.Exists("ConnectionString.txt"))
            {
                storageConnectionString = File.ReadAllText("ConnectionString.txt");

                AzureExperimentStorage storage = new AzureExperimentStorage(storageConnectionString);
            }
        }

        public static void ValidatesConnectionString()
        {
            if(storageConnectionString == null)
            {
                Assert.Inconclusive("AzureExperimentsTests expect that the test folder contains ConnectionString.txt with connection string of an Azure Storage.");
            }
        }

        [TestMethod]
        public async Task GetResultsFromCloud()
        {
            ValidatesConnectionString();
            AzureExperimentStorage storage = new AzureExperimentStorage(storageConnectionString);
            AzureExperimentManager manager = AzureExperimentManager.OpenWithoutStart(storage);

            Stopwatch sw1 = Stopwatch.StartNew();
            var results = await manager.GetResults(TestExperimentId);
            Assert.AreEqual(TestExperimentId, results.ExperimentId);
            sw1.Stop();
            Trace.WriteLine("1st time: " + sw1.ElapsedMilliseconds);
            Assert.AreEqual(103814, results.Benchmarks.Length);

            /// Again, should read from local disk:
            Stopwatch sw2 = Stopwatch.StartNew();
            var results2 = await manager.GetResults(TestExperimentId);
            sw2.Stop();
            Trace.WriteLine("2nd time: " + sw2.ElapsedMilliseconds);

            Assert.AreEqual(103814, results2.Benchmarks.Length);
        }

        [TestMethod]
        public async Task GetExperimentsFromCloud()
        {
            ValidatesConnectionString();
            AzureExperimentStorage storage = new AzureExperimentStorage(storageConnectionString);
            AzureExperimentManager manager = AzureExperimentManager.OpenWithoutStart(storage);

            var experiments = (await manager.FindExperiments()).ToArray();
            Assert.IsTrue(experiments.Length > 0);
        }
    }
}
