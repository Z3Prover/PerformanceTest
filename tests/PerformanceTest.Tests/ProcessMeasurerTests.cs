using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerformanceTest;
using System.Threading.Tasks;
using Measurement;
using System.Diagnostics;
using System.IO;
using Ionic.Zip;

namespace UnitTests
{
    [TestClass]
    public class ProcessMeasurerTests
    {
        [TestMethod]
        public void MeasureProcessRun()
        {
            Measurement.ProcessRunMeasure m = ProcessMeasurer.Measure("Delay.exe", "100", TimeSpan.FromMilliseconds(1000));

            Assert.AreEqual(0, m.ExitCode, "Exit code");
            Assert.AreEqual(Measure.LimitsStatus.WithinLimits, m.Limits);
            Assert.IsTrue(m.PeakMemorySizeMB > 1, "Memory size seems too low");


            var ptime = m.TotalProcessorTime.TotalMilliseconds;
            var wctime = m.WallClockTime.TotalMilliseconds;
            Assert.IsTrue(ptime <= 1000 && wctime >= ptime, "Total processor time must be very small because Delay.exe mostly sleeps but it is " + ptime);
            Assert.IsTrue(wctime >= 100, "Wall-clock time must be greater than given delay");
            Assert.IsTrue(wctime < 1000, "Wall-clock time must be less");


            StreamReader reader = new StreamReader(m.StdOut);
            string output = reader.ReadToEnd();
            Assert.IsTrue(output.Contains("Done."), "Output must contain certain text.");

            reader = new StreamReader(m.StdErr);
            string error = reader.ReadToEnd();
            Assert.IsTrue(error.Contains("Sample error text."), "Error output must contain certain text.");
        }

        [TestMethod]
        public void MeasureProcessRunFromZip()
        {
            var zipname = Path.GetTempFileName();
            using (var zip = new ZipFile())
            {
                zip.AddFiles(new string[] { "Delay.exe" });
                zip.Save(zipname);
            }
            try
            {
                Measurement.ProcessRunMeasure m = ProcessMeasurer.Measure(zipname, "100", TimeSpan.FromMilliseconds(1000));

                Assert.AreEqual(0, m.ExitCode, "Exit code");
                Assert.AreEqual(Measure.LimitsStatus.WithinLimits, m.Limits);
                Assert.IsTrue(m.PeakMemorySizeMB > 1, "Memory size seems too low");


                var ptime = m.TotalProcessorTime.TotalMilliseconds;
                var wctime = m.WallClockTime.TotalMilliseconds;
                Assert.IsTrue(ptime <= 1000 && wctime >= ptime, "Total processor time must be very small because Delay.exe mostly sleeps but it is " + ptime);
                Assert.IsTrue(wctime >= 100 && wctime < 1000, "Wall-clock time must be greater than given delay");

                StreamReader reader = new StreamReader(m.StdOut);
                string output = reader.ReadToEnd();
                Assert.IsTrue(output.Contains("Done."), "Output must contain certain text.");

                reader = new StreamReader(m.StdErr);
                string error = reader.ReadToEnd();
                Assert.IsTrue(error.Contains("Sample error text."), "Error output must contain certain text.");
            }
            finally
            {
                File.Delete(zipname);
            }
        }

