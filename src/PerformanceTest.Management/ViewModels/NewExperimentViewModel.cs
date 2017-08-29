using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using AzurePerformanceTest;

namespace PerformanceTest.Management
{
    public class NewExperimentViewModel : INotifyPropertyChanged
    {
        private readonly AzureExperimentManagerViewModel manager;
        private readonly IUIService service;
        private readonly IDomainResolver domainResolver;
        private readonly RecentValuesStorage recentValues;
        private readonly string creator;

        private string benchmarkContainerUri, benchmarkContainerUriNotDefault;
        private bool isDefaultBenchmarkContainerUri;
        private string benchmarkDirectory;
        private string categories;
        private string domain;
        private double memlimit;
        private double timelimit;
        private double exptimelimit;
        private string parameters;
        private string extension;
        private string note;
        private string executable;
        private bool allowAdaptiveRuns;
        private int maxRepetitions;
        private double maxTimeForAdaptiveRuns;

        private int useMostRecentExecutable;

        private string[] fileNames;
        private string recentBlobDisplayName;
        private Task<string> taskRecentBlob;

        private string selectedPool;
        private bool canUseMostRecent;

        public event PropertyChangedEventHandler PropertyChanged;


        public NewExperimentViewModel(AzureExperimentManagerViewModel manager, IUIService service, RecentValuesStorage recentValues, string creator, IDomainResolver domainResolver)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (recentValues == null) throw new ArgumentNullException(nameof(recentValues));
            if (domainResolver == null) throw new ArgumentNullException(nameof(domainResolver));
            this.manager = manager;
            this.service = service;
            this.recentValues = recentValues;
            this.creator = creator;
            this.domainResolver = domainResolver;

            benchmarkContainerUri = ExperimentDefinition.DefaultContainerUri;
            benchmarkContainerUriNotDefault = "";
            isDefaultBenchmarkContainerUri = true;
            ChooseDirectoryCommand = new DelegateCommand(ChooseDirectory);
            ChooseCategoriesCommand = new DelegateCommand(ChooseCategories);
            ChooseExecutableCommand = new DelegateCommand(ChooseExecutable);
            ChoosePoolCommand = new DelegateCommand(ListPools);

            benchmarkDirectory = recentValues.BenchmarkDirectory;
            categories = recentValues.BenchmarkCategories;
            timelimit = recentValues.BenchmarkTimeLimit.TotalSeconds;
            exptimelimit = recentValues.ExperimentTimeLimit.TotalSeconds;
            memlimit = recentValues.BenchmarkMemoryLimit;
            note = recentValues.ExperimentNote;
            allowAdaptiveRuns = recentValues.AllowAdaptiveRuns;
            maxRepetitions = recentValues.MaxRepetitions;
            maxTimeForAdaptiveRuns = recentValues.MaxTimeForAdaptiveRuns;

            Domain = Domains[0];
            // Following will override the defaults given when setting the Domain above.
            string storedExt = recentValues.BenchmarkExtension;
            if (!string.IsNullOrEmpty(storedExt))
                extension = storedExt;
            string storedParam = recentValues.ExperimentExecutableParameters;
            if (!string.IsNullOrEmpty(storedParam))
                parameters = storedParam;
            // string storedDomain = recentValues.Domain;

            UseMostRecentExecutable = true;
            RecentBlobDisplayName = "searching...";
            taskRecentBlob = FindRecentExecutable();

            selectedPool = recentValues.BatchPool;
        }

        public string Creator
        {
            get { return creator; }
        }
        public string Executable
        {
            get { return executable; }
            set
            {
                executable = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("HasOriginalExecutable");
            }
        }
        public bool HasOriginalExecutable
        {
            get { return Executable != null && Executable != ""; }
        }
        public string BenchmarkLibaryDescription
        {
            get { return manager.BenchmarkLibraryDescription; }
        }

