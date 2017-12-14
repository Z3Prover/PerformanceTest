using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Web.UI.DataVisualization.Charting;

using PerformanceTest;
using AzurePerformanceTest;

using DataProcessor.Properties;
using System.Threading;

namespace DataProcessor
{
    class Program
    {
        public static void GeneratePlot(AzureExperimentManager aeMan, Tags tags, string prefix, ComparableExperiment refE, int refId, int id, string outputPath)
        {
            Console.WriteLine("Generating plot for #{0}...", id);
            System.Diagnostics.Debug.Print("Starting task for job #{0}", id);
            ComparableExperiment e = Helpers.GetComparableExperiment(id, aeMan).Result;

            Comparison cmp = new Comparison(refE, e, prefix.Replace('|', '/'), tags);
            Chart chart = Charts.BuildComparisonChart(prefix, cmp);

            chart.SaveImage(Path.Combine(outputPath, String.Format("{0}-{1}.png", refId, id)), ChartImageFormat.Png);

            e = null; cmp = null; chart = null;
            System.GC.Collect();
            System.Diagnostics.Debug.Print("Ending task for job #{0}", id);
        }

        private static void outputHandler(StreamWriter sw, string data)
        {
            if (!String.IsNullOrEmpty(data))
                sw.WriteLine(data);
        }
        public static void GenerateLog(string repositoryPath, string outputPath, int fromId, int id, DateTime from, DateTime to)
        {
            string fn = Path.Combine(outputPath, String.Format("{0}-{1}.txt", fromId, id));
            to = new DateTime(to.Year, to.Month, to.Day, 23, 59, 59);
            string args = String.Format("log --after=\"{0}\" " +
                                            "--before=\"{1}\" " +
                                            "--pretty=format:\"%cd %h %an: %s\" " +
                                            "--date=format-local:\"%Y-%m-%d %H:%M\"",
                                            from.ToString("s"),
                                            to.ToString("s"));

            ProcessStartInfo si = new ProcessStartInfo();
            si.Arguments = args;
            si.FileName = "git.exe";
            si.WindowStyle = ProcessWindowStyle.Hidden;
            si.UseShellExecute = false;
            si.WorkingDirectory = repositoryPath;
            si.RedirectStandardOutput = true;
            si.RedirectStandardError = true;
            si.CreateNoWindow = true;

            using (StreamWriter sw = new StreamWriter(new FileStream(fn, FileMode.OpenOrCreate, FileAccess.Write)))
            {
                Process p = Process.Start(si);

                p.OutputDataReceived += new DataReceivedEventHandler((_, eArgs) => outputHandler(sw, eArgs.Data));
                p.ErrorDataReceived += new DataReceivedEventHandler((_, eArgs) => outputHandler(sw, eArgs.Data));
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
            }
        }

        public static async Task Run(string prefix, string outputPath, string repositoryPath)
        {
            Console.WriteLine("Connecting...");
            var sStorage = new SecretStorage(Settings.Default.AADApplicationId, Settings.Default.AADApplicationCertThumbprint, Settings.Default.KeyVaultUrl);
            string cString = await sStorage.GetSecret(Settings.Default.ConnectionStringSecretId);
            AzureSummaryManager sMan = new AzureSummaryManager(cString, Helpers.GetDomainResolver());
            AzureExperimentManager aeMan = AzureExperimentManager.Open(cString);
            Tags tags = await Helpers.GetTags(Settings.Default.SummaryName, sMan);

            Console.WriteLine("Loading timeline...");
            Timeline timeline = await Helpers.GetTimeline(Settings.Default.SummaryName, aeMan, sMan, cString);

            // Numbers: 4.5.0 = 8023; suspect 8308 -> 8312
            Directory.CreateDirectory(outputPath);

            int refId = 8023;
            Console.WriteLine("Loading reference #{0}...", refId);
            ComparableExperiment refE = await Helpers.GetComparableExperiment(refId, aeMan);

            List<Task> tasks = new List<Task>();

            for (int i = 0; i < timeline.Experiments.Length; i++)
            {
                ExperimentViewModel e = timeline.Experiments[i];
                if (e.Id < refId) continue;
                //tasks.Add(GeneratePlot(aeMan, tags, prefix, refE, refId, e.Id, outputPath));
                //GeneratePlot(aeMan, tags, prefix, refE, refId, e.Id, outputPath);
                if (i > 0 && e.Id != refId)
                    GenerateLog(repositoryPath, outputPath, timeline.Experiments[i - 1].Id, e.Id, timeline.Experiments[i - 1].SubmissionTime, e.SubmissionTime);
                //if (e.Id == 8099) break;
            }

            //ParallelOptions popts = new ParallelOptions();
            //popts.MaxDegreeOfParallelism = 1;
            //ParallelLoopResult r = Parallel.ForEach(tasks, popts, t => t.Wait());

            //Task.WaitAll(tasks.ToArray());

            //await Task.WhenAll(tasks);
        }

        static void Main(string[] args)
        {
            Run("QF_NIA/", @"c:\temp\plots", @"c:\dev\z3").Wait();
        }
    }
}