        [TestMethod]
        public void MeasureProcessRunFromPackage()
        {
            var zipname = Path.GetTempFileName();
            using (var zip = new ZipFile())
            {
                zip.AddFiles(new string[] { "Delay.exe", "FailingTool.exe" });
                string mainexe = "Delay.exe";
                string content_types = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"exe\" ContentType=\"application/octet-stream\" /><Default Extension=\"dll\" ContentType=\"application/octet-stream\" /><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\" /></Types>";
                string rels = "<?xml version=\"1.0\" encoding=\"utf-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Type=\"http://schemas.openxmlformats.org/package/2006/relationships/meta data/thumbnail\" Target=\"/" + mainexe + "\" Id=\"R17bb7f6124fd45fe\" /></Relationships>";
                zip.AddEntry("[Content_Types].xml", content_types);
                zip.AddEntry("_rels\\.rels", rels);
                zip.Save(zipname);
            }
            try
            {
                Measurement.ProcessRunMeasure m = ProcessMeasurer.Measure(zipname, "100", TimeSpan.FromMilliseconds(1000));

                Assert.AreEqual(0, m.ExitCode, "Exit code");
                Assert.AreEqual(Measure.LimitsStatus.WithinLimits, m.Limits);
                Assert.IsTrue(m.PeakMemorySizeMB > 1, "Memory size seems too low");


                var ptime = m.TotalProcessorTime.TotalMilliseconds;
                var wctime = m.WallClockTime.TotalMilliseconds;
                Assert.IsTrue(ptime <= 1000 && wctime >= ptime, "Total processor time must be very small because Delay.exe mostly sleeps but it is " + ptime);
                Assert.IsTrue(wctime >= 100 && wctime < 1000, "Wall-clock time must be greater than given delay");

                StreamReader reader = new StreamReader(m.StdOut);
                string output = reader.ReadToEnd();
                Assert.IsTrue(output.Contains("Done."), "Output must contain certain text.");

                reader = new StreamReader(m.StdErr);
                string error = reader.ReadToEnd();
                Assert.IsTrue(error.Contains("Sample error text."), "Error output must contain certain text.");
            }
            finally
            {
                File.Delete(zipname);
            }
        }

        [TestMethod]
        public void ZipWithMultipleExecutablesWithoutRelationshipFail()
        {
            var zipname = Path.GetTempFileName();
            using (var zip = new ZipFile())
            {
                zip.AddFiles(new string[] { "Delay.exe", "FailingTool.exe" });
                zip.Save(zipname);
            }
            bool fail = false;
            try
            {
                Measurement.ProcessRunMeasure m = ProcessMeasurer.Measure(zipname, "100", TimeSpan.FromMilliseconds(1000));

            }
            catch
            {
                fail = true;
            }
            finally
            {
                File.Delete(zipname);
            }
            Assert.IsTrue(fail, "Zip with multiple executables and no relationships should be considered incorrect.");
        }

        [TestMethod]
        public void MeasureProcessRunWithTimeout()
        {
            Measurement.ProcessRunMeasure m = ProcessMeasurer.Measure("Delay.exe", "10000", TimeSpan.FromMilliseconds(100));

            Assert.AreEqual(Measure.LimitsStatus.TimeOut, m.Limits);
            Assert.AreEqual(null, m.ExitCode, "Exit code");
            Assert.IsTrue(m.PeakMemorySizeMB > 1, "Memory size seems too low");


            var ptime = m.TotalProcessorTime.TotalMilliseconds;
            var wctime = m.WallClockTime.TotalMilliseconds;
            Assert.IsTrue(ptime > 15 && ptime <= 100, "Total processor time");
            Assert.IsTrue(wctime >= 100 && wctime <= 500, "Wall-clock time must be greater than given timeout");

            StreamReader reader = new StreamReader(m.StdOut);
            string output = reader.ReadToEnd();
            Assert.IsTrue(output.Contains("Starting"), "Output must contain certain text.");

            reader = new StreamReader(m.StdErr);
            string error = reader.ReadToEnd();
            Assert.IsTrue(String.IsNullOrEmpty(error), "Error output");
        }