        public bool IsDefaultBenchmarkContainerUri
        {
            get { return isDefaultBenchmarkContainerUri; }
            set
            {
                isDefaultBenchmarkContainerUri = value;
                if (isDefaultBenchmarkContainerUri) BenchmarkContainerUri = ExperimentDefinition.DefaultContainerUri;
                else BenchmarkContainerUri = BenchmarkContainerUriNotDefault;
                NotifyPropertyChanged();

            }
        }
        public bool UseNotDefaultBenchmarkContainerUri
        {
            get { return !isDefaultBenchmarkContainerUri; }
            set
            {
                isDefaultBenchmarkContainerUri = !value;
                if (isDefaultBenchmarkContainerUri) BenchmarkContainerUri = ExperimentDefinition.DefaultContainerUri;
                else BenchmarkContainerUri = BenchmarkContainerUriNotDefault;
                NotifyPropertyChanged();

            }
        }
        public string BenchmarkContainerUri
        {
            get { return benchmarkContainerUri; }
            set
            {
                benchmarkContainerUri = value;
                NotifyPropertyChanged();
                BenchmarkDirectory = "";
                Categories = "";
            }
        }
        public string BenchmarkContainerUriNotDefault
        {
            get { return benchmarkContainerUriNotDefault; }
            set
            {
                BenchmarkContainerUri = benchmarkContainerUriNotDefault = value;
                NotifyPropertyChanged();
            }
        }
        public string BenchmarkDirectory
        {
            get { return benchmarkDirectory; }
            set
            {
                if (benchmarkDirectory == value) return;
                benchmarkDirectory = value;
                NotifyPropertyChanged();
                Categories = "";
            }
        }

