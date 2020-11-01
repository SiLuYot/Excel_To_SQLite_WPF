using System;
using System.Windows.Input;

namespace Excel_To_SQLite_WPF
{
    public class Command : ICommand
    {
        private readonly Func<bool> canExecute;
        private readonly Action<object> execute;

        public Command(Action<object> execute) : this(execute, null)
        {
        }

        public Command(Action<object> execute, Func<bool> canExecute)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            if (this.canExecute == null)
            {
                return true;
            }
            return this.canExecute();
        }

        public void Execute(object parameter)
        {
            this.execute?.Invoke(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
