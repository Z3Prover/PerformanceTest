using AzurePerformanceTest;
using NightlyRunner.Properties;
using Octokit;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NightlyRunner
{
    class Program
    {
        static Settings Settings = Settings.Default;
        static Regex regex = new Regex(Settings.RegexExecutableFileName, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        static int Main(string[] args)
        {
            try
            {

                Run().Wait();
                return 0;
            }
            catch(Exception ex)
            {
                Trace.WriteLine("ERROR: " + ex);
                return 1;
            }
        }

        static async Task Run()
        {
            string connectionString = await GetConnectionString();

            Tuple<ReleaseAsset, string> asset_hash = await GetNightlyBuild();
            if (asset_hash == null || asset_hash.Item1 == null)
            {
                Trace.WriteLine("Repository has no new build.");
                return;
            }
            ReleaseAsset asset = asset_hash.Item1;
            string hash = asset_hash.Item2;

            AzureExperimentManager manager = AzureExperimentManager.Open(connectionString);
            DateTime last_submission_time = await GetLastNightlyExperimentSubmissionTime(manager);
            if (last_submission_time >= asset.UpdatedAt)
            {
                Trace.WriteLine("No changes found since last nightly experiment.");
                return;
            }

            using (MemoryStream stream = new MemoryStream(asset.Size))
            {
                await Download(asset, stream);
                stream.Position = 0;

                Trace.WriteLine("Opening an experiment manager...");
                await SubmitExperiment(manager, stream, asset.Name, hash);
            }
        }

        private static async Task<string> GetConnectionString()
        {
            if (!String.IsNullOrWhiteSpace(Settings.ConnectionString))
            {
                return Settings.ConnectionString;
            }

            var secretStorage = new SecretStorage(Settings.Default.AADApplicationId, Settings.Default.AADApplicationCertThumbprint, Settings.Default.KeyVaultUrl);
            return await secretStorage.GetSecret(Settings.Default.ConnectionStringSecretId);
        }

        private static async Task Download(ReleaseAsset binary, Stream stream)
        {
            Trace.WriteLine(string.Format("Downloading new nightly build from {0} ({1:F2} MB)...", binary.BrowserDownloadUrl, binary.Size / 1024.0 / 1024.0));

            HttpWebRequest request = WebRequest.CreateHttp(binary.BrowserDownloadUrl);
            request.AutomaticDecompression = DecompressionMethods.GZip;

            using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
            using (Stream respStream = response.GetResponseStream())
            {
                await respStream.CopyToAsync(stream);
            }
        }

        static async Task SubmitExperiment(AzureExperimentManager manager, Stream source, string fileName, string commit_hash)
        {
            Trace.WriteLine("Uploading new executable...");
            string packageName = await manager.Storage.UploadNewExecutable(source, fileName, Settings.Creator);
            Trace.WriteLine("Successfully uploaded as " + packageName);

            ExperimentDefinition definition =
                ExperimentDefinition.Create(
                    packageName,
                    ExperimentDefinition.DefaultContainerUri,
                    Settings.BenchmarkDirectory,
                    Settings.BenchmarkFileExtension,
                    Settings.Parameters,
                    TimeSpan.FromSeconds(Settings.BenchmarkTimeoutSeconds),
                    TimeSpan.FromSeconds(Settings.ExperimentTimeoutSeconds),
                    Settings.Domain,
                    Settings.BenchmarkCategory,
                    Settings.MemoryLimitMegabytes,
                    1, 0 /* Run each benchmark once (i.e. adaptive run is off) */);

            Trace.WriteLine(string.Format("Starting nightly experiment in Batch pool \"{0}\"...", Settings.AzureBatchPoolId));
            manager.BatchPoolID = Settings.AzureBatchPoolId;

            string note = commit_hash != null ?
                string.Format("{0} for https://github.com/{1}/{2}/commit/{3}", Settings.ExperimentNote, Settings.GitHubOwner, Settings.GitHubZ3Repository, commit_hash) :
                Settings.ExperimentNote;

            var summaryName = Settings.SummaryName == "" ? null : Settings.SummaryName;
            var experimentId = await manager.StartExperiment(definition, Settings.Creator, note, summaryName);
            Trace.WriteLine(string.Format("Done, experiment id {0}.", experimentId));
        }

        static async Task<DateTime> GetLastNightlyExperimentSubmissionTime(AzureExperimentManager manager)
        {
            Trace.WriteLine("Looking for most recent nightly experiment...");

            // Returns a list ordered by submission time
            var experiments = await manager.FindExperiments(new ExperimentManager.ExperimentFilter() { CreatorEquals = Settings.Creator });
            var mostRecent = experiments.FirstOrDefault();
            if (mostRecent == null) return DateTime.MinValue;

            Trace.WriteLine("Last nightly experiment was submitted at " + mostRecent.Status.SubmissionTime);
            return mostRecent.Status.SubmissionTime;
        }

        static async Task<Tuple<ReleaseAsset, string>> GetNightlyBuild()
        {
            Trace.WriteLine("Looking for most recent nightly build...");
            var github = new GitHubClient(new ProductHeaderValue("Z3-Tests-Nightly-Runner"));
            var release = await github.Repository.Release.Get(Settings.GitHubOwner, Settings.GitHubZ3Repository, "Nightly");
            if (release == null) return null; // no matching files found

            var assets = release.Assets.Select(f => Tuple.Create(f, regex.Match(f.Name))).Where(fm => fm.Item2.Success).ToArray();
            if (assets == null || assets.Length == 0) return null;  // no matching files found

            // Multiple matching files, should take the most recent
            DateTimeOffset max = DateTimeOffset.MinValue;
            ReleaseAsset recent = null;
            string hash = release.TargetCommitish;

            foreach (var a in assets)
            {
                var commit = await github.Repository.Commit.Get(Settings.GitHubOwner, Settings.GitHubZ3Repository, hash);
                var date = a.Item1.UpdatedAt;
                if (date > max)
                {
                    max = date;
                    recent = a.Item1;
                }
            }

            return Tuple.Create(recent, release.TargetCommitish);
        }
    }
}
