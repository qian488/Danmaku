using System.IO;
using Excel;
using System.Data;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从 Excel 生成 C# 数据类 + JSON 数据文件。
/// Excel 约定：第1行字段名，第2行类型（int/string/bool/int[]/string[]/bool[]），第3行描述，第4行起为数据。
/// 生成路径：
///   .cs  → Assets/Scripts/DataTable/{ClassName}.cs   (namespace DemoFrameWork.Table)
///   .json → Assets/Resources/Tables/{ClassName}.json (运行时由 TableManager 加载)
/// </summary>
public class Excel2CsTool
{
    static string ExcelDataPath = Application.dataPath + "/../ExcelData";
    static string CsClassPath = Application.dataPath + "/Scripts/DataTable";
    static string JsonDataPath = Application.dataPath + "/Resources/Tables";

    [MenuItem("Tools/DemoFrameWork/Data/Excel2Cs+Json")]
    static void Excel2CsAndJson()
    {
        Init();
        GenerateAll();
    }

    [MenuItem("Tools/DemoFrameWork/Data/Excel2Cs")]
    static void Excel2CsOnly()
    {
        Init();
        GenerateAll(jsonExport: false);
    }

    static void Init()
    {
        if (!Directory.Exists(CsClassPath))
            Directory.CreateDirectory(CsClassPath);
        if (!Directory.Exists(JsonDataPath))
            Directory.CreateDirectory(JsonDataPath);
    }

    static void WriteCs(string className, string[] names, string[] types, string[] descs)
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace DemoFrameWork.Table");
            sb.AppendLine("{");
            sb.AppendLine("    [Serializable]");
            sb.AppendLine("    public class " + className);
            sb.AppendLine("    {");
            for (int i = 0; i < names.Length; i++)
            {
                sb.AppendLine("        /// <summary>");
                sb.AppendLine("        /// " + descs[i]);
                sb.AppendLine("        /// </summary>");
                string type = types[i];
                if (type.Contains("[]"))
                {
                    type = type.Replace("[]", "");
                    sb.AppendLine("        public List<" + type + "> " + names[i] + ";");
                }
                else
                {
                    sb.AppendLine("        public " + type + " " + names[i] + ";");
                }
                sb.AppendLine();
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string csPath = Path.Combine(CsClassPath, className + ".cs");
            File.WriteAllText(csPath, sb.ToString(), Encoding.UTF8);
            Debug.Log("[Excel2Cs] 生成 .cs: " + csPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Excel2Cs] 写入 .cs 失败: " + e.Message);
            throw;
        }
    }

    static void WriteJson(string className, string[] names, string[] types, List<string[]> datasList)
    {
        try
        {
            var rows = new List<Dictionary<string, object>>();
            foreach (var row in datasList)
            {
                var dict = new Dictionary<string, object>();
                for (int i = 0; i < names.Length && i < row.Length; i++)
                {
                    dict[names[i]] = ParseValue(row[i], types[i]);
                }
                rows.Add(dict);
            }

            string json = JsonConvert.SerializeObject(rows, Formatting.Indented);
            string jsonPath = Path.Combine(JsonDataPath, className + ".json");
            File.WriteAllText(jsonPath, json, Encoding.UTF8);
            Debug.Log("[Excel2Cs] 生成 .json: " + jsonPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Excel2Cs] 写入 .json 失败: " + e.Message);
            throw;
        }
    }

    static object ParseValue(string raw, string type)
    {
        switch (type.ToLower())
        {
            case "int":
                return int.TryParse(raw, out int iv) ? iv : 0;
            case "bool":
                return raw == "1" || raw.ToLower() == "true";
            case "float":
                return float.TryParse(raw, out float fv) ? fv : 0f;
            case "int[]":
                var iArr = new List<int>();
                foreach (var s in raw.Split(','))
                    if (int.TryParse(s.Trim(), out int ia)) iArr.Add(ia);
                return iArr;
            case "string[]":
                return new List<string>(System.Array.ConvertAll(raw.Split(','), x => x.Trim()));
            default:
                return raw;
        }
    }

    static void GenerateAll(bool jsonExport = true)
    {
        if (!Directory.Exists(ExcelDataPath))
        {
            Debug.LogWarning("[Excel2Cs] ExcelData 目录不存在: " + ExcelDataPath);
            AssetDatabase.Refresh();
            return;
        }

        string[] excelPaths = Directory.GetFiles(ExcelDataPath, "*.xlsx");
        foreach (string excelPath in excelPaths)
        {
            string className;
            string[] names;
            string[] types;
            string[] descs;
            List<string[]> datasList;

            try
            {
                className = Path.GetFileNameWithoutExtension(excelPath).ToLower();
                FileStream fileStream = File.Open(excelPath, FileMode.Open, FileAccess.Read);
                IExcelDataReader reader = ExcelReaderFactory.CreateOpenXmlReader(fileStream);
                DataSet result = reader.AsDataSet();
                reader.Close();

                int columns = result.Tables[0].Columns.Count;
                int rows = result.Tables[0].Rows.Count;
                names = new string[columns];
                types = new string[columns];
                descs = new string[columns];
                datasList = new List<string[]>();

                for (int r = 0; r < rows; r++)
                {
                    string[] row = new string[columns];
                    for (int c = 0; c < columns; c++)
                    {
                        string val = result.Tables[0].Rows[r][c].ToString();
                        if (r < 2) val = val.Trim();
                        row[c] = val;
                    }
                    if (r == 0) names = row;
                    else if (r == 1) types = row;
                    else if (r == 2) descs = row;
                    else datasList.Add(row);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Excel2Cs] 请关闭 Excel 文件: " + e.Message);
                return;
            }

            WriteCs(className, names, types, descs);
            if (jsonExport)
                WriteJson(className, names, types, datasList);
        }

        AssetDatabase.Refresh();
    }
}
