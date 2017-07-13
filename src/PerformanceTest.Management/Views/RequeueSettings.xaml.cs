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
    /// <summary>
    /// Interaction logic for RequeueSettings.xaml
    /// </summary>
    public partial class RequeueSettings : Window
    {
        public RequeueSettings()
        {
            InitializeComponent();
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            NewExperimentViewModel vm = DataContext as NewExperimentViewModel;
            if (vm != null)
            {
                if (vm.Validate())
                    DialogResult = true;
            }
            else
            {
                DialogResult = true;
            }
        }
        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
