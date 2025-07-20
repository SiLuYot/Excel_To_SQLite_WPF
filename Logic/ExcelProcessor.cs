using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Excel_To_SQLite_WPF.Logic
{
    public class ExcelProcessor
    {
        public async Task Process(string path, bool isMultiSheet, Dictionary<string, string> enumDic, Action executeQuery, CodeGenerator codeGenerator, DatabaseManager dbManager)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    var conn = dbManager.CreateConnection(fileName);

                    int sheetCount = 0;
                    do
                    {
                        string dbName = isMultiSheet ? $"{fileName}{++sheetCount}" : fileName;

                        reader.Read();

                        int fieldCount = 0;

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            if (reader.GetString(i) != null)
                            {
                                fieldCount++;
                            }
                        }

                        var fieldNames = new string[fieldCount];
                        for (int i = 0; i < fieldCount; i++)
                        {
                            fieldNames[i] = reader.GetString(i);
                        }

                        var createTableQuery = GetCreateTableQuery(reader, dbName, fieldNames, fieldCount);
                        var insertQueries = new List<string>();

                        while (reader.Read())
                        {
                            if (reader.IsDBNull(0))
                                break;

                            insertQueries.Add(GetInsertQuery(reader, fieldNames, fieldCount));
                            SetEnumStr(reader, dbName, fieldNames, fieldCount, enumDic);
                        }

                        GetCreateEnumStr(reader, dbName, fieldNames, fieldCount, enumDic);
                        codeGenerator.Generate(dbName, fieldNames, fieldCount, enumDic, reader);

                        await dbManager.ExecuteQuery(conn, dbName, createTableQuery, insertQueries);

                    } while (isMultiSheet && reader.NextResult());

                    conn.Close();
                    conn.Dispose();
                }
            }

            executeQuery?.Invoke();
        }

        private string GetCreateTableQuery(IExcelDataReader reader, string dbName, string[] fieldNames, int fieldCount)
        {
            string query = string.Empty;
            var processedFields = new HashSet<string>();

            for (int i = 0; i < fieldCount; i++)
            {
                var split = fieldNames[i].Split(':');
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

                query = string.Concat(query, $"{fieldName} {type} NOT NULL");
            }

            return query;
        }

        private string GetInsertQuery(IExcelDataReader reader, string[] fieldNames, int fieldCount)
        {
            string query = "(";
            var processedFields = new HashSet<string>();

            for (int i = 0; i < fieldCount; i++)
            {
                var fieldName = fieldNames[i].Split(':')[0];
                if (processedFields.Contains(fieldName)) continue;

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

                    query += $"'{Newtonsoft.Json.JsonConvert.SerializeObject(arrayValues)}'";
                }
                else
                {
                    query += $"'{reader.GetValue(i)}'";
                }
            }
            query += ")";
            return query;
        }

        private void GetCreateEnumStr(IExcelDataReader reader, string dbName, string[] fieldNames, int fieldCount, Dictionary<string, string> enumDic)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < fieldCount; i++)
            {
                var split = fieldNames[i].Split(':');
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
                    var enumValueRaw = reader.GetValue(i);
                    if (enumValueRaw == null)
                        continue;

                    var enumValue = enumValueRaw.ToString();
                    if (string.IsNullOrEmpty(enumValue) || str.Contains(enumValue))
                        continue;

                    sb.Append(str);
                    sb.AppendLine($"        {enumValue},");

                    dic[fieldNames[i]] = sb.ToString();
                    sb.Clear();
                }
            }
        }

        private string GetTableValueType(object value)
        {
            if (value != null)
            {
                if (int.TryParse(value.ToString(), out _))
                {
                    return "INTEGER";
                }

                if (float.TryParse(value.ToString(), out _))
                {
                    return "REAL";
                }
            }

            return "TEXT";
        }
    }
}