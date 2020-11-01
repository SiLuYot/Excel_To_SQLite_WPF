using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Excel_To_SQLite_WPF
{
    public partial class LogInWindow : Window, INotifyPropertyChanged
    {
        private string id = "";
        public string ID
        {
            get { return id; }
            set
            {
                id = value;
                PropertyChanged(this, new PropertyChangedEventArgs("ID"));
            }
        }

        private string info = "";
        public string Info
        {
            get { return info; }
            set
            {
                info = value;
                PropertyChanged(this, new PropertyChangedEventArgs("Info"));
            }
        }

        private ICommand connectCommand;
        public ICommand ConnectCommand
        {
            get
            {
                return (connectCommand) ?? (connectCommand = new Command(ConnectCommandExecute));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public LogInWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            this.Loaded += (sender, e) =>
            {
                Info = string.Format(
                    " {0}\n" + " {1}",
                    GitHubManager.OWNER,
                    GitHubManager.REPO_NAME);
            };
        }

        private async void ConnectCommandExecute(object parameter)
        {
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox.Password;

            if (ID == string.Empty)
                return;

            if (password == string.Empty)
                return;

            var msg = await GitHubManager.Instance.GetCurrentUser(ID, password);

            if (GitHubManager.Instance.IsGetUserSuccess)
            {
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                mainWindow.Focus();

                this.Close();
            }
            else MessageBox.Show(this, msg, "", MessageBoxButton.OK);
        }
    }


}
