using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ceres.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ceres.DataDriven.Editor
{
    /// <summary>
    /// Text form of a DataTable stored in a .datatable file
    /// </summary>
    public sealed class DataTableSource
    {
        public readonly struct Row
        {
            public string Id { get; }

            /// <summary>
            /// Row members as compact JSON readable by <see cref="JsonUtility"/>
            /// </summary>
            public string Json { get; }

            public Row(string id, string json)
            {
                Id = id;
                Json = json;
            }
        }

        public const string Extension = "datatable";

        private const string RowTypeKey = "rowType";

        private const string RowsKey = "rows";

        private const string IdKey = "id";

        private const string DataKey = "data";

        /// <summary>
        /// Assembly qualified row type name without version, or empty for a table without schema
        /// </summary>
        public string RowTypeName { get; }

        public IReadOnlyList<Row> Rows { get; }

        public DataTableSource(string rowTypeName, IReadOnlyList<Row> rows)
        {
            RowTypeName = rowTypeName ?? string.Empty;
            Rows = rows ?? Array.Empty<Row>();
        }

        public static string GetRowTypeName(Type rowType)
        {
            return rowType == null ? string.Empty : SerializedType.GetTypeName(SerializedType.ToString(rowType));
        }

        public static Type ResolveRowType(string rowTypeName)
        {
            return string.IsNullOrEmpty(rowTypeName) ? null : SerializedType.FromString(rowTypeName + "##0");
        }

        /// <summary>
        /// Parse source text; throws <see cref="FormatException"/> for malformed sources
        /// </summary>
        public static DataTableSource Parse(string text)
        {
            JObject root;
            try
            {
                using var reader = new JsonTextReader(new StringReader(text))
                {
                    FloatParseHandling = FloatParseHandling.Double,
                    DateParseHandling = DateParseHandling.None
                };
                root = JObject.Load(reader);
            }
            catch (JsonException e)
            {
                throw new FormatException(e.Message, e);
            }

            var rowTypeToken = root[RowTypeKey];
            if (rowTypeToken != null && rowTypeToken.Type != JTokenType.String)
            {
                throw new FormatException($"'{RowTypeKey}' must be a string.");
            }

            var rows = new List<Row>();
            var rowsToken = root[RowsKey];
            if (rowsToken != null)
            {
                if (rowsToken is not JArray array)
                {
                    throw new FormatException($"'{RowsKey}' must be an array.");
                }
                foreach (var item in array)
                {
                    var line = ((IJsonLineInfo)item).LineNumber;
                    if (item is not JObject row
                        || row[IdKey] is not JValue { Type: JTokenType.String } id
                        || row[DataKey] is not JObject data)
                    {
                        throw new FormatException($"Row at line {line} needs a string '{IdKey}' and an object '{DataKey}'.");
                    }
                    rows.Add(new Row((string)id.Value, data.ToString(Formatting.None)));
                }
            }
            return new DataTableSource((string)rowTypeToken ?? string.Empty, rows);
        }

        /// <summary>
        /// Write source text with stable formatting
        /// </summary>
        public string Write()
        {
            var builder = new StringBuilder();
            using (var stringWriter = new StringWriter(builder) { NewLine = "\n" })
            using (var writer = new SourceWriter(stringWriter)
                   {
                       Formatting = Formatting.Indented,
                       Indentation = 2,
                       StringEscapeHandling = StringEscapeHandling.Default
                   })
            {
                writer.WriteStartObject();
                writer.WritePropertyName(RowTypeKey);
                writer.WriteValue(RowTypeName);
                writer.WritePropertyName(RowsKey);
                writer.WriteStartArray();
                foreach (var row in Rows)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName(IdKey);
                    writer.WriteValue(row.Id);
                    writer.WritePropertyName(DataKey);
                    using (var reader = new JsonTextReader(new StringReader(row.Json))
                           {
                               FloatParseHandling = FloatParseHandling.Double,
                               DateParseHandling = DateParseHandling.None
                           })
                    {
                        writer.WriteToken(reader);
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            builder.Append('\n');
            return builder.ToString();
        }

        private sealed class SourceWriter : JsonTextWriter
        {
            private static readonly char[] FloatMarkers = { '.', 'E', 'e' };

            public SourceWriter(TextWriter textWriter) : base(textWriter)
            {
            }

            /// <summary>
            /// Write the shortest form that reads back as the same value, as <see cref="JsonUtility"/> does
            /// </summary>
            public override void WriteValue(double value)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    base.WriteValue(value);
                    return;
                }
                string text = null;
                for (int precision = 15; precision <= 17; precision++)
                {
                    text = value.ToString("G" + precision, CultureInfo.InvariantCulture);
                    if (double.Parse(text, CultureInfo.InvariantCulture) == value) break;
                }
                if (text.IndexOfAny(FloatMarkers) < 0) text += ".0";
                WriteRawValue(text);
            }
        }
    }
}
