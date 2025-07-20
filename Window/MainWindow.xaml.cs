using Excel_To_SQLite_WPF.Logic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace Excel_To_SQLite_WPF
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private double _currentProgress;
        public double CurrentProgress
        {
            get { return _currentProgress; }
            set
            {
                _currentProgress = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentProgress)));
            }
        }

        private string _label = "waiting..";
        public string Label
        {
            get { return _label; }
            set
            {
                _label = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
            }
        }

        private string _errorLabel = "";
        public string ErrorLabel
        {
            get { return _errorLabel; }
            set
            { 
                _errorLabel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorLabel)));
            }
        }

        private string _userName = "";
        public string UserName
        {
            get { return _userName; }
            set
            { 
                _userName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UserName)));
            }
        }

        private bool _isWorking = false;
        private string[] _excelFileArray = null;
        private List<string> _fileList = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += (sender, e) =>
            {
                UserName = Repository.RepositoryManager.GetManager()?.GetUserName;
            };
        }

        private void StartWork(string msg)
        {
            Label = msg;
            CurrentProgress = 0.0f;
            _isWorking = true;
        }

        private void EndWork(string msg)
        {
            Label = msg;
            CurrentProgress = 100.0f;
            _isWorking = false;
        }

        private void OpenButtonClick(object sender, RoutedEventArgs e)
        { 
            if (_isWorking) return;

            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "xlsx files (*.xlsx)|*.xlsx|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _excelFileArray = dialog.FileNames;
                excelList.ItemsSource = _excelFileArray;
            }
        }

        private async void ExcelToSQLiteButtonClick(object sender, RoutedEventArgs e)
        {
            if (_excelFileArray == null || !_excelFileArray.Any() || _isWorking)
                return;

            ErrorLabel = string.Empty;
            StartWork("Excel To SQLite Start!");

            try
            {
                var dbPath = GetDirectoryPath("db");
                var codePath = GetDirectoryPath("code");

                var codeGenerator = new CodeGenerator(codePath);
                var dbManager = new DatabaseManager(dbPath);
                var excelProcessor = new ExcelProcessor();

                Repository.RepositoryManager.GetManager().SetUnityPath(isUnity.IsChecked == true);

                var remoteEnumPath = $"{Repository.RepositoryManager.GetManager().CodePath}/Enums.cs";
                var fileContent = await Repository.RepositoryManager.GetManager().GetFileContent(remoteEnumPath);

                if (fileContent != null && fileContent.StartsWith("ERROR:"))
                {
                    ErrorLabel = fileContent;
                    return;
                }

                if (!string.IsNullOrEmpty(fileContent))
                {
                    var pattern = @"public\s+enum\s+([^\s{]+)\s*{([\s\S]*?)\s*}";
                    var matches = Regex.Matches(fileContent, pattern);

                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count == 3)
                        {
                            var enumName = match.Groups[1].Value.Trim();
                            var enumMembers = match.Groups[2].Value;
                            var key = $"{enumName}:enum";
                            if (excelProcessor.EnumDic.ContainsKey(key)) continue;

                            var valueBuilder = new System.Text.StringBuilder();
                            valueBuilder.AppendLine($"    public enum {enumName}");
                            valueBuilder.Append("    {");
                            valueBuilder.Append(enumMembers);

                            excelProcessor.EnumDic.Add(key, valueBuilder.ToString());
                        }
                    }
                }

                foreach (var path in _excelFileArray)
                {
                    excelProcessor.Process(path, isMultiSheet.IsChecked == true, codeGenerator, dbManager);
                }

                codeGenerator.GenerateEnums(excelProcessor.EnumDic);

                _fileList.Clear();
                _fileList.AddRange(dbManager.GeneratedFilePaths);
                _fileList.AddRange(codeGenerator.GeneratedFilePaths);

                EndWork("Excel To SQLite Done!");
            }
            catch (Exception ex)
            {
                ErrorLabel = ex.Message;
                _isWorking = false;
            }
        }

        private async void UploadButtonClick(object sender, RoutedEventArgs e)
        {
            if (_fileList == null || !_fileList.Any() || _isWorking)
                return;

            var instance = Repository.RepositoryManager.GetManager();
            if (instance.IsGetUserSuccess)
            {
                StartWork("Upload Start!");

                Action<string> updateLabel = (str) => Label = str;
                Action<float, float> updateProgress = (v1, v2) => CurrentProgress = (int)((v1 / v2) * 100.0f);

                instance.SetUnityPath(isUnity.IsChecked == true);

                var msg = await instance.CommitProcess(
                    _excelFileArray,
                    _fileList.ToArray(),
                    updateLabel,
                    updateProgress);

                if (!string.IsNullOrEmpty(msg))
                {
                    ErrorLabel = msg;
                    MessageBox.Show(this, "upload error", "", MessageBoxButton.OK);
                }

                EndWork("Upload Done!");
            }
        }

        private string GetDirectoryPath(string path)
        {
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            return fullPath;
        }
    }
}