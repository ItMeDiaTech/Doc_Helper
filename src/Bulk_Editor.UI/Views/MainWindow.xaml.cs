using System.Windows;

namespace Doc_Helper.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()  // parameterless; Prism's ViewModelLocator will auto-wire the VM
        {
            InitializeComponent();
        }
    }
}
