using System.Windows;
using Doc_Helper.UI.ViewModels.Dialogs;

namespace Doc_Helper.UI.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog : Window
    {
        public SettingsDialog()
        {
            // InitializeComponent() will be available after rebuild if XAML file exists
            InitializeComponent();
        }

        public SettingsDialog(SettingsDialogViewModel viewModel) : this()
        {
            DataContext = viewModel;

            // Prism dialog system will handle the RequestClose automatically
            // No manual subscription needed
        }
    }
}