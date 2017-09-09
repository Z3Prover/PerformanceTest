using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using Measurement;
using System.Diagnostics;
using System.IO;

namespace PerformanceTest.Management
{
    public class ShowResultsViewModel : INotifyPropertyChanged
    {
        private readonly int id;
        private readonly TimeSpan benchmarkTimeout;
        private readonly ExperimentStatusViewModel statusVm;
        private readonly ExperimentManager manager;
        private readonly AzureExperimentManagerViewModel managerVm;
        private readonly ExperimentListViewModel experimentsVm;
        private readonly IUIService uiService;
        private readonly string sharedDirectory;
        private string benchmarkContainerUri;
        private ExperimentExecutionStateVM? jobStatus;

        private ExperimentResults experimentResults;
        private IEnumerable<BenchmarkResultViewModel> results, allResults;

        private bool isFiltering;
        private RecentValuesStorage recentValues;

        public event PropertyChangedEventHandler PropertyChanged;

        public ShowResultsViewModel(ExperimentStatusViewModel st, string sharedDirectory, ExperimentManager manager, 
            AzureExperimentManagerViewModel managerVm, ExperimentListViewModel experimentsVm, RecentValuesStorage recentValues, IUIService uiService)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (managerVm == null) throw new ArgumentNullException(nameof(managerVm));
            if (uiService == null) throw new ArgumentNullException("uiService");
            if (experimentsVm == null) throw new ArgumentNullException(nameof(experimentsVm));
            this.manager = manager;
            this.managerVm = managerVm;
            this.experimentsVm = experimentsVm;
            this.uiService = uiService;
            this.statusVm = st;
            this.id = st.ID;
            this.sharedDirectory = sharedDirectory;
            this.benchmarkTimeout = st.Definition.BenchmarkTimeout;
            this.jobStatus = ExperimentExecutionStateVM.Loading;
            this.benchmarkContainerUri = st.Definition.BenchmarkContainerUri;
            this.recentValues = recentValues;
            RefreshResultsAsync();
        }
        public ExperimentExecutionStateVM? JobStatus
        {
            get { return jobStatus; }
            private set
            {
                jobStatus = value;
                NotifyPropertyChanged();
            }
        }
        public bool IsFiltering
        {
            get { return isFiltering; }
            private set
            {
                isFiltering = value;
                NotifyPropertyChanged();
            }
        }

