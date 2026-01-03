using System;
using System.Collections.Generic;
using System.Text;

namespace SematicKernelWpf.ViewModel
{
    // Generic asynchronous command class for ViewModel commands
    public class AsyncViewModelCommand : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged;

        private readonly Func<Task> _action;
        private bool _isExecuting;

        public bool IsEnabled { get; set; } = true;

        public AsyncViewModelCommand(Func<Task> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public bool CanExecute(object? parameter)
            => IsEnabled && !_isExecuting;

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;

            try
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();
                await _action();
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

}
