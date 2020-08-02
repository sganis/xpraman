using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace xpra
{
    public partial class ApControl : UserControl
    {
        public ApControl()
        {
            InitializeComponent();

            
        }
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = (MainWindowViewModel)DataContext;
            vm.OnComboChanged();
            
        }

        private void TreeView_Collapsed(object sender, RoutedEventArgs e)
        {
            ((TreeItem)((TreeViewItem)e.OriginalSource).DataContext).IsExpanded = false;
        }

        private void TreeView_Expanded(object sender, RoutedEventArgs e)
        {
            ((TreeItem)((TreeViewItem)e.OriginalSource).DataContext).IsExpanded = true;
        }
    }
}