        private async void RefreshJobStatus()
        {
            await statusVm.UpdateJobStatus();
            JobStatus = statusVm.JobStatus;
        }
        private async void RefreshResultsAsync()
        {
            var handle = uiService.StartIndicateLongOperation("Loading experiment results...");
            try
            {
                experimentResults = null;
                allResults = Results = null;


                var res = await Task.Run(() => manager.GetResults(id));
                experimentResults = res;
                allResults = Results = res.Benchmarks.Select(e => new BenchmarkResultViewModel(e, uiService)).ToArray();
                RefreshJobStatus();
            }
            catch (Exception ex)
            {
                experimentResults = null;
                allResults = Results = null;

                uiService.ShowError(ex.Message, "Failed to load experiment results");
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
        }
        public IEnumerable<BenchmarkResultViewModel> Results
        {
            get { return results; }
            private set
            {
                results = value;
                NotifyPropertyChanged();
            }
        }
        public string Title
        {
            get { return "Experiment " + id.ToString(); }
        }
        public string Directory
        {
            get { return sharedDirectory; }
        }

        public void FilterResultsByError(int code)
        {
            if (IsFiltering) return;
            IsFiltering = true;
            try
            {
                if (code == 0) Results = allResults.Where(e => e.Status == ResultStatus.Success && e.Sat > 0).ToArray();
                else if (code == 1) Results = allResults.Where(e => e.Status == ResultStatus.Success && e.Unsat > 0).ToArray();
                else if (code == 2) Results = allResults.Where(e => e.Status == ResultStatus.Success && e.Unknown > 0).ToArray();
                else if (code == 3) Results = allResults.Where(e => e.Status == ResultStatus.Bug).ToArray();
                else if (code == 4) Results = allResults.Where(e => e.Status == ResultStatus.Error || e.Status == ResultStatus.InfrastructureError).ToArray();
                else if (code == 5) Results = allResults.Where(e => e.Status == ResultStatus.Timeout).ToArray();
                else if (code == 6) Results = allResults.Where(e => e.Status == ResultStatus.OutOfMemory).ToArray();
                else if (code == 7) Results = allResults.Where(e => e.Status == ResultStatus.Success && e.Sat + e.Unsat > e.TargetSat + e.TargetUnsat && e.Unknown < e.TargetUnknown).ToArray();
                else if (code == 8) Results = allResults.Where(e => e.Sat + e.Unsat < e.Sat + e.Unsat || e.Unknown > e.TargetUnknown).ToArray();
                else Results = allResults;
            }
            finally
            {
                IsFiltering = false;
            }
        }
        public async void FilterResultsByText(string filter, int code)
        {
            if (String.IsNullOrEmpty(filter) || code < 0 || code > 1) return;
            if (IsFiltering) return;

            var handle = uiService.StartIndicateLongOperation("Filtering results...");
            IsFiltering = true;
            try
            {
                //code == 0 - only filename
                //code == 1 - output
                if (filter != "")
                {
                    if (code == 0)
                    {
                        var resVm = allResults;
                        if (filter == "sat")
                        {
                            resVm = allResults.Where(e => Regex.IsMatch(e.Filename, "/^(?:(?!unsat).)*$/")).ToArray();
                        }
                        Results = resVm.Where(e => e.Filename.Contains(filter)).ToArray();
                    }
                    if (code == 1) // search in outputs
                    {
                        Results = await Task.Run(async () =>
                        {
                            var selectionTask = allResults.Select(async r =>
                            {
                                string output = await r.GetStdOutAsync(false);
                                if (output.Contains(filter)) return true;

                                string error = await r.GetStdErrAsync(false);
                                if (error.Contains(filter)) return true;

                                return false;
                            });
                            var selection = await Task.WhenAll(selectionTask);
                            return allResults.Where((r, i) => selection[i]).ToArray();
                        });
                    }
                }
                else Results = allResults;
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex, "Failed to filter results");
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
                IsFiltering = false;
            }
        }

        public void FilterResultsByRuntime(int limit)
        {
            if (IsFiltering) return;
            IsFiltering = true;
            try
            {
                Results = allResults.Where(e => e.NormalizedCPUTime >= limit).ToArray();
            }
            finally
            {
                IsFiltering = false;
            }
        }

        public async void ReclassifyResults(BenchmarkResultViewModel[] old_Results, ResultStatus rc)
        {
            var handle = uiService.StartIndicateLongOperation("Updating results status...");
            try
            {
                bool success = await BenchmarkResultViewModel.TryReclassifyResults(old_Results, rc, benchmarkTimeout, experimentResults);
                if (success)
                    uiService.ShowInfo("Results have been reclassified.");
                else
                    uiService.ShowWarning("Results of the experiment have been modified since they were downloaded. Reopen the table and try again.", "Failed to reclassify");
            }
            catch(Exception ex)
            {
                uiService.ShowError(ex, "Error when reclassifying");
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
        }

        public async void RequeueResults(BenchmarkResultViewModel[] items)
        {
            string[] benchmarkNames = items.Select(e => e.Filename).Distinct().ToArray();

            RequeueSettingsViewModel requeueSettingsVm = new RequeueSettingsViewModel(benchmarkContainerUri, managerVm, recentValues, uiService);
            requeueSettingsVm = uiService.ShowRequeueSettings(requeueSettingsVm);
            if (requeueSettingsVm != null)
            {
                var handle = uiService.StartIndicateLongOperation("Requeue experiment results...");
                try
                {
                    manager.BatchPoolID = requeueSettingsVm.Pool;
                    await manager.RestartBenchmarks(id, benchmarkNames, requeueSettingsVm.BenchmarkContainerUri);
                    RefreshJobStatus();
                    experimentsVm.Refresh();
                    uiService.ShowInfo(string.Format("{0} benchmark(s) requeued.", benchmarkNames.Length));
                }
                catch (Exception ex)
                {
                    uiService.ShowError(ex.Message, "Failed to requeue experiment results");
                }
                finally
                {
                    uiService.StopIndicateLongOperation(handle);
                }
            }
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
