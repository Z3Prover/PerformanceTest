using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public class ExperimentListViewModel : INotifyPropertyChanged
    {
        private readonly ExperimentManager manager;
        private readonly IUIService ui;
        private ExperimentStatusViewModel[] allExperiments;
        private ExperimentStatusViewModel[] filteredExperiments;
        private bool isMyExperiments;
        private string keyword;
        private bool filterPending, isFiltering;

        public event PropertyChangedEventHandler PropertyChanged;

        public ExperimentListViewModel(ExperimentManager manager, IUIService ui)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (ui == null) throw new ArgumentNullException("message");
            this.manager = manager;
            this.ui = ui;

            RefreshItemsAsync();
        }

        public ExperimentStatusViewModel[] Items
        {
            get { return filteredExperiments; }
            private set
            {
                filteredExperiments = value;
                NotifyPropertyChanged();
            }
        }

        public string FilterKeyword
        {
            get { return keyword; }
            set
            {
                if (keyword == value) return;
                keyword = value;
                NotifyPropertyChanged();
                FilterExperiments(keyword);
            }
        }
        public bool IsMyExperiments
        {
            get { return isMyExperiments; }
            set
            {
                if (isMyExperiments == value) return;
                isMyExperiments = !isMyExperiments;
                NotifyPropertyChanged();
                FilterExperiments(keyword);
            }
        }

        public void Refresh()
        {
            RefreshItemsAsync();
        }

        public async void DeleteExperiment(int id)
        {
            if (allExperiments == null) return;

            var expr = allExperiments.First(e => e.ID == id);
            string executable = expr.Definition.Executable;

            bool deleteExecutable = false;
            int n = allExperiments.Count(e => e.Definition.Executable == executable);
            if (n == 1)
            {
                var ans = ui.AskYesNoCancel(string.Format("Would you like to delete the executable that was used by the experiment {0}?", id), "Deleting the experiment");
                if (ans == null) return;
                deleteExecutable = ans.Value;
            }

            var handle = ui.StartIndicateLongOperation("Deleting the experiment...");
            try
            {
                Items = filteredExperiments.Where(st => st.ID != id).ToArray();
                await Task.Run(() => manager.DeleteExperiment(id));
                if (deleteExecutable)
                    await Task.Run(() => manager.DeleteExecutable(executable));
            }
            catch (Exception ex)
            {
                Refresh();
                ui.ShowError(ex, "Error occured when tried to delete the experiment " + id.ToString());
            }
            finally
            {
                ui.StopIndicateLongOperation(handle);
            }
        }

        public Task<double> GetRuntimes(int[] ids)
        {
            return Task.Run(async () =>
            {
                var res = await manager.GetStatus(ids);
                return res.Sum(r => r.TotalRuntime.TotalSeconds);
            });
        }

        private async void RefreshItemsAsync()
        {
            var handle = ui.StartIndicateLongOperation("Loading table of experiments...");
            try
            {
                var exp = await Task.Run(() => manager.FindExperiments());
                allExperiments = exp.Select(e => new ExperimentStatusViewModel(e, manager, ui)).ToArray();
                Items = FilterExperiments(allExperiments, keyword);
            }
            catch (Exception ex)
            {
                ui.ShowError(ex, "Failed to load experiments list");
            }
            finally
            {
                ui.StopIndicateLongOperation(handle);
            }

            List<ExperimentStatusViewModel> items = new List<ExperimentStatusViewModel>();
            try
            {
                var now = DateTime.Now;
                foreach (var vm in allExperiments)
                {
                    if (now.Subtract(vm.Submitted).TotalDays > 7) break;
                    vm.JobStatus = ExperimentExecutionStateVM.Loading;
                    items.Add(vm);
                }

                var states = await Task.Run(() => manager.GetExperimentJobState(items.Select(item => item.ID)));
                int n = Math.Min(states.Length, items.Count);
                for (int i = 0; i < n; i++)
                {
                    items[i].JobStatus = (ExperimentExecutionStateVM)states[i];
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Failed to get status of jobs: " + ex);
                foreach (var item in items)
                {
                    item.JobStatus = null;
                }
            }
        }

        private async void FilterExperiments(string keyword)
        {
            if (allExperiments == null) return;
            if (isFiltering)
            {
                filterPending = true;
                return;
            }

            var handle = ui.StartIndicateLongOperation("Filtering experiments...");
            isFiltering = true;
            try
            {
                do
                {
                    filterPending = false;

                    var old = allExperiments;
                    ExperimentStatusViewModel[] prefiltered, filtered;
                    if (IsMyExperiments)
                    {
                        prefiltered = await Task.Run(() => FindCurrentUserExperiments(old, System.Security.Principal.WindowsIdentity.GetCurrent().Name).ToArray());
                        filtered = await Task.Run(() => FilterExperiments(prefiltered, keyword).ToArray());
                    }
                    else
                    {
                        filtered = await Task.Run(() => FilterExperiments(old, keyword).ToArray());
                    }
                    if (allExperiments == old)
                        Items = filtered;
                } while (filterPending);
            }
            catch (Exception ex)
            {
                ui.ShowError(ex, "Failed to filter experiments list");
            }
            finally
            {
                ui.StopIndicateLongOperation(handle);
                isFiltering = false;
            }
        }

        private static ExperimentStatusViewModel[] FilterExperiments(ExperimentStatusViewModel[] source, string keyword)
        {
            if (String.IsNullOrEmpty(keyword)) return source;

            List<ExperimentStatusViewModel> dest = new List<ExperimentStatusViewModel>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                var e = source[i];
                if (Contains(e.Category, keyword) ||
                    Contains(e.Creator, keyword) ||
                    Contains(e.ID.ToString(), keyword) ||
                    Contains(e.Note, keyword))
                    dest.Add(e);
            }
            return dest.ToArray();
        }
        private static ExperimentStatusViewModel[] FindCurrentUserExperiments(ExperimentStatusViewModel[] source, string name)
        {
            if (String.IsNullOrEmpty(name)) return source;

            List<ExperimentStatusViewModel> dest = new List<ExperimentStatusViewModel>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                var e = source[i];
                if (Contains(e.Creator, name))
                    dest.Add(e);
            }
            return dest.ToArray();
        }
        private static bool Contains(string str, string keyword)
        {
            if (str == null) return String.IsNullOrEmpty(keyword);
            return keyword == null || str.ToLowerInvariant().Contains(keyword.ToLowerInvariant());
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public enum ExperimentExecutionStateVM
    {
        Active,
        Completed,
        Terminated,
        NotFound,
        Failed,
        Loading
    }
    public class ExperimentStatusViewModel : INotifyPropertyChanged
    {
        private readonly ExperimentDefinition definition;
        private readonly ExperimentManager manager;
        private readonly IUIService uiService;

        private ExperimentStatus status;

        private bool flag;
        private ExperimentExecutionStateVM? jobStatus;
        public event PropertyChangedEventHandler PropertyChanged;

        public ExperimentStatusViewModel(Experiment exp, ExperimentManager manager, IUIService message)
        {
            if (exp == null) throw new ArgumentNullException("experiment");
            if (manager == null) throw new ArgumentNullException("manager");
            if (message == null) throw new ArgumentNullException("message");
            this.status = exp.Status;
            this.definition = exp.Definition;
            this.flag = status.Flag;
            this.manager = manager;
            this.uiService = message;
        }

        public ExperimentDefinition Definition { get { return definition; } }
        public int ID { get { return status.ID; } }

        public string Category { get { return status.Category; } }

        public DateTime Submitted { get { return status.SubmissionTime; } }

        public string Note
        {
            get { return status.Note; }
        }

        public string Creator { get { return status.Creator; } }

        public string WorkerInformation { get { return status.WorkerInformation; } }

        public int? BenchmarksDone { get { return status.BenchmarksTotal == 0 ? null : (int?)status.BenchmarksDone; } }
        public int? BenchmarksTotal { get { return status.BenchmarksTotal == 0 ? null : (int?)status.BenchmarksTotal; } }
        public int? BenchmarksQueued { get { return status.BenchmarksTotal == 0 ? null : (int?)status.BenchmarksQueued; } }

        public bool Flag
        {
            get { return flag; }

            set
            {
                if (status.Flag != value && status.Flag == flag)
                {
                    flag = value;
                    NotifyPropertyChanged();

                    UpdateStatusFlag();
                }
            }
        }
        public string AdaptiveRun
        {
            get
            {
                if (definition.AdaptiveRunMaxRepetitions == 1 && definition.AdaptiveRunMaxTimeInSeconds == 0)
                    return "Run Once";
                else
                    return String.Format("Auto({0} times,{1} sec)", definition.AdaptiveRunMaxRepetitions, definition.AdaptiveRunMaxTimeInSeconds);
            }
        }
        public ExperimentExecutionStateVM? JobStatus
        {
            get { return jobStatus; }
            set
            {
                if (jobStatus == value) return;
                jobStatus = value;
                NotifyPropertyChanged();
            }
        }
        public void NewStatus(ExperimentStatus newStatus)
        {
            if (newStatus == null) throw new ArgumentNullException("newStatus");
            if (newStatus.ID != status.ID) throw new InvalidOperationException("Invalid experiment id");

            status = newStatus;

            NotifyPropertyChanged(nameof(Category));
            NotifyPropertyChanged(nameof(Submitted));
            NotifyPropertyChanged(nameof(Note));
            NotifyPropertyChanged(nameof(Creator));
            NotifyPropertyChanged(nameof(WorkerInformation));
            NotifyPropertyChanged(nameof(BenchmarksDone));
            NotifyPropertyChanged(nameof(BenchmarksTotal));
            NotifyPropertyChanged(nameof(BenchmarksQueued));
        }

        private async void UpdateStatusFlag()
        {
            try
            {
                await manager.UpdateStatusFlag(status.ID, flag);
                status.Flag = flag;
                Trace.WriteLine("Status flag changed to " + flag + " for " + status.ID);
            }
            catch (Exception ex)
            {
                flag = status.Flag;
                NotifyPropertyChanged("Note");
                uiService.ShowError(ex, "Failed to update experiment status flag");
            }
        }
        public async Task UpdateJobStatus()
        {
            var state = await Task.Run(() => manager.GetExperimentJobState(new[] { status.ID }));
            JobStatus = (ExperimentExecutionStateVM)state[0];
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
