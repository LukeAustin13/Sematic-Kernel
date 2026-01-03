using System;
using System.Collections.Generic;
using System.Text;

namespace SematicKernelWpf.ViewModel
{
    //Generic command class for ViewModel commands
    public class ViewModelCommand  :System.Windows.Input.ICommand
    {

        public event EventHandler? CanExecuteChanged = (sender, e) => { };
        private readonly Action Action;
        public bool IsEnabled { get; set; }

        public ViewModelCommand(Action action)
        {
            {
                IsEnabled = true;
                this.Action = action;
            }
        }

        public bool CanExecute(object? parameter)
        {
            if (IsEnabled)
            {
                return true;
            }
            return false;
        }
        public void Execute(object? parameter)
        {
            if (IsEnabled)
            {
                Action();
            }

        }
    }
}
