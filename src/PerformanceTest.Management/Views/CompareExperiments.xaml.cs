using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PerformanceTest.Management
{
    public partial class CompareExperiments : Window
    {
        public static RoutedCommand CopyFilenameCommand = new RoutedCommand();

        private IUIService uiService;

        public CompareExperiments()
        {
            InitializeComponent();
        }

        public void SetUIService(IUIService uiService)
        {
            this.uiService = uiService;
        }

        private void canCopyFilename(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 1;
        }
        private void CopyFilename(object target, ExecutedRoutedEventArgs e)
        {
            CompareBenchmarksViewModel elem = (CompareBenchmarksViewModel)dataGrid.SelectedItem;
            Clipboard.SetText(elem.Filename);
        }
        private async void dataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGrid.SelectedItems.Count != 1 || uiService == null)
                return;


            var handle = uiService.StartIndicateLongOperation("Loading output for the benchmark...");
            try
            {
                ShowOutput w = new ShowOutput();
                w.Owner = this;
                w.Show();

                CompareBenchmarksViewModel elem = (CompareBenchmarksViewModel)dataGrid.SelectedItem;
                int inx = dataGrid.CurrentCell.Column.DisplayIndex;
                BenchmarkResultViewModel result = (inx == 1 || inx == 3 || inx == 5 || inx >= 8 && inx <= 10 || inx == 14) ? elem.Results1 : elem.Results2;

                string stdout = await result.GetStdOutAsync(true);
                string stderr = await result.GetStdErrAsync(true);
                ShowOutputViewModel vm = new ShowOutputViewModel(result.ID, elem.Filename, stdout, stderr);
                w.DataContext = vm;
            }
            finally
            {
                uiService.StopIndicateLongOperation(handle);
            }
        }
        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as CompareExperimentsViewModel;
            if (vm != null)
            {
                if ((RadioButton)sender == radioAll) vm.FilterResultsByError(-1);
                else if ((RadioButton)sender == radioBOTHSAT) vm.FilterResultsByError(0);
                else if ((RadioButton)sender == radioBOTHUNSAT) vm.FilterResultsByError(1);
                else if ((RadioButton)sender == radioBOTHUNKNOWN) vm.FilterResultsByError(2);
                else if ((RadioButton)sender == radioONESAT) vm.FilterResultsByError(3);
                else if ((RadioButton)sender == radioONEUNSAT) vm.FilterResultsByError(4);
                else if ((RadioButton)sender == radioONEUNKNOWN) vm.FilterResultsByError(5);
                else if ((RadioButton)sender == radioONEBUGS) vm.FilterResultsByError(6);
                else if ((RadioButton)sender == radioONEERROR) vm.FilterResultsByError(7);
                else if ((RadioButton)sender == radioONETimeouts) vm.FilterResultsByError(8);
                else if ((RadioButton)sender == radioONEMemouts) vm.FilterResultsByError(9);
                else if ((RadioButton)sender == radioSATSTAR) vm.FilterResultsByError(10);
                else if ((RadioButton)sender == radioUNSATSTAR) vm.FilterResultsByError(11);
                else if ((RadioButton)sender == radioOKSTAR) vm.FilterResultsByError(12);
                else if ((RadioButton)sender == radioSATUNSAT) vm.FilterResultsByError(13);

                else if ((RadioButton)sender == radioFNSAT) vm.FilterResultsByText("sat");
                else if ((RadioButton)sender == radioFNUNSAT) vm.FilterResultsByText("unsat");
                else if ((RadioButton)sender == radioFNTEXT) vm.FilterResultsByText(txtFilename.Text);
            }
        }
        private void txtFilename_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as CompareExperimentsViewModel;
                if (vm != null)
                {
                    vm.FilterResultsByText(txtFilename.Text);
                    radioFNTEXT.IsChecked = true;
                }
            }
        }
        private void txtExtensionLeft_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as CompareExperimentsViewModel;
                if (vm != null)
                    vm.Extension1 = txtExtensionLeft.Text;
            }
        }

        private void txtExtensionRight_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as CompareExperimentsViewModel;
                if (vm != null)
                    vm.Extension2 = txtExtensionRight.Text;
            }
        }


    }
}
