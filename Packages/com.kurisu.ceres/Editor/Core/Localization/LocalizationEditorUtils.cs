using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ceres.DataDriven;
using Ceres.DataDriven.Editor;
using UnityEditor;

namespace Ceres.Localization.Editor
{
    public sealed class StringTableGroup
    {
        public string Name { get; }

        /// <summary>
        /// Folder of the group's tables, where new language tables are created
        /// </summary>
        public string Folder { get; }

        public SortedDictionary<string, DataTable> Tables { get; } = new(StringComparer.Ordinal);

        public StringTableGroup(string name, string folder)
        {
            Name = name;
            Folder = folder;
        }
    }

    public sealed class StringTableReport
    {
        public string Language { get; }

        /// <summary>
        /// Source keys without text in this language
        /// </summary>
        public IReadOnlyList<string> Missing { get; }

        /// <summary>
        /// Keys of this language that the source does not define
        /// </summary>
        public IReadOnlyList<string> Orphans { get; }

        public StringTableReport(string language, IReadOnlyList<string> missing, IReadOnlyList<string> orphans)
        {
            Language = language;
            Missing = missing;
            Orphans = orphans;
        }
    }

    public static class LocalizationEditorUtils
    {
        private const string KeyColumn = "key";

        private const string NoteColumn = "note";

        public static IReadOnlyList<StringTableGroup> FindGroups()
        {
            var groups = new SortedDictionary<string, StringTableGroup>(StringComparer.Ordinal);
            foreach (var path in AssetDatabase.FindAssets("t:" + nameof(DataTable)).Select(AssetDatabase.GUIDToAssetPath))
            {
                var table = AssetDatabase.LoadAssetAtPath<DataTable>(path);
                if (!table || table.GetRowStructType() != typeof(LocalizedTextRow)) continue;
                var language = LocalizationSystem.GetTableLanguage(table.name);
                if (language == null) continue;
                var name = table.name[..(table.name.Length - language.Length - 1)];
                var key = Path.GetDirectoryName(path)?.Replace('\\', '/') + "/" + name;
                if (!groups.TryGetValue(key, out var group)) groups[key] = group = new StringTableGroup(name, Path.GetDirectoryName(path)?.Replace('\\', '/'));
                group.Tables[language] = table;
            }
            return groups.Values.ToArray();
        }

        /// <summary>
        /// Text by key of one table, including unsaved edits of an open document
        /// </summary>
        public static Dictionary<string, LocalizedTextRow> ReadRows(DataTable table)
        {
            return DataTableEditorUtils.GetRowMapSafe(table).ToDictionary(pair => pair.Key, pair => (LocalizedTextRow)pair.Value);
        }

        /// <summary>
        /// Keys in source order followed by keys only translations define
        /// </summary>
        public static List<string> GetKeys(StringTableGroup group, IReadOnlyDictionary<string, Dictionary<string, LocalizedTextRow>> rows)
        {
            var keys = new List<string>();
            var known = new HashSet<string>();
            var source = LocalizationSystem.SourceLanguage;
            foreach (var language in rows.Keys.OrderBy(language => language == source ? 0 : 1))
            {
                foreach (var key in rows[language].Keys)
                {
                    if (known.Add(key)) keys.Add(key);
                }
            }
            return keys;
        }

        public static IReadOnlyList<StringTableReport> Report(StringTableGroup group)
        {
            var source = LocalizationSystem.SourceLanguage;
            var rows = group.Tables.ToDictionary(pair => pair.Key, pair => ReadRows(pair.Value));
            var sourceKeys = rows.TryGetValue(source, out var sourceRows) ? sourceRows.Keys.ToArray() : Array.Empty<string>();
            var reports = new List<StringTableReport>();
            foreach (var (language, texts) in rows)
            {
                if (language == source) continue;
                var missing = sourceKeys.Where(key => !texts.TryGetValue(key, out var row) || string.IsNullOrEmpty(row.text)).ToArray();
                var orphans = texts.Keys.Where(key => !sourceRows?.ContainsKey(key) ?? true).ToArray();
                reports.Add(new StringTableReport(language, missing, orphans));
            }
            return reports;
        }

