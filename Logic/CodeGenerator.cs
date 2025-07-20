using ExcelDataReader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Excel_To_SQLite_WPF.Logic
{
    public class CodeGenerator
    {
        public string CodePath { get; }
        public List<string> GeneratedFilePaths { get; } = new List<string>();

        public CodeGenerator(string codePath)
        {
            CodePath = codePath;
        }

        public void Generate(string dbName, string[] fieldNames, int fieldCount, Dictionary<string, string> enumDic, IExcelDataReader reader)
        {
            var codeFullPath = Path.Combine(CodePath, $"{dbName}.cs");
            GeneratedFilePaths.Add(codeFullPath);

            var classSb = new StringBuilder();
            classSb.Append("using Newtonsoft.Json;\n");
            classSb.Append("using System.Collections.Generic;\n\n");
            classSb.Append("namespace Excel_To_SQLite_WPF.Data\n{\n");
            classSb.Append($"    public class {dbName} : ICustomData\n    {{\n");
            classSb.Append(GetCreateCodeStr(reader, dbName, fieldNames, fieldCount));
            classSb.Append("\n    }");
            classSb.Append("\n}");

            File.WriteAllText(codeFullPath, classSb.ToString());
        }

        public void GenerateEnums(Dictionary<string, string> enumDic)
        {
            if (enumDic.Count == 0)
                return;

            var enumSb = new StringBuilder();
            var enumFullPath = Path.Combine(CodePath, "Enums.cs");
            GeneratedFilePaths.Add(enumFullPath);

            enumSb.Append("namespace Excel_To_SQLite_WPF.Data\n{");

            foreach (var kv in enumDic)
            {
                enumSb.AppendLine($"\n{kv.Value}");
                enumSb.AppendLine($"    }}");
            }

            enumSb.Append("\n}");

            File.WriteAllText(enumFullPath, enumSb.ToString());
        }

        private string GetCreateCodeStr(IExcelDataReader reader, string dbName, string[] fieldNames, int fieldCount)
        {
            var sb = new StringBuilder();
            var menbers = new Dictionary<string, List<int>>();

            for (int i = 0; i < fieldCount; i++)
            {
                var split = fieldNames[i].Split(':');
                var fieldName = split[0];

                if (!menbers.ContainsKey(fieldName))
                    menbers.Add(fieldName, new List<int>());

                menbers[fieldName].Add(i);
            }

            foreach (var menber in menbers)
            {
                var index = menber.Value.First();
                var split = fieldNames[index].Split(':');

                var fieldName = split[0];
                var fieldType = split[1];

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
                    sb.AppendLine($"        public {fieldType} {fieldName} {{ get; set; }}");
                }
            }

            sb.AppendLine($"\n        public {dbName}() {{ }}");

            sb.Append($"\n        public {dbName}(");

            var tempList = new List<string>();
            foreach (var menber in menbers)
            {
                var index = menber.Value.First();
                var split = fieldNames[index].Split(':');

                var fieldName = split[0];
                var fieldType = split[1];

                if (fieldType == "enum")
                    fieldType = fieldName;

                if (menber.Value.Count > 1)
                {
                    fieldType = "string";
                }

                var lowerFieldName = Regex.Replace(fieldName, "^[A-Z]", m => m.Value.ToLower());
                tempList.Add($"{fieldType} {lowerFieldName}");
            }

            sb.Append(string.Join(", ", tempList));
            sb.AppendLine(")");
            sb.AppendLine("        {");

            foreach (var menber in menbers)
            {
                var index = menber.Value.First();
                var fieldName = fieldNames[index].Split(':')[0];
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
                    var index = menber.Value.First();
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

            return sb.ToString();
        }
    }
}