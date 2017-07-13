using Measurement;
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
using System.Globalization;

namespace PerformanceTest.Management
{
    /// <summary>
    /// Interaction logic for ShowResults.xaml
    /// </summary>
    public partial class ShowResults : Window
    {
        public static RoutedCommand ReclassifyOKCommand = new RoutedCommand();
        public static RoutedCommand ReclassifyBugCommand = new RoutedCommand();
        public static RoutedCommand ReclassifyErrorCommand = new RoutedCommand();
        public static RoutedCommand ReclassifyTimeoutCommand = new RoutedCommand();
        public static RoutedCommand ReclassifyMemoutCommand = new RoutedCommand();
        public static RoutedCommand RequeueCommand = new RoutedCommand();
        public static RoutedCommand CopyFilenameCommand = new RoutedCommand();
        public ShowResults()
        {
            InitializeComponent();
        }

        private void canReclassify(object sender, CanExecuteRoutedEventArgs e)
        {
            var vm = DataContext as ShowResultsViewModel;
            if (vm.JobStatus != ExperimentExecutionStateVM.Completed)
            {
                e.CanExecute = false;
                return;
            }
            e.CanExecute = dataGrid.SelectedItems.Count > 0;
        }
        private void Reclassify(ResultStatus rc)
        {
            var vm = DataContext as ShowResultsViewModel;
            var elems = dataGrid.SelectedItems.Cast<BenchmarkResultViewModel>().ToArray();
            vm.ReclassifyResults(elems, rc);
        }
        private void ReclassifyOK(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Reclassify(ResultStatus.Success);
            Mouse.OverrideCursor = null;
        }
        private void ReclassifyBug(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Reclassify(ResultStatus.Bug);
            Mouse.OverrideCursor = null;
        }
        private void ReclassifyError(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Reclassify(ResultStatus.Error);
            Mouse.OverrideCursor = null;
        }
        private void ReclassifyTimeout(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Reclassify(ResultStatus.Timeout);
            Mouse.OverrideCursor = null;
        }
        private void ReclassifyMemout(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Reclassify(ResultStatus.OutOfMemory);
            Mouse.OverrideCursor = null;
        }
        private void canRequeue(object sender, CanExecuteRoutedEventArgs e)
        {
            var vm = DataContext as ShowResultsViewModel;

            if (vm.JobStatus == ExperimentExecutionStateVM.Active || vm.JobStatus == ExperimentExecutionStateVM.Loading)
            {
                e.CanExecute = false;
                return;
            }
            e.CanExecute = dataGrid.SelectedItems.Count >= 1;
        }
        private void Requeue(object target, ExecutedRoutedEventArgs e)
        {
            var vm = DataContext as ShowResultsViewModel;
            var elems = dataGrid.SelectedItems.Cast<BenchmarkResultViewModel>().ToArray();
            vm.RequeueResults(elems);
        }
        private void canCopyFilename(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = dataGrid.SelectedItems.Count == 1;
        }
        private void CopyFilename(object target, ExecutedRoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            BenchmarkResultViewModel elem = (BenchmarkResultViewModel)dataGrid.SelectedItem;
            Clipboard.SetText(elem.Filename);
            Mouse.OverrideCursor = null;
        }
        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ShowResultsViewModel;
            if ((RadioButton)sender == radioAll) vm.FilterResultsByError(-1);
            else if ((RadioButton)sender == radioSAT) vm.FilterResultsByError(0);
            else if ((RadioButton)sender == radioUNSAT) vm.FilterResultsByError(1);
            else if ((RadioButton)sender == radioUNKNOWN) vm.FilterResultsByError(2);
            else if ((RadioButton)sender == radioBUGS) vm.FilterResultsByError(3);
            else if ((RadioButton)sender == radioERROR) vm.FilterResultsByError(4);
            else if ((RadioButton)sender == radioTimeouts) vm.FilterResultsByError(5);
            else if ((RadioButton)sender == radioMemouts) vm.FilterResultsByError(6);
            else if ((RadioButton)sender == radioOver) vm.FilterResultsByError(7);
            else if ((RadioButton)sender == radioUnder) vm.FilterResultsByError(8);
            else if ((RadioButton)sender == radioMoreThan)
            {
                int limit;
                try
                {
                    limit = Convert.ToInt32(txtSeconds.Text);
                }
                catch (FormatException)
                {
                    txtSeconds.Text = "0";
                    limit = 0;
                }
                vm.FilterResultsByRuntime(limit);
            }
            else if ((RadioButton)sender == radioOutputContains) vm.FilterResultsByText(txtOutputMatch.Text, 1);
            else if ((RadioButton)sender == radioFNSAT) vm.FilterResultsByText("sat", 0);
            else if ((RadioButton)sender == radioFNUNSAT) vm.FilterResultsByText("unsat", 0);
            else if ((RadioButton)sender == radioFNTEXT) vm.FilterResultsByText(txtFilename.Text, 0);


        }
        private void txtFilename_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as ShowResultsViewModel;
                vm.FilterResultsByText(txtFilename.Text, 0);
                radioFNTEXT.IsChecked = true;
            }
        }
        private void txtSeconds_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as ShowResultsViewModel;
                int limit;
                try
                {
                    limit = Convert.ToInt32(txtSeconds.Text);
                }
                catch (FormatException)
                {
                    txtSeconds.Text = "0";
                    limit = 0;
                }
                vm.FilterResultsByRuntime(limit);
                radioMoreThan.IsChecked = true;
            }
        }

        private void txtOutputMatch_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as ShowResultsViewModel;
                vm.FilterResultsByText(txtOutputMatch.Text, 1);
                radioOutputContains.IsChecked = true;
            }
        }
        private async void dataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGrid.SelectedItems.Count != 1)
                return;

            try
            {
                BenchmarkResultViewModel vm = (BenchmarkResultViewModel)dataGrid.SelectedItem;
                ShowOutput w = new ShowOutput();
                w.Owner = this;
                w.Show();

                w.DataContext = await vm.GetOutputViewModel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Failed to load output");
            }
        }
    }

    public class ResultStatusToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is ResultStatus)
            {
                ResultStatus st = (ResultStatus)value;
                switch (st)
                {
                    case ResultStatus.Success:
                        return "Success";
                    case ResultStatus.OutOfMemory:
                        return "Memory out";
                    case ResultStatus.Timeout:
                        return "Timeout";
                    case ResultStatus.Error:
                        return "Error";
                    case ResultStatus.InfrastructureError:
                        return "Infrastructure error";
                    case ResultStatus.Bug:
                        return "Bug";
                    default:
                        return null;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
