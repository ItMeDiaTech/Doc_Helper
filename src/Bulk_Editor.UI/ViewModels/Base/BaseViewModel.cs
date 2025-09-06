using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Prism.Mvvm;

namespace Doc_Helper.UI.ViewModels.Base
{
    /// <summary>
    /// Base ViewModel class providing common MVVM functionality
    /// </summary>
    public abstract class BaseViewModel : BindableBase, INotifyPropertyChanged, IDisposable
    {
        protected readonly ILogger _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isBusy;
        private string _statusMessage = string.Empty;
        private bool _disposed;

        /// <summary>
        /// Gets the cancellation token for long-running operations
        /// </summary>
        protected CancellationToken CancellationToken => _cancellationTokenSource.Token;

        /// <summary>
        /// Indicates if the ViewModel is currently performing an operation
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            protected set => SetProperty(ref _isBusy, value);
        }

        /// <summary>
        /// Current status message for the ViewModel
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            protected set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Indicates if the ViewModel has been disposed
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// Initializes a new instance of BaseViewModel
        /// </summary>
        /// <param name="logger">Logger instance</param>
        protected BaseViewModel(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Executes an async operation with busy state management
        /// </summary>
        /// <param name="operation">Operation to execute</param>
        /// <param name="busyMessage">Message to display while busy</param>
        /// <returns>Task representing the operation</returns>
        protected async Task ExecuteAsync(Func<Task> operation, string busyMessage = "Processing...")
        {
            if (IsBusy)
            {
                _logger.LogWarning("Attempted to execute operation while busy: {BusyMessage}", busyMessage);
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = busyMessage;

                await operation().ConfigureAwait(false);

                StatusMessage = "Ready";
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Operation was cancelled: {BusyMessage}", busyMessage);
                StatusMessage = "Cancelled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing operation: {BusyMessage}", busyMessage);
                StatusMessage = $"Error: {ex.Message}";
                throw;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Executes an async operation with result and busy state management
        /// </summary>
        /// <typeparam name="T">Result type</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="busyMessage">Message to display while busy</param>
        /// <returns>Task representing the operation with result</returns>
        protected async Task<T?> ExecuteAsync<T>(Func<Task<T>> operation, string busyMessage = "Processing...")
        {
            if (IsBusy)
            {
                _logger.LogWarning("Attempted to execute operation while busy: {BusyMessage}", busyMessage);
                return default;
            }

            try
            {
                IsBusy = true;
                StatusMessage = busyMessage;

                var result = await operation().ConfigureAwait(false);

                StatusMessage = "Ready";
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Operation was cancelled: {BusyMessage}", busyMessage);
                StatusMessage = "Cancelled";
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing operation: {BusyMessage}", busyMessage);
                StatusMessage = $"Error: {ex.Message}";
                throw;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Cancels any ongoing operations
        /// </summary>
        public virtual void CancelOperations()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogInformation("Cancelling operations for {ViewModelType}", GetType().Name);
                _cancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// Called when the ViewModel is being initialized
        /// </summary>
        public virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the ViewModel is being cleaned up
        /// </summary>
        public virtual Task CleanupAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Safely updates a property on the UI thread
        /// </summary>
        /// <param name="action">Action to execute on UI thread</param>
        protected void UpdateUI(Action action)
        {
            if (Application.Current?.Dispatcher != null)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    Application.Current.Dispatcher.BeginInvoke(action);
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Disposes the ViewModel and cancels any ongoing operations
        /// </summary>
        public virtual void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                CancelOperations();
                CleanupAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during ViewModel cleanup for {ViewModelType}", GetType().Name);
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~BaseViewModel()
        {
            Dispose();
        }
    }
}