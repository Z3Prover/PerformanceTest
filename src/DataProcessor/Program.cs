using System;
using System.Collections.Generic;
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
            System.Diagnostics.Debug.Print("Starting task for job #{0}", id);
            ComparableExperiment e = Helpers.GetComparableExperiment(id, aeMan).Result;

            Comparison cmp = new Comparison(refE, e, prefix.Replace('|', '/'), tags);
            Chart chart = Charts.BuildComparisonChart(prefix, cmp);

            chart.SaveImage(Path.Combine(outputPath, String.Format("{0}-{1}.png", refId, id)), ChartImageFormat.Png);

            System.GC.Collect();
            System.Diagnostics.Debug.Print("Ending task for job #{0}", id);
        }
        
        public static async Task Run(string prefix, string outputPath)
        {
            var sStorage = new SecretStorage(Settings.Default.AADApplicationId, Settings.Default.AADApplicationCertThumbprint, Settings.Default.KeyVaultUrl);
            string cString = await sStorage.GetSecret(Settings.Default.ConnectionStringSecretId);
            AzureSummaryManager sMan = new AzureSummaryManager(cString, Helpers.GetDomainResolver());
            AzureExperimentManager aeMan = AzureExperimentManager.Open(cString);
            Tags tags = await Helpers.GetTags(Settings.Default.SummaryName, sMan);
            Timeline timeline = await Helpers.GetTimeline(Settings.Default.SummaryName, aeMan, sMan, cString);


            // Numbers: 4.5.0 = 8023; suspect 8308 -> 8312
            Directory.CreateDirectory(outputPath);
            int refId = 8023;
            ComparableExperiment refE = await Helpers.GetComparableExperiment(refId, aeMan);

            List<Task> tasks = new List<Task>();

            for (int i = 0; i < timeline.Experiments.Length; i++)
            {
                ExperimentViewModel e = timeline.Experiments[i];
                if (e.Id < refId) continue;
                //tasks.Add(GeneratePlot(aeMan, tags, prefix, refE, refId, e.Id, outputPath));
                GeneratePlot(aeMan, tags, prefix, refE, refId, e.Id, outputPath);
                if (e.Id == 8099) break;
            }

            //ParallelOptions popts = new ParallelOptions();
            //popts.MaxDegreeOfParallelism = 1;            
            //ParallelLoopResult r = Parallel.ForEach(tasks, popts, t => t.Wait());

            //Task.WaitAll(tasks.ToArray());

            //await Task.WhenAll(tasks);
            
        }

        static void Main(string[] args)
        {
            Run("QF_NIA/", @"c:\temp\plots").Wait();
        }
    }
}