        /// <summary>
        /// Write one row per key with the note and one column per language
        /// </summary>
        public static void ExportCsv(StringTableGroup group, string path)
        {
            var source = LocalizationSystem.SourceLanguage;
            var rows = group.Tables.ToDictionary(pair => pair.Key, pair => ReadRows(pair.Value));
            var languages = rows.Keys.OrderBy(language => language == source ? 0 : 1).ThenBy(language => language, StringComparer.Ordinal).ToArray();
            var builder = new StringBuilder();
            AppendLine(builder, new[] { KeyColumn, NoteColumn }.Concat(languages));
            foreach (var key in GetKeys(group, rows))
            {
                var note = rows.TryGetValue(source, out var sourceRows) && sourceRows.TryGetValue(key, out var sourceRow) ? sourceRow.note : string.Empty;
                AppendLine(builder, new[] { key, note }.Concat(languages.Select(language =>
                    rows[language].TryGetValue(key, out var row) ? row.text : string.Empty)));
            }
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
        }

        /// <summary>
        /// Write the non-empty cells of a CSV into the group's tables, creating missing language tables, and return the number of written cells
        /// </summary>
        public static int ImportCsv(StringTableGroup group, string path)
        {
            var records = ReadCsv(File.ReadAllText(path));
            if (records.Count == 0 || records[0].Count < 3 || records[0][0] != KeyColumn || records[0][1] != NoteColumn)
            {
                throw new FormatException($"'{path}' must start with the columns {KeyColumn}, {NoteColumn} and one column per language.");
            }
            var header = records[0];
            var source = LocalizationSystem.SourceLanguage;
            int written = 0;
            using (DataTableEditorUtils.BeginBatch())
            {
                for (int column = 2; column < header.Count; column++)
                {
                    var language = header[column].Trim();
                    if (!group.Tables.TryGetValue(language, out var table))
                    {
                        table = DataTableEditorUtils.CreateDataTable($"{group.Folder}/{group.Name}.{language}.{DataTableSource.Extension}", typeof(LocalizedTextRow));
                        group.Tables[language] = table;
                    }
                    int index = column;
                    DataTableEditorUtils.Edit(table, document =>
                    {
                        foreach (var record in records.Skip(1))
                        {
                            if (record.Count <= index || string.IsNullOrEmpty(record[0]) || string.IsNullOrEmpty(record[index])) continue;
                            var row = document.GetRow<LocalizedTextRow>(record[0]) ?? new LocalizedTextRow();
                            row.text = record[index];
                            if (language == source && record.Count > 1) row.note = record[1];
                            document.AddOrUpdateRow(record[0], row);
                            written++;
                        }
                    });
                }
            }
            return written;
        }

        private static void AppendLine(StringBuilder builder, IEnumerable<string> cells)
        {
            builder.Append(string.Join(",", cells.Select(Quote))).Append("\r\n");
        }

        private static string Quote(string cell)
        {
            cell ??= string.Empty;
            return cell.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
        }

        internal static List<List<string>> ReadCsv(string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c != '"') cell.Append(c);
                    else if (i + 1 < text.Length && text[i + 1] == '"') cell.Append(text[++i]);
                    else quoted = false;
                    continue;
                }
                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        record.Add(cell.ToString());
                        cell.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        record.Add(cell.ToString());
                        cell.Clear();
                        records.Add(record);
                        record = new List<string>();
                        break;
                    case '﻿' when i == 0:
                        break;
                    default:
                        cell.Append(c);
                        break;
                }
            }
            if (cell.Length > 0 || record.Count > 0)
            {
                record.Add(cell.ToString());
                records.Add(record);
            }
            return records;
        }
    }

    internal sealed class StringTablePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var extension = "." + DataTableSource.Extension;
            if (importedAssets.Concat(deletedAssets).Concat(movedAssets).Any(path => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            {
                LocalizationSystem.ReloadEditorTables();
            }
        }
    }
}
