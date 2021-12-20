using Excel_To_SQLite_WPF.Repository;
using System.ComponentModel;
using System.IO;
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

        private string token = "";
        public string Token
        {
            get { return token; }
            set
            {
                token = value;
                PropertyChanged(this, new PropertyChangedEventArgs("Token"));
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

        private Visibility githubContentEnable;
        public Visibility GithubContentEnable
        {
            get { return githubContentEnable; }
            set
            {
                githubContentEnable = value;
                PropertyChanged(this, new PropertyChangedEventArgs("GithubContentEnable"));
            }
        }

        private Visibility bitbucketContentEnable;
        public Visibility BitbucketContentEnable
        {
            get { return bitbucketContentEnable; }
            set
            {
                bitbucketContentEnable = value;
                PropertyChanged(this, new PropertyChangedEventArgs("BitbucketContentEnable"));
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

            this.DataContext = this;
            this.Loaded += (sender, e) =>
            {
                RepoTypeCombo.Items.Add("Github");
                RepoTypeCombo.Items.Add("Bitbucket");

                var fileInfo = new FileInfo(".save");
                if (fileInfo.Exists)
                {
                    var file = fileInfo.OpenText();
                    var str = file.ReadToEnd();
                    var array = str.Split('/');

                    ID = array[0];
                    TextBox_Password.Password = array[1];
                    Token = array[2];
                    RepoTypeCombo.SelectedIndex = int.Parse(array[3]);
                }
            };
            this.isWorking = false;
        }

        private async void ConnectCommandExecute(object parameter)
        {
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox.Password;

            if (isWorking)
                return;

            isWorking = true;

            var instance = RepositoryManager.GetManager();
            var msg = await instance.GetCurrentUser(Token, ID, password);

            isWorking = false;

            if (instance.IsGetUserSuccess)
            {
                if (RememberCheckBox.IsChecked.Value)
                {
                    var fileInfo = new FileInfo(".save");
                    if (fileInfo.Exists)
                    {
                        fileInfo.Delete();
                    }

                    var file = fileInfo.CreateText();
                    file.Write(
                        ID + "/" + 
                        password + "/" + 
                        Token + "/" + 
                        RepoTypeCombo.SelectedIndex);

                    file.Close();
                }

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

        private void RepoTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool gitHubEnable = RepoTypeCombo.SelectedIndex == 0;

            if (gitHubEnable)
            {
                GithubContentEnable = Visibility.Visible;
                BitbucketContentEnable = Visibility.Hidden;

                RepositoryManager.SetManager(new GitHubManager());                
            }
            else
            {
                GithubContentEnable = Visibility.Hidden;
                BitbucketContentEnable = Visibility.Visible;

                RepositoryManager.SetManager(new BitbucketManager());
            }

            Info = string.Format(" {0}\n" + " {1}",
                    RepositoryManager.GetManager().OwnerSpaceName,
                    RepositoryManager.GetManager().RepositoryName);
        }
    }
}
