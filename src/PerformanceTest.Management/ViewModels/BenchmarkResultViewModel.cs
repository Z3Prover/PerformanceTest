using Measurement;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PerformanceTest.Management
{
    public class BenchmarkResultViewModel : INotifyPropertyChanged
    {
        private readonly IUIService uiService;
        private BenchmarkResult result;

        public event PropertyChangedEventHandler PropertyChanged;

        public BenchmarkResultViewModel(BenchmarkResult res, IUIService service)
        {
            if (res == null) throw new ArgumentNullException("benchmark");
            this.result = res;
            this.uiService = service;
        }
        public int ID
        {
            get { return result.ExperimentID; }
        }
        public string Filename
        {
            get { return result.BenchmarkFileName; }
        }
        public int? ExitCode
        {
            get { return result.ExitCode; }
        }
        public double WallClockTime
        {
            get { return result.WallClockTime.TotalSeconds; }
        }
        public double CPUTime
        {
            get { return result.CPUTime.TotalSeconds; }
        }
        public ResultStatus Status
        {
            get { return result.Status; }
        }
        public int Sat
        {
            get { return GetProperty(Z3Domain.KeySat); }
        }
        public int Unsat
        {
            get { return GetProperty(Z3Domain.KeyUnsat); }
        }
        public int Unknown
        {
            get { return GetProperty(Z3Domain.KeyUnknown); }
        }
        public int TargetSat
        {
            get { return GetProperty(Z3Domain.KeyTargetSat); }
        }
        public int TargetUnsat
        {
            get { return GetProperty(Z3Domain.KeyTargetUnsat); }
        }
        public int TargetUnknown
        {
            get { return GetProperty(Z3Domain.KeyTargetUnknown); }
        }
        public double NormalizedCPUTime
        {
            get { return result.NormalizedCPUTime; }
        }

        public double MemorySizeMB
        {
            get { return result.PeakMemorySizeMB; }
        }

        public Task<string> GetStdOutAsync(bool useDefaultIfMissing)
        {
            return ReadOutputAsync(result.StdOut, useDefaultIfMissing);
        }

        public Task<string> GetStdErrAsync(bool useDefaultIfMissing)
        {
            return ReadOutputAsync(result.StdErr, useDefaultIfMissing);
        }


        public async Task<ShowOutputViewModel> GetOutputViewModel()
        {
            var handle = uiService.StartIndicateLongOperation("Loading benchmark output...");
            try
            {
                return await Task.Run(async () =>
                {
                    string stdOut = await GetStdOutAsync(true);
                    string stdErr = await GetStdErrAsync(true);
                    ShowOutputViewModel vm = new ShowOutputViewModel(ID, Filename, stdOut, stdErr);
                    return vm;
                });
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
        }

        private int GetProperty(string prop)
        {
            int res = result.Properties.ContainsKey(prop) ? Int32.Parse(result.Properties[prop], System.Globalization.CultureInfo.InvariantCulture) : 0;
            return res;
        }

        private async Task<string> ReadOutputAsync(Stream stream, bool useDefaultIfMissing)
        {
            string text = null;
            if (stream != null)
            {
                stream.Position = 0;
                StreamReader reader = new StreamReader(stream);
                text = await reader.ReadToEndAsync();
                text = System.Text.RegularExpressions.Regex.Unescape(text);
            }

            if (useDefaultIfMissing && String.IsNullOrEmpty(text))
                return "*** NO OUTPUT SAVED ***";
            return text;
        }

        private void UpdateResult(BenchmarkResult newResult)
        {
            if (newResult == null) throw new ArgumentNullException(nameof(newResult));
            this.result = newResult;
            NotifyPropertyChanged(nameof(Filename));
            NotifyPropertyChanged(nameof(ExitCode));
            NotifyPropertyChanged(nameof(WallClockTime));
            NotifyPropertyChanged(nameof(CPUTime));
            NotifyPropertyChanged(nameof(Status));
            NotifyPropertyChanged(nameof(Sat));
            NotifyPropertyChanged(nameof(Unsat));
            NotifyPropertyChanged(nameof(Unknown));
            NotifyPropertyChanged(nameof(TargetSat));
            NotifyPropertyChanged(nameof(TargetUnsat));
            NotifyPropertyChanged(nameof(TargetUnknown));
            NotifyPropertyChanged(nameof(NormalizedCPUTime));
            NotifyPropertyChanged(nameof(MemorySizeMB));
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static async Task<bool> TryReclassifyResults(BenchmarkResultViewModel[] resultsToUpdate, ResultStatus newStatus, TimeSpan benchmarkTimeout, ExperimentResults results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (resultsToUpdate == null) throw new ArgumentNullException(nameof(resultsToUpdate));
            if (resultsToUpdate.Length == 0) return true;

            var changes = await results.TryUpdateStatus(resultsToUpdate.Select(r => r.result), newStatus);
            if (changes != null) // ok
            {
                foreach (var c in changes)
                {
                    BenchmarkResultViewModel vm = resultsToUpdate.First(r => r.result == c.Key);
                    // replace and notify about new status
                    vm.UpdateResult(c.Value);
                }
                return true;
            }
            return false;
        }
    }

}
