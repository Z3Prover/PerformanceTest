using Nightly;
using Nightly.Properties;
using PerformanceTest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace AzurePerformanceTest
{
    public static class Helpers
    {
        public static async Task<Timeline> GetTimeline(string summaryName, AzureExperimentManager expManager, AzureSummaryManager summaryManager)
        {
            string connectionString = await Helpers.GetConnectionString();
            IDomainResolver domainResolver = GetDomainResolver();
            var vm = await Timeline.Initialize(connectionString, summaryName, expManager, summaryManager);
            return vm;
        }

        public static async Task<Tuple<int,int>> GetTwoLastExperimentsId(string summaryName, AzureSummaryManager summaryManager)
        {
            var summaries = await summaryManager.GetTimeline(summaryName);

            int penultimate = int.MinValue;
            int latest = int.MinValue;
            for (int i = 0; i < summaries.Length; i++)
            {
                int id = summaries[i].Id;
                if (id > latest)
                {
                    penultimate = latest;
                    latest = id;
                }
                else if (id > penultimate)
                {
                    penultimate = id;
                }
            }
            return Tuple.Create(penultimate, latest);
        }

        public static async Task<ComparableExperiment> GetComparableExperiment(int id, AzureExperimentManager expManager)
        {
            var exp = await expManager.TryFindExperiment(id);
            if (exp == null) throw new KeyNotFoundException("Experiment "+ id + " not found");

            var benchResults = (await expManager.GetResults(id)).Benchmarks;
            ComparableResult[] results = new ComparableResult[benchResults.Length];
            double maxTimeout = 0;
            for (int i = 0; i < benchResults.Length; i++)
            {
                var b = benchResults[i];
                results[i] = new ComparableResult(b);
                if(b.Status == Measurement.ResultStatus.Timeout && b.NormalizedRuntime > maxTimeout)
                {
                    maxTimeout = b.NormalizedRuntime;
                }
            }

            return new ComparableExperiment(id, exp.Status.SubmissionTime, maxTimeout, results);
        }

        public static Task<Tags> GetTags(string summaryName, AzureSummaryManager summaryManager)
        {
            return summaryManager.GetTags(summaryName);
        }


        public static async Task<string> GetConnectionString()
        {
            if (!String.IsNullOrWhiteSpace(Settings.Default.ConnectionString))
            {
                return Settings.Default.ConnectionString;
            }

            var secretStorage = new SecretStorage(Settings.Default.AADApplicationId, Settings.Default.AADApplicationCertThumbprint, Settings.Default.KeyVaultUrl);
            return await secretStorage.GetSecret(Settings.Default.ConnectionStringSecretId);
        }


        private static IDomainResolver domainResolver;

        public static IDomainResolver GetDomainResolver()
        {
            if (domainResolver == null)
                domainResolver = new MEFDomainResolver(System.IO.Path.Combine(HttpRuntime.AppDomainAppPath, "bin"));
            return domainResolver;
        }
    }
}
