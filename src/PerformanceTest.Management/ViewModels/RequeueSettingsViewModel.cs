using AzurePerformanceTest;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PerformanceTest.Management
{
    public class RequeueSettingsViewModel : INotifyPropertyChanged
    {
        private readonly AzureExperimentManagerViewModel managerVm;
        private readonly IUIService service; 
        private string benchmarkContainerUri, benchmarkContainerUriNotDefault;
        private readonly bool isNotDefaultBenchmarkContainerUri;
        private bool isChosenDefaultContainer;
        private string selectedPool;
        private RecentValuesStorage recentValues;
        public event PropertyChangedEventHandler PropertyChanged;

        public RequeueSettingsViewModel(string benchmarkContainerUri, AzureExperimentManagerViewModel managerVm, RecentValuesStorage recentValues, IUIService uiService)
        {
            if (managerVm == null) throw new ArgumentNullException(nameof(managerVm));
            if (uiService == null) throw new ArgumentNullException(nameof(uiService));
            if (recentValues == null) throw new ArgumentNullException(nameof(recentValues));
            this.managerVm = managerVm;
            this.service = uiService;
            this.recentValues = recentValues;
            this.benchmarkContainerUri = benchmarkContainerUri;
            isNotDefaultBenchmarkContainerUri = benchmarkContainerUri != ExperimentDefinition.DefaultContainerUri;
            this.benchmarkContainerUriNotDefault = isNotDefaultBenchmarkContainerUri ? benchmarkContainerUri : "";
            isChosenDefaultContainer = benchmarkContainerUri == ExperimentDefinition.DefaultContainerUri;
            ChoosePoolCommand = new DelegateCommand(ListPools);
            selectedPool = recentValues.BatchPool;
        }

        public bool IsNotDefaultBenchmarkContainerUri
        {
            get { return isNotDefaultBenchmarkContainerUri; }
        }
        public bool IsChosenDefaultContainer
        {
            get { return isChosenDefaultContainer; }
            set
            {
                isChosenDefaultContainer = value;
                if (isChosenDefaultContainer) BenchmarkContainerUri = ExperimentDefinition.DefaultContainerUri;
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
        public string Pool
        {
            get { return selectedPool; }
            set
            {
                selectedPool = value;
                NotifyPropertyChanged();
            }
        }

        public ICommand ChoosePoolCommand
        {
            get; private set;
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

            if (string.IsNullOrEmpty(Pool))
            {
                isValid = false;
                service.ShowWarning("Azure Batch Pool is not specified", "Validation failed");
            }

            return isValid;
        }

        public void SaveRecentSettings()
        {
            recentValues.BatchPool = selectedPool;
        }

        private void ListPools()
        {
            try
            {
                PoolDescription pool = service.ChooseOption("Choose an Azure Batch Pool",
                    new AsyncLazy<PoolDescription[]>(() => managerVm.GetAvailablePools()),
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
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
