using Measurement;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace PerformanceTest.Management
{
    public class ExperimentPropertiesViewModel : INotifyPropertyChanged
    {
        public static async Task<ExperimentPropertiesViewModel> CreateAsync(ExperimentManager manager, int id, IDomainResolver domainResolver, IUIService ui)
        {
            if (manager == null) throw new ArgumentNullException("manager");

            Experiment exp = await manager.TryFindExperiment(id);
            if (exp == null) throw new KeyNotFoundException(string.Format("There is no experiment with id {0}.", id));

            return new ExperimentPropertiesViewModel(exp.Definition, exp.Status, domainResolver.GetDomain(exp.Definition.DomainName ?? "Z3"), manager, ui);
        }


        private static async Task<ExperimentStatistics> GetStatistics(ExperimentManager manager, int id, Measurement.Domain domain)
        {
            var results = await manager.GetResults(id);
            var aggr = domain.Aggregate(results.Benchmarks.Select(r => new ProcessRunResults(new ProcessRunAnalysis(r.Status, r.Properties), r.CPUTime.TotalSeconds)));
            return new ExperimentStatistics(aggr);
        }

        private readonly int id;
        private readonly ExperimentDefinition definition;
        private readonly Domain domain;
        private ExperimentStatus status;
        private ExperimentStatistics statistics;
        private readonly string[] MachineStatuses = { "OK", "Unable to retrieve status." };

        private readonly ExperimentManager manager;
        private readonly IUIService ui;

        private string currentNote;
        private bool isSyncing;

        private ExperimentExecutionStateVM? executionStatus;

        public event PropertyChangedEventHandler PropertyChanged;

        public ExperimentPropertiesViewModel(ExperimentDefinition def, ExperimentStatus status, Domain domain, ExperimentManager manager, IUIService ui)
        {
            if (def == null) throw new ArgumentNullException("def");
            if (status == null) throw new ArgumentNullException("status");
            if (domain == null) throw new ArgumentNullException("domain");
            if (manager == null) throw new ArgumentNullException("manager");
            if (ui == null) throw new ArgumentNullException("ui");

            this.id = status.ID;
            this.definition = def;
            this.status = status;
            this.domain = domain;
            this.manager = manager;
            this.ui = ui;

            currentNote = status.Note;

            isSyncing = true;
            Sync = new DelegateCommand(async _ =>
                {
                    isSyncing = true;
                    Sync.RaiseCanExecuteChanged();
                    try
                    {
                        if (NoteChanged) await SubmitNote();
                        await Refresh();
                    }
                    catch (Exception ex)
                    {
                        ui.ShowError(ex, "Failed to synchronize experiment properties");
                    }
                    finally
                    {
                        isSyncing = false;
                        Sync.RaiseCanExecuteChanged();
                    }
                },
                _ => !isSyncing);
            Initialize();
        }

        private async void Initialize()
        {
            try
            {
                await RefreshExecutionStatus();
                await BuildStatistics();
            }
            catch (Exception ex)
            {
                ui.ShowError(ex, "Failed to get properties of the experiment");
            }

            isSyncing = false;
            Sync.RaiseCanExecuteChanged();
        }

        private async Task Refresh()
        {
            await RefreshStatus();
            await RefreshExecutionStatus();
            await BuildStatistics();
        }

        private async Task RefreshStatus()
        {
            var handle = ui.StartIndicateLongOperation("Gettings status of the experiment...");
            try
            {
                var resp = (await Task.Run(() => manager.GetStatus(new[] { id }))).FirstOrDefault();
                if (resp == null) return;
                this.status = resp;
                Note = status.Note;
            }
            finally
            {
                ui.StopIndicateLongOperation(handle);
            }

            NotifyPropertyChanged("Status");
            NotifyPropertyChanged("NoteChanged");
            NotifyPropertyChanged("SubmissionTime");
            NotifyPropertyChanged("BenchmarksTotal");
            NotifyPropertyChanged("BenchmarksDone");
            NotifyPropertyChanged("BenchmarksQueued");
            NotifyPropertyChanged("Creator");
        }

        private async Task BuildStatistics()
        {
            var handle = ui.StartIndicateLongOperation("Loading statistics for the experiment...");
            try
            {
                statistics = await Task.Run(() => GetStatistics(manager, id, domain));
            }
            finally
            {
                ui.StopIndicateLongOperation(handle);
            }
            NotifyPropertyChanged("Sat");
            NotifyPropertyChanged("Unsat");
            NotifyPropertyChanged("Unknown");
            NotifyPropertyChanged("Overperformed");
            NotifyPropertyChanged("Underperformed");
            NotifyPropertyChanged("ProblemBug");
            NotifyPropertyChanged("ProblemNonZero");
            NotifyPropertyChanged("ProblemTimeout");
            NotifyPropertyChanged("ProblemMemoryout");
        }

        private async Task RefreshExecutionStatus()
        {
            var state = await Task.Run(() => manager.GetExperimentJobState(new[] { id }));
            executionStatus = (ExperimentExecutionStateVM)state[0];
            NotifyPropertyChanged("ExecutionStatus");
        }

        public ExperimentExecutionStateVM? ExecutionStatus
        {
            get { return executionStatus; }
        }

        public ExperimentStatus Status
        {
            get { return status; }
        }

        public bool NoteChanged
        {
            get { return status.Note != currentNote; }
        }

        public string Note
        {
            get { return currentNote; }
            set
            {
                if (currentNote == value) return;
                currentNote = value;
                NotifyPropertyChanged("NoteChanged");
                NotifyPropertyChanged();
            }
        }


        public DelegateCommand Sync { get; private set; }

        public DateTime SubmissionTime
        {
            get { return status.SubmissionTime; }
        }
        public string BenchmarkContainerUri
        {
            get { return definition.BenchmarkContainerUri; }
        }
        public string Category
        {
            get { return definition.Category; }
        }
        public int BenchmarksTotal
        {
            get { return status.BenchmarksTotal; }
        }
        public int BenchmarksDone
        {
            get { return status.BenchmarksDone; }
        }
        public int BenchmarksQueued
        {
            get { return status.BenchmarksQueued; }
        }

        private int? GetProperty(string prop)
        {
            if (statistics == null) return null;
            return int.Parse(statistics.AggregatedResults.Properties[prop], System.Globalization.CultureInfo.InvariantCulture);
        }

        public int? Sat
        {
            get
            {
                return GetProperty(Z3Domain.KeySat);
            }

        }
        public int? Unsat
        {
            get
            {
                return GetProperty(Z3Domain.KeyUnsat);
            }
        }
        public int? Unknown
        {
            get
            {
                return GetProperty(Z3Domain.KeyUnknown);
            }
        }
        public int? Overperformed
        {
            get
            {
                return GetProperty(Z3Domain.KeyOverperformed);
            }
        }
        public int? Underperformed
        {
            get
            {
                return GetProperty(Z3Domain.KeyUnderperformed);
            }
        }
        public int? ProblemBug
        {
            get
            {
                return statistics == null ? null : (int?)statistics.AggregatedResults.Bugs;
            }
        }
        public int? ProblemNonZero
        {
            get { return statistics == null ? null : (int?)(statistics.AggregatedResults.Errors + statistics.AggregatedResults.InfrastructureErrors); }
        }
        public int? ProblemTimeout
        {
            get { return statistics == null ? null : (int?)statistics.AggregatedResults.Timeouts; }
        }
        public int? ProblemMemoryout
        {
            get { return statistics == null ? null : (int?)statistics.AggregatedResults.MemoryOuts; }
        }
        public double TimeOut
        {
            get
            {
                return definition.BenchmarkTimeout.TotalSeconds;
            }
        }
        public double MemoryOut
        {
            get
            {
                return definition.MemoryLimitMB;
            }
        }
        public string WorkerInformation
        {
            get { return status.WorkerInformation; }
        }
        public string Parameters
        {
            get { return definition.Parameters; }
        }
        public string Group
        {
            get { return definition.GroupName; }
        }
        public string Creator
        {
            get { return status.Creator; }
        }
        public string MachineStatus
        {
            get { return MachineStatuses[1]; }
        }
        public Brush MachineStatusForeground
        {
            get { return Brushes.Orange; }
        }
        public string Title
        {
            get { return "Experiment #" + id.ToString(); }
        }
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async Task SubmitNote()
        {
            try
            {
                await manager.UpdateNote(status.ID, currentNote);
                status.Note = currentNote;
                NotifyPropertyChanged("Note");
                NotifyPropertyChanged("NoteChanged");

                Trace.WriteLine("Note changed to '" + currentNote + "' for " + status.ID);
            }
            catch (Exception ex)
            {
                currentNote = status.Note;
                NotifyPropertyChanged("Note");
                NotifyPropertyChanged("NoteChanged");

                ui.ShowError(ex, "Failed to update experiment note");
            }
        }

        public async Task SaveNote()
        {
            if (NoteChanged)
            {
                await SubmitNote();
                NotifyPropertyChanged("Status");
            }
        }
    }
}
