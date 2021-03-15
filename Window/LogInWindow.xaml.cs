using Excel_To_SQLite_WPF.GitRespositoryManager;
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
                return connectCommand ?? (connectCommand = new Command(ConnectCommandExecute));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool isWorking;

        public LogInWindow()
        {
            InitializeComponent();

            //RespositoryManager.SetManager(new GitHubManager());
            RespositoryManager.SetManager(new BitbucketManager());

            this.DataContext = this;
            this.Loaded += (sender, e) =>
            {
                Info = string.Format(" {0}\n" + " {1}",
                    RespositoryManager.GetManager().OwnerSpaceName,
                    RespositoryManager.GetManager().RepositoryName);
            };
            this.isWorking = false;
        }

        private async void ConnectCommandExecute(object parameter)
        {
            if (isWorking)
                return;

            isWorking = true;

            var passwordBox = parameter as PasswordBox;
            var password = passwordBox.Password;

            if (ID == string.Empty)
                return;

            if (password == string.Empty)
                return;
            
            var instance = RespositoryManager.GetManager();

            var msg = await instance.GetCurrentUser(ID, password);

            isWorking = false;

            if (instance.IsGetUserSuccess)
            {
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                mainWindow.Focus();

                this.Close();
            }
            else MessageBox.Show(this, msg, "", MessageBoxButton.OK);
        }

        private void TextBox_Password_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConnectCommandExecute(TextBox_Password);
            }
        }
    }


}
