using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// <summary>
    /// Interaction logic for ExperimentProperties.xaml
    /// </summary>
    public partial class ExperimentProperties : Window
    {
        public ExperimentProperties()
        {
            InitializeComponent();
        }

        private async void closeButton_Click(object sender, RoutedEventArgs e)
        {
            ExperimentPropertiesViewModel vm = DataContext as ExperimentPropertiesViewModel;
            if (vm != null)
            {
                await vm.SaveNote();
            }
            Close();
        }
    }

    public class BoolToAsteriskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool && (bool)value)
            {
                return "*";
            }
            else return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CountToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int count;
            if (value is int?)
            {
                int? nullValue = (int?)value;
                count = nullValue.HasValue ? nullValue.Value : 0;
            }
            else if (value is int)
            {
                count = (int)value;
            }
            else count = 0;

            if (count == 0)
            {
                if (parameter != null && parameter is string && (string)parameter == "Green")
                    return Brushes.Green;
                return Brushes.Black;
            }
            return Brushes.Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ExecutionStatusToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ExperimentExecutionStateVM? state = value as ExperimentExecutionStateVM?;
            if (value == null)
            {
                if (parameter is string && parameter != null) return parameter;
                return "";
            }
            else
            {
                switch (state)
                {
                    case ExperimentExecutionStateVM.Loading:
                        return "(loading...)";
                    case ExperimentExecutionStateVM.NotFound:
                        return "";
                    case ExperimentExecutionStateVM.Completed:
                        return "Completed";
                    case ExperimentExecutionStateVM.Terminated:
                        return "Terminated";
                    case ExperimentExecutionStateVM.Active:
                        return "Active";
                    case ExperimentExecutionStateVM.Failed:
                        return "Failed";
                    default:
                        return "Unknown";
                }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
