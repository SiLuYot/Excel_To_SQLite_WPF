using ExcelDataReader;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace GithubExcel2SQLiteTool
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private double currentProgress;
        public double CurrentProgress
        {
            get { return this.currentProgress; }
            set
            {
                currentProgress = value;
                PropertyChanged(this, new PropertyChangedEventArgs("CurrentProgress"));
            }
        }

        private string label = "waiting..";
        public string Label
        {
            get { return label; }
            set
            {
                label = value;
                PropertyChanged(this, new PropertyChangedEventArgs("Label"));
            }
        }

        private string errorLabel = "";
        public string ErrorLabel
        {
            get { return errorLabel; }
            set
            {
                errorLabel = value;
                PropertyChanged(this, new PropertyChangedEventArgs("ErrorLabel"));
            }
        }

        private string userName = "";
        public string UserName
        {
            get { return userName; }
            set
            {
                userName = value;
                PropertyChanged(this, new PropertyChangedEventArgs("UserName"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private float loadingCount = 0;
        private float loadingCountMax = 0;
        private bool isWorking = false;
        private string[] excelFileArray = null;
        private List<string> dbFileList = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            this.Loaded += (sender, e) =>
            {
                UserName = GitHubManager.Instance.GetUser.Name;
            };
        }

        private void StartWork(string msg)
        {
            Label = msg;
            CurrentProgress = 0.0f;
            isWorking = true;
        }

        private void EndWork(string msg)
        {
            Label = msg;
            CurrentProgress = 100.0f;
            isWorking = false;
        }

        private string[] FileExplorer()
        {
            OpenFileDialog dig = new OpenFileDialog();
            dig.Multiselect = true;
            dig.Filter = "xlsx files (*.xlsx)|*.xlsx|All files (*.*)|*.*";

            bool? result = dig.ShowDialog();
            if (result == true)
            {
                return dig.FileNames;
            }
            else return null;
        }

        private async void ExcelToSQLite()
        {
            StartWork("Excel To SQLite Start!");

            SQLiteConnection conn = null;
            dbFileList.Clear();

            var dbPath = GetDirectoryPath();
            foreach (var path in excelFileArray)
            {
                try
                {
                    using (var stream = File.Open(path, System.IO.FileMode.Open, FileAccess.Read))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(path);
                        var dbFilePath = string.Format("{0}/{1}.db", dbPath, fileName);
                        dbFileList.Add(dbFilePath);

                        SQLiteConnection.CreateFile(dbFilePath);
                        conn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", dbFilePath));
                        conn.Open();

                        using (var reader = ExcelReaderFactory.CreateReader(stream))
                        {
                            int sheetCount = 0;
                            List<string> insertQuery = new List<string>();

                            do
                            {
                                loadingCount = 0;
                                loadingCountMax = 0;

                                string dbName = fileName;
                                string createTableQuery = string.Empty;

                                string[] fieldName = null;
                                insertQuery.Clear();

                                if (isMultiSheet.IsChecked == true)
                                {
                                    sheetCount += 1;
                                    dbName = string.Format("{0}{1}", fileName, sheetCount);
                                }

                                while (reader.Read())
                                {
                                    //null이면 종료
                                    if (reader.IsDBNull(0))
                                    {
                                        break;
                                    }

                                    //필드 이름 세팅
                                    if (reader.Depth == 0)
                                    {
                                        fieldName = new string[reader.FieldCount];
                                        for (int i = 0; i < reader.FieldCount; i++)
                                        {
                                            fieldName[i] = reader.GetString(i);
                                        }
                                    }
                                    else
                                    {
                                        if (createTableQuery == string.Empty)
                                        {
                                            //create table 쿼리 작성
                                            createTableQuery = GetCreateTableQuery(reader, fieldName);
                                            loadingCount++;
                                        }

                                        //insert 쿼리 작성                                    
                                        insertQuery.Add(GetInsertQuery(reader));
                                        loadingCount++;
                                    }
                                }

                                loadingCountMax = loadingCount;
                                loadingCount = 0;

                                await Task.Run(() =>
                                {
                                    //테이블 생성 실행
                                    ExecuteCreateTableQuery(conn, dbName, createTableQuery);

                                    //데이터 삽입 실행
                                    ExecuteInsertQuery(conn, dbName, insertQuery);
                                });

                            } while (isMultiSheet.IsChecked == true && reader.NextResult()); //다음 시트로 이동
                        }

                        conn.Close();
                        stream.Close();
                    }
                }
                catch (Exception e)
                {
                    ErrorLabel = e.Message;
                    dbFileList.Clear();
                    isWorking = false;
                    return;
                }
            }

            conn.Dispose();
            conn = null;

            EndWork("Excel To SQLite Done!");
        }

        private string GetDirectoryPath()
        {
            var dbPath = string.Format("{0}/db", Directory.GetCurrentDirectory());

            var di = new DirectoryInfo(dbPath);
            if (!di.Exists)
            {
                di.Create();
            }

            return dbPath;
        }

        private string GetCreateTableQuery(IExcelDataReader reader, string[] fieldName)
        {
            string query = string.Empty;

            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (query != string.Empty)
                {
                    query = string.Concat(query, ", ");
                }

                var value = reader.GetValue(i);
                if (value == null)
                {
                    //예외 알림 처리 필요
                }

                var type = GetValueType(value);
                query = string.Concat(query, string.Format("{0} {1} NOT NULL", fieldName[i], type));
            }

            return query;
        }

        private string GetInsertQuery(IExcelDataReader reader)
        {
            string query = "(";
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (query.Contains(")"))
                {
                    query = query.Replace(")", ", ");
                }

                var value = reader.GetValue(i);
                query = string.Concat(query, string.Format("'{0}')", value));
            }
            return query;
        }

        private void ExecuteCreateTableQuery(SQLiteConnection conn, string dbName, string createTableHolders)
        {
            Label = string.Format("Create {0} Table", dbName);

            var sql = string.Format("CREATE TABLE {0} ({1})", dbName, createTableHolders);
            var command = new SQLiteCommand(sql, conn);

            var result = command.ExecuteNonQuery();

            loadingCount++;
            CurrentProgress = (int)((loadingCount / loadingCountMax) * 100.0f);
        }

        private void ExecuteInsertQuery(SQLiteConnection conn, string dbName, List<string> insertQuery)
        {
            foreach (var query in insertQuery)
            {
                Label = string.Format("Insert {0}", query);

                var sql = string.Format("INSERT INTO {0} VALUES {1}", dbName, query);
                var command = new SQLiteCommand(sql, conn);

                var result = command.ExecuteNonQuery();

                loadingCount++;
                CurrentProgress = (int)((loadingCount / loadingCountMax) * 100.0f);
            }
        }

        private string GetValueType(object value)
        {
            if (value != null)
            {
                int intParse = 0;
                if (int.TryParse(value.ToString(), out intParse))
                {
                    return "INTEGER";
                }

                float floatParse = 0;
                if (float.TryParse(value.ToString(), out floatParse))
                {
                    return "REAL";
                }
            }

            return "TEXT";
        }

        private void OpenButtonClick(object sender, RoutedEventArgs e)
        {
            if (isWorking)
            {
                return;
            }

            excelFileArray = FileExplorer();
            excelList.ItemsSource = excelFileArray;
        }

        private void ExcelToSQLiteButtonClick(object sender, RoutedEventArgs e)
        {
            ErrorLabel = string.Empty;

            if (excelFileArray == null)
            {
                return;
            }

            if (isWorking)
            {
                return;
            }

            ExcelToSQLite();
        }

        private async void UploadButtonClick(object sender, RoutedEventArgs e)
        {
            if (excelFileArray == null)
            {
                return;
            }

            if (dbFileList.Count <= 0)
            {
                return;
            }

            if (isWorking)
            {
                return;
            }

            if (GitHubManager.Instance.IsGetUserSuccess)
            {
                StartWork("Upload Start!");

                string msg = string.Empty;

                Action<string> updateLabel = (str) => Label = str;
                Action<float, float> updateProgress = (v1, v2) => CurrentProgress = (int)((v1 / v2) * 100.0f);

                var versionData = await GitHubManager.Instance.GetVersionFile(excelFileArray, updateLabel);

                msg = string.Empty;
                msg = await GitHubManager.Instance.Commit_Base64(
                    excelFileArray,
                    versionData,
                    updateLabel,
                    updateProgress);

                if (msg != string.Empty)
                {
                    ErrorLabel = msg;
                    MessageBox.Show(this, "upload excel error", "", MessageBoxButton.OK);
                }

                msg = string.Empty;
                msg = await GitHubManager.Instance.Commit_Base64(
                    dbFileList.ToArray(),
                    versionData,
                    updateLabel,
                    updateProgress);

                if (msg != string.Empty)
                {
                    ErrorLabel = msg;
                    MessageBox.Show(this, "upload db error", "", MessageBoxButton.OK);
                }

                await GitHubManager.Instance.UploadVersionFile(versionData, updateLabel);

                EndWork("Upload Done!");
            }
        }
    }
}
