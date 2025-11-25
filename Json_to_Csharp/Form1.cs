using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Json_to_Csharp
{
    public partial class Form1 : DevExpress.XtraEditors.XtraForm
    {
        public Form1()
        {
            InitializeComponent();

        }


        private void JSONuGoster(string json)
        {
            try
            {
                DataTable dt = new DataTable();

                // Önce array olarak dene
                try
                {
                    var jsonArray = JArray.Parse(json);

                    // Kolonları ekle (ilk objeden al)
                    if (jsonArray.Count > 0)
                    {
                        foreach (var item in ((JObject)jsonArray[0]).Properties())
                        {
                            dt.Columns.Add(item.Name);
                        }

                        // Verileri ekle
                        foreach (JObject item in jsonArray)
                        {
                            DataRow row = dt.NewRow();
                            foreach (var prop in item.Properties())
                            {
                                row[prop.Name] = prop.Value?.ToString() ?? "";
                            }
                            dt.Rows.Add(row);
                        }
                    }
                }
                catch (Newtonsoft.Json.JsonReaderException)
                {
                    // Array değilse object olarak dene
                    var jsonObject = JObject.Parse(json);

                    // Kolonları ekle
                    foreach (var prop in jsonObject.Properties())
                    {
                        dt.Columns.Add(prop.Name);
                    }

                    // Veriyi ekle
                    DataRow row = dt.NewRow();
                    foreach (var prop in jsonObject.Properties())
                    {
                        row[prop.Name] = prop.Value?.ToString() ?? "";
                    }
                    dt.Rows.Add(row);
                }

                // Grid'e bağla
                gridControl1.DataSource = dt;

                // Kolonları otomatik genişlet
                if (gridControl1.MainView is GridView gridView)
                {
                    gridView.BestFitColumns();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"JSON hatası: {ex.Message}");
            }
        }


        void jsontotable(string jsonInput)
        {
            JToken token = JToken.Parse(jsonInput);

            // Ana tabloyu oluştur
            var list = ConvertToObjects(token);

            gridControl1.DataSource = list;
            gridView1.PopulateColumns();
            gridView1.BestFitColumns();

            gridControl1.LevelTree.Nodes.Clear();
            ConfigureDetailGrids(token, gridView1, "Root");
        }

        private List<object> ConvertToObjects(JToken token)
        {
            if (token is JArray array)
                return array.Select<JToken, object>(t => ConvertJTokenToObject(t)).ToList();
            else if (token is JObject obj)
                return new List<object> { ConvertJTokenToObject(obj) };
            else
                return new List<object>();
        }
        private object ConvertJTokenToObject(JToken token)
        {
            if (token is JObject obj)
                return ConvertJObjectToDictionary(obj);
            else if (token is JArray arr)
                return arr.Select<JToken, object>(t => ConvertJTokenToObject(t)).ToList();
            else
                return token.ToString();
        }
        private Dictionary<string, object> ConvertJObjectToDictionary(JObject obj)
        {
            var dict = new Dictionary<string, object>();
            foreach (var prop in obj.Properties())
            {
                if (prop.Value is JObject childObj)
                    dict[prop.Name] = ConvertJObjectToDictionary(childObj);
                else if (prop.Value is JArray arr)
                    dict[prop.Name] = arr.Select<JToken, object>(t => ConvertJTokenToObject(t)).ToList();
                else
                    dict[prop.Name] = prop.Value?.ToString();
            }
            return dict;
        }

        private void ConfigureDetailGrids(JToken token, GridView parentView, string parentName)
        {
            if (token is JArray arr)
            {
                if (arr.Count > 0 && arr[0] is JObject firstObj)
                    ConfigureDetailGrids(firstObj, parentView, parentName);
            }
            else if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    if (prop.Value is JArray array)
                    {
                        GridView detailView = new GridView(gridControl1)
                        {
                            Name = parentName + "_" + prop.Name,
                            ViewCaption = prop.Name,
                            OptionsView = { ShowGroupPanel = false, ShowViewCaption = true }
                        };
                        gridControl1.LevelTree.Nodes.Add(prop.Name, detailView);

                        gridControl1.ViewRegistered += (s, e) =>
                        {
                            if (e.View is GridView gv)
                            {
                                gv.PopulateColumns();
                                gv.BestFitColumns();
                            }
                        };

                        ConfigureDetailGrids(array, detailView, prop.Name);
                    }
                    else if (prop.Value is JObject nested)
                    {
                        ConfigureDetailGrids(nested, parentView, prop.Name);
                    }
                }
            }
        }
        private void memoEdit1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                string jsonInput = memoEdit1.Text.Trim();
                if (string.IsNullOrWhiteSpace(jsonInput))
                {
                    MessageBox.Show("Lütfen JSON verisi girin!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                JSONuGoster(jsonInput);

                JToken token = JToken.Parse(jsonInput);

                // C# sınıfını oluştur
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("public class RootObject");
                sb.AppendLine("{");

                if (token is JObject jObject)
                {
                    GenerateProperties(jObject, sb, 1);
                }
                else if (token is JArray jArray && jArray.Count > 0)
                {
                    sb.AppendLine("    // JSON bir dizi, ilk öğeyi kullanarak sınıf oluşturuldu");
                    if (jArray[0] is JObject firstObject)
                    {
                        GenerateProperties(firstObject, sb, 1);
                    }
                }

                sb.AppendLine("}");

                // Nested sınıfları ekle
                sb.AppendLine();
                sb.Append(nestedClasses.ToString());

                memoEdit2.Text = sb.ToString();
                nestedClasses.Clear();

                MessageBox.Show("Dönüşüm başarılı!", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
           
        private StringBuilder nestedClasses = new StringBuilder();

        private void GenerateProperties(JObject jObject, StringBuilder sb, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 4);

            foreach (var property in jObject.Properties())
            {
                string propertyName = ToPascalCase(property.Name);
                string propertyType = GetCSharpType(property.Value, propertyName);

                sb.AppendLine($"{indent}public {propertyType} {propertyName} {{ get; set; }}");
            }
        }
        private string GetCSharpType(JToken token, string propertyName)
        {
            switch (token.Type)
            {
                case JTokenType.Integer:
                    return "int";
                case JTokenType.Float:
                    return "double";
                case JTokenType.Boolean:
                    return "bool";
                case JTokenType.Date:
                    return "DateTime";
                case JTokenType.String:
                    return "string";
                case JTokenType.Array:
                    JArray array = (JArray)token;
                    if (array.Count > 0)
                    {
                        string elementType = GetCSharpType(array[0], propertyName);
                        return $"List<{elementType}>";
                    }
                    return "List<object>";
                case JTokenType.Object:
                    string className = propertyName;
                    JObject jObject = (JObject)token;

                    nestedClasses.AppendLine($"public class {className}");
                    nestedClasses.AppendLine("{");
                    GenerateProperties(jObject, nestedClasses, 1);
                    nestedClasses.AppendLine("}");
                    nestedClasses.AppendLine();

                    return className;
                case JTokenType.Null:
                    return "object";
                default:
                    return "object";
            }
        }

        private string ToPascalCase(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Underscore veya dash varsa onları kaldır ve sonraki harfi büyüt
            var parts = text.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var result = string.Join("", parts.Select(p =>
                char.ToUpper(p[0]) + (p.Length > 1 ? p.Substring(1) : "")));

            return result;
        }
    }
}
