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
using Microsoft.Win32;

namespace PerformanceTest.Management
{
    /// <summary>
    /// Interaction logic for CreateGroupDialog.xaml
    /// </summary>
    public partial class CreateGroupDialog : Window
    {
        private const string userRoot = "HKEY_CURRENT_USER";
        private const string subkey = "ClusterExperiments";
        private const string keyName = userRoot + "\\" + subkey;
        public CreateGroupDialog()
        {
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtGroupName.Text = (string)Registry.GetValue(keyName, "lastGroupName", "");
            txtGroupName.Text = (string)Registry.GetValue(keyName, "lastGroupNote", "");
        }
        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            Registry.SetValue(keyName, "lastGroupName", txtGroupName.Text, RegistryValueKind.String);
            Registry.SetValue(keyName, "lastGroupNote", txtNote.Text, RegistryValueKind.String);
            DialogResult = true;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
