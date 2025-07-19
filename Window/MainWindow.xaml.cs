using ExcelDataReader;
using Microsoft.Win32;
using SQLite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

            var iCustomDataPath = $"{codePath}/ICustomData.cs";
            var iCustomDataContent = @"namespace Excel_To_SQLite_WPF.Data
{
    public interface ICustomData
    {
        int Id { get; }
        string Name { get; }
        void OnLoaded();
    }
}";
            File.WriteAllText(iCustomDataPath, iCustomDataContent);
            fileList.Add(iCustomDataPath);

            var enumDic = new Dictionary<string, string>();

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
                                string[] fieldNames = null;

                                insertQuery.Clear();

                                if (isMultiSheet.IsChecked.Value)
                                {
                                    sheetCount += 1;
                                    dbName = string.Format("{0}{1}", fileName, sheetCount);
                                }

                                var codeFullPath = $"{codePath}/{dbName}.cs";
                                fileList.Add(codeFullPath);

                                var classSb = new StringBuilder();
                                classSb.Append("using Newtonsoft.Json;\n");
                                classSb.Append("using System.Collections.Generic;\n\n");
                                classSb.Append("namespace Excel_To_SQLite_WPF.Data\n{\n");
                                classSb.Append($"    public class {dbName} : ICustomData\n    {{\n");

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

                                        fieldNames = new string[fieldCount];
                                        for (int i = 0; i < fieldCount; i++)
                                        {
                                            fieldNames[i] = reader.GetString(i);
                                        }
                                    }
                                    else
                                    {
                                        if (createTableQuery == string.Empty)
                                        {
                                            //create table 쿼리 작성
                                            createTableQuery = GetCreateTableQuery(reader, dbName, fieldNames, fieldCount);

                                            //데이터 클래스 작성
                                            classSb.Append(GetCreateCodeStr(reader, dbName, fieldNames, fieldCount));

                                            //Enum 작성 시작
                                            GetCreateEnumStr(reader, dbName, fieldNames, fieldCount, enumDic);

                                            loadingCount++;
                                        }

                                        //insert 쿼리 작성                                    
                                        insertQuery.Add(GetInsertQuery(reader, fieldNames, fieldCount));

                                        //Enum 내용 작성
                                        SetEnumStr(reader, dbName, fieldNames, fieldCount, enumDic);

                                        loadingCount++;
                                    }
                                }

                                classSb.Append("\n}");

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
                                    File.WriteAllText(codeFullPath, classSb.ToString());
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

            if (enumDic.Count > 0)
            {
                var enumSb = new StringBuilder();
                var enumFullPath = $"{codePath}/Enums.cs";
                fileList.Add(enumFullPath);

                enumSb.Append("namespace Excel_To_SQLite_WPF.Data\n{");

                foreach (var kv in enumDic)
                {
                    Label = $"Create {kv.Key} Enum..";
                    enumSb.AppendLine($"\n{kv.Value}    }}");
                }

                enumSb.Append("\n}");

                File.WriteAllText(enumFullPath, enumSb.ToString());
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

        private string GetCreateTableQuery(IExcelDataReader reader, string dbName, string[] fieldNames, int fieldCount)
        {
            string query = string.Empty;
            var processedFields = new HashSet<string>();

            for (int i = 0; i < fieldCount; i++)
            {
                var split = fieldNames[i].Split(':');
                if (split.Length != 2)
                {
                    Label = $"Invaild field - dbName:{dbName} fieldName:{fieldNames[i]}";
                    return string.Empty;
                }

                var fieldName = split[0];
                if (processedFields.Contains(fieldName))
                    continue;

                processedFields.Add(fieldName);

                if (query != string.Empty)
                {
                    query = string.Concat(query, ", ");
                }

                var value = reader.GetValue(i);
                var type = GetTableValueType(value);

                var isArray = fieldNames.Count(f => f.Split(':')[0] == fieldName) > 1;
                if (isArray)
                {
                    type = "TEXT";
                }

                query = string.Concat(query, string.Format("{0} {1} NOT NULL", fieldName, type));
            }

            return query;
        }

        private void GetCreateEnumStr(IExcelDataReader reader, string dbName, string[] fieldNames, int fieldCount, Dictionary<string, string> enumDic)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < fieldCount; i++)
            {
                var split = fieldNames[i].Split(':');
                if (split.Length != 2)
                {
                    Label = $"Invaild field - dbName:{dbName} fieldName:{fieldNames[i]}";
                    return;
                }

                var fieldName = split[0];
                var fieldType = split[1];

                if (fieldType == "enum")
                {
                    if (enumDic.ContainsKey(fieldNames[i]))
                        continue;

                    sb.Append($"    public enum {fieldName}\n    {{\n");
                    enumDic.Add(fieldNames[i], sb.ToString());
                }

                sb.Clear();
            }
        }

        private void SetEnumStr(IExcelDataReader reader, string dbName, string[] fieldNames, int fieldCount, Dictionary<string, string> dic)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < fieldCount; i++)
            {
                if (dic.TryGetValue(fieldNames[i], out var str))
                {
                    var enumValue = reader.GetValue(i).ToString();
                    if (str.Contains(enumValue))
                        continue;

                    sb.Append(str);
                    sb.AppendLine($"        {enumValue},");

                    dic[fieldNames[i]] = sb.ToString();
                    sb.Clear();
                }
            }
        }

        private string GetCreateCodeStr(IExcelDataReader reader, string dbName, string[] fieldNames, int fieldCount)
        {
            var sb = new StringBuilder();
            var menbers = new Dictionary<string, List<int>>();

            for (int i = 0; i < fieldCount; i++)
            {
                var split = fieldNames[i].Split(':');
                if (split.Length != 2)
                {
                    Label = $"Invaild field - dbName:{dbName} fieldName:{fieldNames[i]}";
                    return string.Empty;
                }

                var fieldName = split[0];
                var fieldType = split[1];

                if (!menbers.ContainsKey(fieldName))
                    menbers.Add(fieldName, new List<int>());

                menbers[fieldName].Add(i);
            }

            foreach (var menber in menbers)
            {
                var index = menber.Value.FirstOrDefault();
                var split = fieldNames[index].Split(':');

                var fieldName = split[0];
                var fieldType = split[1];
                var arrayField = string.Empty;

                if (fieldType == "enum")
                    fieldType = fieldName;

                if (menber.Value.Count > 1)
                {
                    var listFieldType = fieldType;
                    if (listFieldType == "enum")
                        listFieldType = fieldName;

                    sb.AppendLine($"        public string {fieldName} {{ get; set; }}");
                    sb.AppendLine($"        public List<{listFieldType}> {fieldName}List {{ get; set; }}");
                }
                else
                {
                    sb.AppendLine($"        public {fieldType}{arrayField} {fieldName} {{ get; set; }}");
                }
            }

            sb.AppendLine($"\n        public {dbName}() {{ }}");

            sb.Append($"\n        public {dbName}(");

            var tempList = new List<string>();
            foreach (var menber in menbers)
            {
                var index = menber.Value.FirstOrDefault();
                var split = fieldNames[index].Split(':');

                var fieldName = split[0];
                var fieldType = split[1];
                var arrayField = string.Empty;

                if (fieldType == "enum")
                    fieldType = fieldName;

                if (menber.Value.Count > 1)
                {
                    fieldType = "string";
                    arrayField = "";
                }

                var lowerFieldName = Regex.Replace(fieldName, "^[A-Z]", m => m.Value.ToLower());
                tempList.Add($"{fieldType}{arrayField} {lowerFieldName}");
            }

            sb.Append(string.Join(", ", tempList));
            sb.AppendLine(")");
            sb.AppendLine("        {");

            foreach (var menber in menbers)
            {
                var index = menber.Value.FirstOrDefault();
                var split = fieldNames[index].Split(':');

                var fieldName = split[0];
                var fieldType = split[1];

                var lowerFieldName = Regex.Replace(fieldName, "^[A-Z]", m => m.Value.ToLower());
                sb.AppendLine($"            this.{fieldName} = {lowerFieldName};");
            }

            sb.AppendLine("        }");

            sb.AppendLine("\n        public void OnLoaded()\n        {");
            foreach (var menber in menbers)
            {
                if (menber.Value.Count > 1)
                {
                    var fieldName = menber.Key;
                    var index = menber.Value.FirstOrDefault();
                    var fieldType = fieldNames[index].Split(':')[1];
                    if (fieldType == "enum")
                        fieldType = fieldName;

                    sb.AppendLine($"            if (!string.IsNullOrEmpty({fieldName}))");
                    sb.AppendLine($"                {fieldName}List = JsonConvert.DeserializeObject<List<{fieldType}>>(this.{fieldName});");
                    sb.AppendLine("            else");
                    sb.AppendLine($"                {fieldName}List = new List<{fieldType}>();");
                }
            }
            sb.AppendLine("        }");

            sb.Append("    }");
            return sb.ToString();
        }

        private string GetInsertQuery(IExcelDataReader reader, string[] fieldNames, int fieldCount)
        {
            string query = "(";
            var processedFields = new HashSet<string>();

            for (int i = 0; i < fieldCount; i++)
            {
                var fieldName = fieldNames[i].Split(':')[0];
                if (processedFields.Contains(fieldName))
                    continue;

                processedFields.Add(fieldName);

                if (query.Length > 1)
                {
                    query += ", ";
                }

                var indices = Enumerable.Range(0, fieldCount)
                                        .Where(j => fieldNames[j].Split(':')[0] == fieldName)
                                        .ToList();

                if (indices.Count > 1)
                {
                    var arrayValues = indices.Select(j => reader.GetValue(j))
                                           .Where(v => v != null && !string.IsNullOrEmpty(v.ToString()))
                                           .Select(v => v.ToString())
                                           .ToList();
                    var jsonArray = Newtonsoft.Json.JsonConvert.SerializeObject(arrayValues);
                    query += string.Format("'{0}'", jsonArray);
                }
                else
                {
                    var value = reader.GetValue(i);
                    query += string.Format("'{0}'", value);
                }
            }
            query += ")";
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

                var msg = await instance.CommitProcess(
                    excelFileArray,
                    fileList.ToArray(),
                    updateLabel,
                    updateProgress);

                if (msg != string.Empty)
                {
                    ErrorLabel = msg;
                    MessageBox.Show(this, "upload error", "", MessageBoxButton.OK);
                }

                EndWork("Upload Done!");
            }
        }
    }
}
