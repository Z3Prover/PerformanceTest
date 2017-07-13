using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerformanceTest;
using System.Threading.Tasks;
using System.Linq;
using static Measurement.Measure;
using System.IO;
using System.Diagnostics;
using Measurement;
using System.Collections.Generic;
using Angara.Data;

namespace PerformanceTest.Tests
{
    [TestClass]
    public class SummaryTests
    {
        private static BenchmarkResult[] BuildResults(int expId, int n, string cat)
        {
            var res = new BenchmarkResult[n];
            for (int i = 0; i < n; i++)
            {
                string s = "1";
                res[i] = new BenchmarkResult(expId, cat + "/file" + i, DateTime.Now, 1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 1, ResultStatus.Success, 0, new MemoryStream(), new MemoryStream(),
                   new Dictionary<string, string>()
                   {
                        { Z3Domain.KeySat, s },
                        { Z3Domain.KeyUnsat, s },
                        { Z3Domain.KeyUnknown, "0" },

                        { Z3Domain.KeyTargetSat, s },
                        { Z3Domain.KeyTargetUnsat, s },
                        { Z3Domain.KeyTargetUnknown, "0" }
                   });
            }
            return res;
        }

        private static void AssertCatSummary(int n, AggregatedAnalysis summary)
        {
            string s = n.ToString();
            Assert.AreEqual(s, summary.Properties[Z3Domain.KeySat]);
            Assert.AreEqual(s, summary.Properties[Z3Domain.KeyUnsat]);
            Assert.AreEqual("0", summary.Properties[Z3Domain.KeyUnknown]);

            Assert.AreEqual("0", summary.Properties[Z3Domain.KeyOverperformed]);
            Assert.AreEqual("0", summary.Properties[Z3Domain.KeyUnderperformed]);

            Assert.AreEqual(s, summary.Properties[Z3Domain.KeyTimeSat]);
            Assert.AreEqual(s, summary.Properties[Z3Domain.KeyTimeUnsat]);
        }

        private static void AreEqualArrays(string[] expected, string[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], actual[i], "Element " + i);
            }
        }

        [TestMethod]
        public void BuildExperimentsSummary()
        {
            var domain = new Z3Domain();

            var benchmarkResults1 = Enumerable.Concat(BuildResults(1, 3, "a"), BuildResults(1, 2, "b"));
            var catSummary1 = ExperimentSummary.Build(benchmarkResults1, domain);

            Assert.AreEqual(2, catSummary1.Count, "2 categories");
            AssertCatSummary(3, catSummary1["a"]);
            AssertCatSummary(2, catSummary1["b"]);

            Table table0 = Table.Empty;
            var experimentSummary1 = new ExperimentSummary(1, DateTimeOffset.Now, catSummary1);
            Table table1 = ExperimentSummaryStorage.AppendOrReplace(table0, experimentSummary1);

            var benchmarkResults2 = Enumerable.Concat(BuildResults(1, 3, "b"), BuildResults(1, 2, "c"));
            var catSummary2 = ExperimentSummary.Build(benchmarkResults2, domain);

            var experimentSummary2 = new ExperimentSummary(2, DateTimeOffset.Now, catSummary2);
            Table table2 = ExperimentSummaryStorage.AppendOrReplace(table1, experimentSummary2);

            Assert.IsTrue(table2.Count >= 2 + 3 * (5 + 8), "Number of columns");
            Assert.AreEqual(2, table2.RowsCount, "Number of rows");

            AreEqualArrays(new[] { "1", "2" }, table2["ID"].Rows.AsString.ToArray());
            AreEqualArrays(new[] { "3", "" }, table2["a|SAT"].Rows.AsString.ToArray());
            AreEqualArrays(new[] { "2", "3" }, table2["b|SAT"].Rows.AsString.ToArray());
            AreEqualArrays(new[] { "", "2" }, table2["c|SAT"].Rows.AsString.ToArray());


        }

        [TestMethod]
        public void BuildExperimentsRecords()
        {
            var domain = new Z3Domain();

            var benchmarkResults1 = Enumerable.Concat(BuildResults(1, 3, "a"), BuildResults(1, 2, "b"));
            var records1 = new Records.RecordsTable(new Dictionary<string, Records.Record>(), new Dictionary<string, Records.CategoryRecord>());
            records1.UpdateWith(benchmarkResults1, domain);

            Assert.AreEqual(3, records1.CategoryRecords["a"].Files);
            Assert.AreEqual(3, records1.CategoryRecords["a"].Runtime);
            Assert.AreEqual(3 + 2, records1.BenchmarkRecords.Count);

            Assert.AreEqual(1, records1.BenchmarkRecords["a/file0"].ExperimentId);
            Assert.AreEqual(1, records1.BenchmarkRecords["a/file0"].Runtime);

            Assert.AreEqual(1, records1.BenchmarkRecords["b/file0"].ExperimentId);
            Assert.AreEqual(1, records1.BenchmarkRecords["b/file0"].Runtime);

            Assert.AreEqual(2, records1.CategoryRecords["b"].Files);
            Assert.AreEqual(2, records1.CategoryRecords["b"].Runtime);
        }
    }
}