        public string Categories
        {
            get { return categories; }
            set
            {
                categories = value;
                NotifyPropertyChanged();
            }
        }
        public string Domain
        {
            get { return domain; }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Domain));
                if (domain == value) return;
                domain = value;
                NotifyPropertyChanged();

                string ext = extension;
                try
                {
                    var d = domainResolver.GetDomain(domain);

                    string[] newExt = d.BenchmarkExtensions;
                    if (newExt == null || newExt.Length == 0) return;
                    Extension = string.Join("|", newExt);

                    // Parameters = d.CommandLineParameters;
                }
                catch (Exception ex)
                {
                    Extension = ext;
                    service.ShowWarning(ex.Message, "Couldn't set extensions of the selected domain");
                }
            }
        }

        public string[] Domains
        {
            get { return domainResolver.Domains; }
        }

        public string MainExecutable
        {
            get { return fileNames != null && fileNames.Length > 0 ? fileNames[0] : string.Empty; }
        }

        public bool UseMostRecentExecutable //1
        {
            get { return useMostRecentExecutable == 1; }
            set
            {
                if (useMostRecentExecutable == 1) return;
                useMostRecentExecutable = 1;
                NotifyPropertyChanged("UseMostRecentExecutable");
                NotifyPropertyChanged("UseNewExecutable");
                NotifyPropertyChanged("UseOriginalExecutable");
            }
        }

        public bool CanUseMostRecent
        {
            get { return canUseMostRecent; }
            private set
            {
                if (canUseMostRecent == value) return;
                canUseMostRecent = value;
                NotifyPropertyChanged();
            }
        }


        public bool UseNewExecutable
        {
            get { return useMostRecentExecutable == 2; }
            set
            {
                if (useMostRecentExecutable == 2) return;
                useMostRecentExecutable = 2;
                NotifyPropertyChanged("UseMostRecentExecutable");
                NotifyPropertyChanged("UseNewExecutable");
                NotifyPropertyChanged("UseOriginalExecutable");
            }
        }
        public bool UseOriginalExecutable
        {
            get { return useMostRecentExecutable == 0; }
            set
            {
                if (useMostRecentExecutable == 0) return;
                useMostRecentExecutable = 0;
                NotifyPropertyChanged("UseMostRecentExecutable");
                NotifyPropertyChanged("UseNewExecutable");
                NotifyPropertyChanged("UseOriginalExecutable");
            }
        }
        public string RecentBlobDisplayName
        {
            get { return recentBlobDisplayName; }
            set
            {
                if (recentBlobDisplayName == value) return;
                recentBlobDisplayName = value;
                NotifyPropertyChanged();
            }
        }

        public string[] ExecutableFileNames
        {
            get { return fileNames; }
        }

        public string Parameters
        {
            get { return parameters; }
            set
            {
                parameters = value;
                NotifyPropertyChanged();
            }
        }

        public double BenchmarkTimeoutSec
        {
            get { return timelimit; }
            set
            {
                timelimit = value;
                NotifyPropertyChanged();
            }
        }
        public double ExperimentTimeoutSec
        {
            get { return exptimelimit; }
            set
            {
                exptimelimit = value;
                NotifyPropertyChanged();
            }
        }

        public double BenchmarkMemoryLimitMb
        {
            get { return memlimit; }
            set
            {
                memlimit = value;
                NotifyPropertyChanged();
            }
        }

        public string Extension
        {
            get { return extension; }
            set
            {
                extension = value;
                NotifyPropertyChanged();
            }
        }

        public bool AllowAdaptiveRuns
        {
            get { return allowAdaptiveRuns; }
            set
            {
                allowAdaptiveRuns = value;
                NotifyPropertyChanged();
            }
        }

        public int MaxRepetitions
        {
            get { return maxRepetitions; }
            set
            {
                maxRepetitions = value;
                NotifyPropertyChanged();
            }
        }

        public double MaxTimeForAdaptiveRuns
        {
            get { return maxTimeForAdaptiveRuns; }
            set
            {
                maxTimeForAdaptiveRuns = value;
                NotifyPropertyChanged();
            }
        }

        public string Note
        {
            get { return note; }
            set
            {
                note = value;
                NotifyPropertyChanged();
            }
        }

        public string Pool
        {
            get { return selectedPool; }
            set
            {
                selectedPool = value;
                NotifyPropertyChanged();
            }
        }



        public ICommand ChooseDirectoryCommand
        {
            get; private set;
        }

        public ICommand ChooseCategoriesCommand
        {
            get; private set;
        }

        public ICommand ChooseExecutableCommand
        {
            get; private set;
        }

        public ICommand ChoosePoolCommand
        {
            get; private set;
        }

        public void SaveRecentSettings()
        {
            recentValues.BenchmarkDirectory = benchmarkDirectory;
            recentValues.BenchmarkCategories = categories;
            recentValues.BenchmarkExtension = extension;
            recentValues.ExperimentExecutableParameters = parameters;
            recentValues.Domain = Domain;
            recentValues.BenchmarkTimeLimit = TimeSpan.FromSeconds(timelimit);
            recentValues.ExperimentTimeLimit = TimeSpan.FromSeconds(exptimelimit);
            recentValues.BenchmarkMemoryLimit = memlimit;
            recentValues.ExperimentNote = note;
            recentValues.BatchPool = selectedPool;
            recentValues.AllowAdaptiveRuns = allowAdaptiveRuns;
        }

        public Task<string> GetRecentExecutable()
        {
            return taskRecentBlob;
        }

        /// <summary>
        /// Returns true, if validation succeded and the new experiment may be submitted.
        /// Returns false, if validation failed and new experiment shouldn't be submitted.
        /// Validation can interact with the user and modify the values.
        /// </summary>
        /// <returns></returns>
        public bool Validate()
        {
            bool isValid = true;

            if (UseNewExecutable && (fileNames == null || fileNames.Length == 0))
            {
                isValid = false;
                service.ShowWarning("No files are selected as new executable", "Validation failed");
            }

            if (string.IsNullOrEmpty(Extension))
            {
                isValid = false;
                service.ShowWarning("Benchmark extension is not specified", "Validation failed");
            }

            if (string.IsNullOrEmpty(Pool))
            {
                isValid = false;
                service.ShowWarning("Azure Batch Pool is not specified", "Validation failed");
            }

            if (Parameters == null)
            {
                isValid = false;
                service.ShowWarning("Parameters value is null", "Validation failed");
            }

            return isValid;
        }

        private async Task<string> FindRecentExecutable()
        {
            try
            {
                var exec = await manager.GetRecentExecutable(creator);
                if (exec == null)
                {
                    RecentBlobDisplayName = "not available";
                    UseNewExecutable = true;
                    return null;
                }
                else
                {
                    CanUseMostRecent = true;
                    RecentBlobDisplayName = exec.Item2 != null ? exec.Item2.Value.ToLocalTime().ToString("dd-MM-yyyy HH:mm") : exec.Item1;
                    return exec.Item1;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to find most recent executable: " + ex);
                RecentBlobDisplayName = "failed to find";
                UseNewExecutable = true;
                return null;
            }
        }

        private void ChooseExecutable()
        {
            string[] files = service.ChooseFiles(null, "Executable files (*.exe;*.dll)|*.exe;*.dll|ZIP files (*.zip)|*.zip|All Files (*.*)|*.*", "exe");
            if (files == null || files.Length == 0) return;

            if (files.Length > 1)
            {
                string[] exeFiles = files.Where(f => f.EndsWith(".exe")).ToArray();
                if (exeFiles.Length == 0)
                {
                    service.ShowError("No executable files have been chosen.", "New experiment");
                    return;
                }
                string mainFile = null;
                if (exeFiles.Length == 1)
                    mainFile = exeFiles[0];
                else
                {
                    mainFile = service.ChooseOption("Select main executable",
                        new AsyncLazy<string[]>(() => Task.FromResult(exeFiles)),
                        new Predicate<string>(file => file == exeFiles[0]));
                    if (mainFile == null) return;
                }

                // First element of the file names array must be main executable
                int i = Array.IndexOf(files, mainFile);
                if (i < 0) throw new InvalidOperationException("The chosen main executable is not found in the original file list");
                files[i] = files[0];
                files[0] = mainFile;
            }
            fileNames = files;
            NotifyPropertyChanged("MainExecutable");
            NotifyPropertyChanged("ExecutableFileNames");
            UseMostRecentExecutable = false;
        }

        private void ListPools()
        {
            try
            {
                PoolDescription pool = service.ChooseOption("Choose an Azure Batch Pool",
                    new AsyncLazy<PoolDescription[]>(() => manager.GetAvailablePools()),
                    new Predicate<PoolDescription>(p => p.Id == selectedPool));
                if (pool != null)
                {
                    Pool = pool.Id;
                }
            }
            catch (Exception ex)
            {
                service.ShowError(ex, "Failed to get list of available Azure Batch pools");
            }
        }

        private async void ChooseDirectory()
        {
            try
            {
                string[] initial = BenchmarkDirectory == null ? new string[0] : BenchmarkDirectory.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var selected = await service.BrowseTree("Browse for directory", initial, selection =>
                {
                    return manager.GetDirectories(string.Join("/", selection), BenchmarkContainerUri);
                });
                if (selected != null)
                {
                    BenchmarkDirectory = string.Join("/", selected);
                }
            }
            catch (Exception ex)
            {
                service.ShowError(ex);
            }
        }

        private void ChooseCategories()
        {
            try
            {
                string[] selected = Categories == null ? new string[0] : Categories.Split('|').Select(s => s.Trim()).ToArray();

                selected = service.ChooseOptions("Choose categories",
                    new AsyncLazy<string[]>(() => manager.GetAvailableCategories(BenchmarkDirectory, BenchmarkContainerUri)),
                    new Predicate<string>(c => selected.Contains(c)));
                if (selected != null)
                {
                    Categories = String.Join("|", selected);
                }
            }
            catch (Exception ex)
            {
                service.ShowError(ex);
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
