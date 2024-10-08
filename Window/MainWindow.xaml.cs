﻿using ExcelDataReader;
using Microsoft.Win32;
using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Excel_To_SQLite_WPF
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
        private List<string> fileList = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            this.Loaded += (sender, e) =>
            {
                UserName = Repository.RepositoryManager.GetManager().GetUserName;
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
            fileList.Clear();

            var dbPath = GetDirectoryPath("db");
            var codePath = GetDirectoryPath("code");

            foreach (var path in excelFileArray)
            {
                try
                {
                    using (var stream = File.Open(path, System.IO.FileMode.Open, FileAccess.Read))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(path);
                        var dbFilePath = string.Format("{0}/{1}.db", dbPath, fileName);
                        fileList.Add(dbFilePath);

                        var options = new SQLiteConnectionString(dbFilePath,
                           SQLiteOpenFlags.Create |
                           SQLiteOpenFlags.FullMutex |
                           SQLiteOpenFlags.ReadWrite,
                           true,
                           key: "your_password");

                        conn = new SQLiteConnection(options);

                        using (var reader = ExcelReaderFactory.CreateReader(stream))
                        {
                            int sheetCount = 0;
                            var insertQuery = new List<string>();

                            do
                            {
                                loadingCount = 0;
                                loadingCountMax = 0;

                                string dbName = fileName;
                                string createTableQuery = string.Empty;

                                int fieldCount = 0;
                                string[] fieldName = null;

                                insertQuery.Clear();

                                if (isMultiSheet.IsChecked.Value)
                                {
                                    sheetCount += 1;
                                    dbName = string.Format("{0}{1}", fileName, sheetCount);
                                }

                                var codeFullPath = $"{codePath}/{dbName}.cs";
                                var sb = new StringBuilder();
                                sb.Append("namespace Excel_To_SQLite_WPF.Data\n{\n");
                                sb.Append($"    public class {dbName} : ICustomData\n    {{\n");

                                fileList.Add(codeFullPath);

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
                                        //필드 검증 (데이터가 없는데 필드로 잡힌 경우가 있었음..)
                                        for (int i = 0; i < reader.FieldCount; i++)
                                        {
                                            if (reader.GetString(i) != null)
                                            {
                                                fieldCount++;
                                            }
                                        }

                                        fieldName = new string[fieldCount];
                                        for (int i = 0; i < fieldCount; i++)
                                        {
                                            fieldName[i] = reader.GetString(i);
                                        }
                                    }
                                    else
                                    {
                                        if (createTableQuery == string.Empty)
                                        {
                                            //create table 쿼리 작성
                                            createTableQuery = GetCreateTableQuery(reader, fieldName, fieldCount);

                                            //데이터 클래스 작성
                                            sb.Append(GetCreateCodeStr(reader, dbName, fieldName, fieldCount));

                                            loadingCount++;
                                        }

                                        //insert 쿼리 작성                                    
                                        insertQuery.Add(GetInsertQuery(reader, fieldCount));
                                        loadingCount++;
                                    }
                                }

                                sb.Append("\n}");
                                loadingCountMax = loadingCount + 1;
                                loadingCount = 0;

                                await Task.Run(() =>
                                {
                                    //테이블 생성 실행
                                    ExecuteCreateTableQuery(conn, dbName, createTableQuery);

                                    //테이블 데이터 모두 삭제
                                    conn.Execute(string.Format("DELETE FROM {0}", dbName));

                                    //데이터 삽입 실행
                                    ExecuteInsertQuery(conn, dbName, insertQuery);

                                    //데이터 클래스 생성
                                    File.WriteAllText(codeFullPath, sb.ToString());
                                });

                            } while (isMultiSheet.IsChecked.Value && reader.NextResult()); //다음 시트로 이동
                        }

                        conn.Close();
                        stream.Close();
                    }
                }
                catch (Exception e)
                {
                    ErrorLabel = e.Message;
                    fileList.Clear();
                    isWorking = false;
                    return;
                }
            }

            conn.Dispose();
            conn = null;

            EndWork("Excel To SQLite Done!");
        }

        private string GetDirectoryPath(string path)
        {
            var dbPath = string.Format("{0}/{1}", Directory.GetCurrentDirectory(), path);

            var di = new DirectoryInfo(dbPath);
            if (!di.Exists)
            {
                di.Create();
            }

            return dbPath;
        }

        private string GetCreateTableQuery(IExcelDataReader reader, string[] fieldName, int fieldCount)
        {
            string query = string.Empty;

            for (int i = 0; i < fieldCount; i++)
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

                var type = GetTableValueType(value);
                query = string.Concat(query, string.Format("{0} {1} NOT NULL", fieldName[i], type));
            }

            return query;
        }

        private string GetCreateCodeStr(IExcelDataReader reader, string dbName, string[] fieldName, int fieldCount)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < fieldCount; i++)
            {
                var value = reader.GetValue(i);
                if (value == null)
                {
                    //TODO : 예외 알림 처리 필요
                    continue;
                }

                sb.AppendLine($"        public {GetPropertyValueType(value)} {fieldName[i]} {{ get; set; }}");
            }

            sb.AppendLine($"\n        public {dbName}() {{ }}");

            sb.Append($"\n        public {dbName}(");
            for (int i = 0; i < fieldCount; i++)
            {
                var value = reader.GetValue(i);
                if (value == null)
                {
                    //TODO : 예외 알림 처리 필요
                    continue;
                }

                sb.Append($"{GetPropertyValueType(value)} {fieldName[i]}");

                if (i != fieldCount - 1)
                    sb.Append($", ");
                else
                    sb.AppendLine($")");
            }

            sb.AppendLine("        {");
            for (int i = 0; i < fieldCount; i++)
            {
                var value = reader.GetValue(i);
                if (value == null)
                {
                    //TODO : 예외 알림 처리 필요
                    continue;
                }

                sb.AppendLine($"            this.{fieldName[i]} = {fieldName[i]};");
            }
            sb.AppendLine("        }");

            sb.Append("    }");
            return sb.ToString();
        }

        private string GetInsertQuery(IExcelDataReader reader, int fieldCount)
        {
            string query = "(";
            for (int i = 0; i < fieldCount; i++)
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

        private void ExecuteCreateTable(SQLiteConnection conn, string dbName, string createTableHolders)
        {
            Label = string.Format("Create {0} Table", dbName);

            var sql = string.Format("CREATE TABLE {0} ({1})", dbName, createTableHolders);
            var command = new SQLiteCommand(conn)
            {
                CommandText = sql
            };

            command.ExecuteNonQuery();
        }

        private void ExecuteDropTable(SQLiteConnection conn, string dbName)
        {
            Label = string.Format("Drop {0} Table", dbName);

            var sql = string.Format("DROP TABLE {0}", dbName);
            var command = new SQLiteCommand(conn)
            {
                CommandText = sql
            };

            command.ExecuteNonQuery();
        }

        private void ExecuteCreateTableQuery(SQLiteConnection conn, string dbName, string createTableHolders)
        {
            try
            {
                ExecuteCreateTable(conn, dbName, createTableHolders);
            }
            catch
            {
                //실패시 테이블 드랍하고 다시만듬
                ExecuteDropTable(conn, dbName);
                ExecuteCreateTable(conn, dbName, createTableHolders);
            }
            finally
            {
                loadingCount++;
                CurrentProgress = (int)((loadingCount / loadingCountMax) * 100.0f);
            }
        }

        private void ExecuteInsertQuery(SQLiteConnection conn, string dbName, List<string> insertQuery)
        {
            foreach (var query in insertQuery)
            {
                Label = string.Format("Insert {0}", query);

                var sql = string.Format("INSERT INTO {0} VALUES {1}", dbName, query);
                var command = new SQLiteCommand(conn)
                {
                    CommandText = sql
                };

                command.ExecuteNonQuery();

                loadingCount++;
                CurrentProgress = (int)((loadingCount / loadingCountMax) * 100.0f);
            }
        }

        private string GetTableValueType(object value)
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

        private string GetPropertyValueType(object value)
        {
            if (value != null)
            {
                int intParse = 0;
                if (int.TryParse(value.ToString(), out intParse))
                {
                    return "int";
                }

                float floatParse = 0;
                if (float.TryParse(value.ToString(), out floatParse))
                {
                    return "float";
                }
            }

            return "string";
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
                return;

            if (fileList.Count <= 0)
                return;

            if (isWorking)
                return;

            var instance = Repository.RepositoryManager.GetManager();
            if (instance.IsGetUserSuccess)
            {
                StartWork("Upload Start!");

                Action<string> updateLabel = (str) => Label = str;
                Action<float, float> updateProgress = (v1, v2) => CurrentProgress = (int)((v1 / v2) * 100.0f);

                instance.SetUnityPath(isUnity.IsChecked.Value);

                var versionData = await instance.GetVersionFile(excelFileArray, updateLabel);

                var msg = await instance.CommitProcess(
                    excelFileArray, fileList.ToArray(),
                    versionData, updateLabel, updateProgress);

                if (msg != string.Empty)
                {
                    ErrorLabel = msg;
                    MessageBox.Show(this, "upload error", "", MessageBoxButton.OK);
                }

                EndWork("Upload Done!");
            }
        }

        private async void ClearOldFileClick(object sender, RoutedEventArgs e)
        {
            if (isWorking)
                return;

            var result = MessageBox.Show("이 기능을 실행하면 최신버전 이외의 데이터들이 모두 삭제됩니다.\n진행하시겠습니까?", "경고", MessageBoxButton.YesNo);
            if (!(result == MessageBoxResult.Yes))
                return;

            ErrorLabel = string.Empty;

            var instance = Repository.RepositoryManager.GetManager();
            if (instance.IsGetUserSuccess)
            {
                StartWork("Clear Start!");

                Action<string> updateLabel = (str) => Label = str;
                Action<float, float> updateProgress = (v1, v2) => CurrentProgress = (int)((v1 / v2) * 100.0f);

                instance.SetUnityPath(isUnity.IsChecked.Value);

                var versionData = await instance.GetVersionFile(excelFileArray, updateLabel);

                var msg = await instance.ClearProcess(versionData, updateLabel, updateProgress);

                if (msg != string.Empty)
                {
                    ErrorLabel = msg;
                    MessageBox.Show(this, "clear error", "", MessageBoxButton.OK);
                }

                EndWork("Clear Done!");
            }
        }
    }
}
