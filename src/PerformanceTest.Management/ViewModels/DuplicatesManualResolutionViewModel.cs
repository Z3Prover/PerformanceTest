using AzurePerformanceTest;
using Measurement;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PerformanceTest.Management
{
    public class DuplicatesManualResolutionViewModel : INotifyPropertyChanged
    {
        private readonly int id;
        private readonly BenchmarkResultViewModel[] duplicatesVm;
        private readonly BenchmarkResult[] duplicates;
        private BenchmarkResult pick;

        public event PropertyChangedEventHandler PropertyChanged;

        public DuplicatesManualResolutionViewModel(int id, BenchmarkResult[] duplicates, IUIService uiService)
        {
            this.id = id;
            this.duplicates = duplicates;
            this.duplicatesVm = duplicates.Select(d => new BenchmarkResultViewModel(d, uiService)).ToArray();
        }

        public string Title
        {
            get { return "Duplicates in experiment #" + id + "..."; }
        }

        public BenchmarkResultViewModel[] Duplicates
        {
            get { return duplicatesVm; }
        }

        public BenchmarkResult SelectedResult
        {
            get { return pick; }
        }

        public void Pick(BenchmarkResultViewModel takeThis)
        {
            pick = duplicates[Array.IndexOf<BenchmarkResultViewModel>(duplicatesVm, takeThis)];
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