        [TestMethod]
        public void MeasureProcessExitCode()
        {
            Measurement.ProcessRunMeasure m = ProcessMeasurer.Measure("Delay.exe", "10000 extraArgument", TimeSpan.FromMilliseconds(10000));

            Assert.AreEqual(Measure.LimitsStatus.WithinLimits, m.Limits);
            Assert.AreEqual(42, m.ExitCode, "Exit code");
            Assert.IsTrue(m.PeakMemorySizeMB > 1, "Memory size seems too low");


            var ptime = m.TotalProcessorTime.TotalMilliseconds;
            var wctime = m.WallClockTime.TotalMilliseconds;
            Assert.IsTrue(ptime > 0 && ptime <= 100, "Total processor time");
            Assert.IsTrue(wctime > 0 && wctime <= 10000, "Wall-clock time must be greater than given timeout");

            StreamReader reader = new StreamReader(m.StdOut);
            string output = reader.ReadToEnd();
            Assert.IsTrue(output.Contains("Use: Delay.exe"), "Output must contain certain text.");

            reader = new StreamReader(m.StdErr);
            string error = reader.ReadToEnd();
            Assert.IsTrue(String.IsNullOrEmpty(error), "Error output");
        }

        [TestMethod]
        public void MeasureProcessThrowsException()
        {
            Measurement.ProcessRunMeasure m = ProcessMeasurer.Measure("FailingTool.exe", "arg1 arg2", TimeSpan.FromMilliseconds(10000));

            Assert.AreEqual(Measure.LimitsStatus.WithinLimits, m.Limits);
            Assert.IsTrue(m.ExitCode < 0, "Exit code");
            Assert.IsTrue(m.PeakMemorySizeMB > 1, "Memory size seems too low");


            var ptime = m.TotalProcessorTime.TotalMilliseconds;
            var wctime = m.WallClockTime.TotalMilliseconds;
            Assert.IsTrue(ptime <= 1000, "Total processor time");
            Assert.IsTrue(wctime <= 10000, "Wall-clock time");

            StreamReader reader = new StreamReader(m.StdOut);
            string output = reader.ReadToEnd();
            reader = new StreamReader(m.StdErr);
            string error = reader.ReadToEnd();

            Assert.IsTrue(String.IsNullOrEmpty(output), "Output");
            Assert.IsTrue(error.Contains("Unhandled Exception"), "Error must contain certain text.");
        }

        [TestMethod]
        public void MeasureProcessOutOfMemory()
        {
            Measurement.ProcessRunMeasure m = ProcessMeasurer.Measure("FailingTool.exe", "out-of-memory", TimeSpan.FromMinutes(10));

            Assert.AreEqual(Measure.LimitsStatus.WithinLimits, m.Limits);
            Assert.IsTrue(m.ExitCode < 0, "Exit code");
            Assert.IsTrue(m.PeakMemorySizeMB > 1, "Memory size seems too low");


            var ptime = m.TotalProcessorTime.TotalMilliseconds;
            var wctime = m.WallClockTime.TotalMilliseconds;
            Assert.IsTrue(ptime <= 100000, "Total processor time");
            Assert.IsTrue(wctime <= 100000, "Wall-clock time");

            StreamReader reader = new StreamReader(m.StdOut);
            string output = reader.ReadToEnd();
            reader = new StreamReader(m.StdErr);
            string error = reader.ReadToEnd();

            Assert.IsTrue(output.Contains("i = 0"), "Output");
            Assert.IsTrue(error.Contains("OutOfMemoryException"), "Error must contain certain text.");
        }

        [TestMethod]
        public void MeasureProcessMemoryLimit()
        {
            Measurement.ProcessRunMeasure m = ProcessMeasurer.Measure("FailingTool.exe", "out-of-memory", TimeSpan.FromMinutes(10), memoryLimit: 1);

            Assert.AreEqual(Measure.LimitsStatus.MemoryOut, m.Limits);
            Assert.IsTrue(m.ExitCode == null, "Exit code");
            Assert.IsTrue(m.PeakMemorySizeMB > 1, "Memory size seems too low");


            var ptime = m.TotalProcessorTime.TotalMilliseconds;
            var wctime = m.WallClockTime.TotalMilliseconds;
            Assert.IsTrue(ptime <= 100000, "Total processor time");
            Assert.IsTrue(wctime <= 100000, "Wall-clock time");

            StreamReader reader = new StreamReader(m.StdOut);
            string output = reader.ReadToEnd();
            reader = new StreamReader(m.StdErr);
            string error = reader.ReadToEnd();
        }
    }
}
