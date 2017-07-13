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
    public partial class BrowseDialog : Window
    {
        public BrowseDialog()
        {
            InitializeComponent();
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = e.NewValue as BrowseTreeItemViewModel;
            var vm = DataContext as BrowseTreeViewModel;
            if (vm != null)
            {
                List<BrowseTreeItemViewModel> path = new List<BrowseTreeItemViewModel>();
                var p = selectedItem;
                while (p != null)
                {
                    path.Add(p);
                    p = p.Parent;
                }

                path.Reverse();
                vm.SelectedPath = path.ToArray();

                TreeView tree = sender as TreeView;
                if (tree != null && tree.SelectedItem != null)
                {
                    var treeItem = tree.ItemContainerGenerator.ContainerFromItem(tree.SelectedItem) as TreeViewItem;
                    treeItem?.BringIntoView();
                }
            }
        }

        public static bool GetBringIntoViewWhenSelected(TreeViewItem treeViewItem)
        {
            return (bool)treeViewItem.GetValue(BringIntoViewWhenSelectedProperty);
        }

        public static void SetBringIntoViewWhenSelected(TreeViewItem treeViewItem, bool value)
        {
            treeViewItem.SetValue(BringIntoViewWhenSelectedProperty, value);
        }

        public static readonly DependencyProperty BringIntoViewWhenSelectedProperty =
            DependencyProperty.RegisterAttached("BringIntoViewWhenSelected", typeof(bool),
            typeof(BrowseDialog), new UIPropertyMetadata(false, OnBringIntoViewWhenSelectedChanged));

        static void OnBringIntoViewWhenSelectedChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
        {
            TreeViewItem item = depObj as TreeViewItem;
            if (item == null)
                return;

            if (e.NewValue is bool == false)
                return;

            if ((bool)e.NewValue)
                item.BringIntoView();
        }
    }
}
