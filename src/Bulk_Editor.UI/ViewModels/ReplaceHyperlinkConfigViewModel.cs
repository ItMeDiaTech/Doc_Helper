using System;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Mvvm;

namespace Doc_Helper.UI.ViewModels
{
    /// <summary>
    /// ViewModel for hyperlink replacement configuration
    /// </summary>
    public class ReplaceHyperlinkConfigViewModel : BindableBase
    {
        private readonly ILogger<ReplaceHyperlinkConfigViewModel> _logger;
        private string _oldUrl = string.Empty;
        private string _newUrl = string.Empty;
        private bool _replaceAll = false;
        private bool _caseSensitive = false;

        public ReplaceHyperlinkConfigViewModel(ILogger<ReplaceHyperlinkConfigViewModel> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ApplyCommand = new DelegateCommand(ExecuteApply, CanExecuteApply);
            CancelCommand = new DelegateCommand(ExecuteCancel);
        }

        #region Properties

        public string OldUrl
        {
            get => _oldUrl;
            set
            {
                SetProperty(ref _oldUrl, value);
                ApplyCommand.RaiseCanExecuteChanged();
            }
        }

        public string NewUrl
        {
            get => _newUrl;
            set
            {
                SetProperty(ref _newUrl, value);
                ApplyCommand.RaiseCanExecuteChanged();
            }
        }

        public bool ReplaceAll
        {
            get => _replaceAll;
            set => SetProperty(ref _replaceAll, value);
        }

        public bool CaseSensitive
        {
            get => _caseSensitive;
            set => SetProperty(ref _caseSensitive, value);
        }

        #endregion

        #region Commands

        public DelegateCommand ApplyCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Command Handlers

        private void ExecuteApply()
        {
            try
            {
                _logger.LogInformation("Applying hyperlink replacement: {OldUrl} -> {NewUrl}", OldUrl, NewUrl);

                // TODO: Implement hyperlink replacement logic
                // This would typically involve calling a service to perform the replacement

                // Close dialog
                DialogResult = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply hyperlink replacement");
            }
        }

        private bool CanExecuteApply()
        {
            return !string.IsNullOrWhiteSpace(OldUrl) && !string.IsNullOrWhiteSpace(NewUrl);
        }

        private void ExecuteCancel()
        {
            try
            {
                _logger.LogInformation("Hyperlink replacement cancelled");
                DialogResult = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cancel operation");
            }
        }

        #endregion

        #region Dialog Result

        public bool? DialogResult { get; private set; }

        #endregion
    }
}