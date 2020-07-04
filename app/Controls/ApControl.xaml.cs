using System.Windows.Controls;
using System.Windows.Data;

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
    }
}
