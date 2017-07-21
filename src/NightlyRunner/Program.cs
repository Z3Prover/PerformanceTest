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

            RepositoryContent binary = await GetRecentNightlyBuild();
            if (binary == null)
            {
                Trace.WriteLine("Repository has no new build.");
                return;
            }
            Trace.WriteLine("Last nightly build contains " + binary.Name);

            AzureExperimentManager manager = AzureExperimentManager.Open(connectionString);
            string lastNightlyExecutable = await GetLastNightlyExperiment(manager);
            if(lastNightlyExecutable == binary.Name)
            {
                Trace.WriteLine("No changes found since last nightly experiment.");
                return;
            }

            using (MemoryStream stream = new MemoryStream(binary.Size))
            {
                await Download(binary, stream);
                stream.Position = 0;

                Trace.WriteLine("Opening an experiment manager...");
                await SubmitExperiment(manager, stream, binary.Name);
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

        private static async Task Download(RepositoryContent binary, Stream stream)
        {
            Trace.WriteLine(string.Format("Downloading new nightly build from {0} ({1:F2} MB)...", binary.DownloadUrl, binary.Size / 1024.0 / 1024.0));

            HttpWebRequest request = WebRequest.CreateHttp(binary.DownloadUrl);
            request.AutomaticDecompression = DecompressionMethods.GZip;

            using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
            using (Stream respStream = response.GetResponseStream())
            {
                await respStream.CopyToAsync(stream);
            }
        }

        static async Task SubmitExperiment(AzureExperimentManager manager, Stream source, string fileName)
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

            string commitSha = GetCommitSha(fileName);
            string note = commitSha != null ?
                string.Format("{0} for https://github.com/{1}/{2}/commit/{3}", Settings.ExperimentNote, Settings.GitHubOwner, Settings.GitHubZ3Repository, commitSha) :
                Settings.ExperimentNote;

            var summaryName = Settings.SummaryName == "" ? null : Settings.SummaryName; 
            var experimentId = await manager.StartExperiment(definition, Settings.Creator, note, summaryName);
            Trace.WriteLine(string.Format("Done, experiment id {0}.", experimentId));
        }

        static async Task<string> GetLastNightlyExperiment(AzureExperimentManager manager)
        {
            Trace.WriteLine("Looking for most recent nightly experiment...");

            // Returns a list ordered by submission time
            var experiments = await manager.FindExperiments(new ExperimentManager.ExperimentFilter() { CreatorEquals = Settings.Creator });
            var mostRecent = experiments.FirstOrDefault();
            if (mostRecent == null) return null;
                        
            var metadata = await manager.Storage.GetExecutableMetadata(mostRecent.Definition.Executable);
            string fileName = null;
            if(metadata.TryGetValue(AzureExperimentStorage.KeyFileName, out fileName))
            {
                Trace.WriteLine("Last nightly experiment was run for " + fileName);
            }
            return fileName;
        }

        static async Task<RepositoryContent> GetRecentNightlyBuild()
        {
            Trace.WriteLine("Looking for most recent nightly build...");
            var github = new GitHubClient(new ProductHeaderValue("Z3-Tests-Nightly-Runner"));
            var nightly = await github.Repository.Content.GetAllContents(Settings.GitHubOwner, Settings.GitHubBinariesRepository, Settings.GitHubBinariesNightlyFolder);

            var files = nightly.Select(f => Tuple.Create(f, regex.Match(f.Name))).Where(fm => fm.Item2.Success).ToArray();
            if (files.Length == 0) return null; // no matching files found
            if (files.Length == 1) return files[0].Item1; // single matching file

            // Multiple matching files, should take the most recent
            DateTimeOffset max = DateTimeOffset.MinValue;
            RepositoryContent recent = null; 

            foreach (var fm in files)
            {
                string sha = fm.Item2.Groups[Settings.RegexExecutableFileName_CommitGroup].Value;
                var commit = await github.Repository.Commit.Get(Settings.GitHubOwner, Settings.GitHubZ3Repository, sha);
                var date = commit.Commit.Committer.Date;
                if(date > max)
                {
                    max = date;
                    recent = fm.Item1;
                }
            }
            return recent;
        }

        static string GetCommitSha(string fileName)
        {
            if (fileName == null) return null;
            var m = regex.Match(fileName);
            if (!m.Success) return null;
            var g = m.Groups[Settings.RegexExecutableFileName_CommitGroup];
            if (!g.Success) return null;
            return g.Value;
        }
    }
}
