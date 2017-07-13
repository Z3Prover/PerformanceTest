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
    public partial class ChooseOptionsWindow : Window
    {
        private readonly IUIService uiService;

        public ChooseOptionsWindow(IUIService service)
        {
            if (service == null) throw new ArgumentNullException("service");
            this.uiService = service;

            InitializeComponent();
        }

        public async void SetMultipleSelection<T>(AsyncLazy<T[]> options, Predicate<T> selected)
        {
            listBox.SelectionMode = SelectionMode.Multiple;
            await PopulateItems(options, selected, false);
        }

        public async void SetSingleSelection<T>(AsyncLazy<T[]> options, Predicate<T> selected)
        {
            listBox.SelectionMode = SelectionMode.Single;
            await PopulateItems(options, selected, true);
        }

        private async Task PopulateItems<T>(AsyncLazy<T[]> options, Predicate<T> selected, bool singleSelection)
        {
            okButton.IsEnabled = false;
            tbLoading.Visibility = Visibility.Visible;

            try
            {
                var syncOpts = await options;
                foreach (var item in syncOpts)
                {
                    listBox.Items.Add(item);
                    if (selected(item))
                        if (singleSelection)
                            listBox.SelectedItem = item;
                        else
                            listBox.SelectedItems.Add(item);
                }
                listBox.SelectedItem = selected;
                listBox.Focus();

                okButton.IsEnabled = true;
                tbLoading.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex);
            }
        }

        public object[] SelectedOptions
        {
            get
            {
                int n = listBox.SelectedItems.Count;
                object[] items = new object[n];
                for (int i = 0; i < n; i++)
                {
                    items[i] = listBox.SelectedItems[i];
                }
                return items;
            }
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            if (listBox.SelectedItems.Count != 0)
            {
                this.DialogResult = true;
                Close();
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }
    }
}
