using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ceres.Localization.Editor
{
    public sealed class LocalizationWindow : EditorWindow
    {
        private IReadOnlyList<StringTableGroup> _groups = Array.Empty<StringTableGroup>();

        private readonly Dictionary<StringTableGroup, IReadOnlyList<StringTableReport>> _reports = new();

        private StringTableGroup _selected;

        private Vector2 _scroll;

        private Vector2 _reportScroll;

        [MenuItem("Tools/Ceres/Localization", false, 1)]
        private static void Open()
        {
            GetWindow<LocalizationWindow>("Localization").Show();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnFocus()
        {
            Refresh();
        }

        private void Refresh()
        {
            _groups = LocalizationEditorUtils.FindGroups();
            _reports.Clear();
            foreach (var group in _groups) _reports[group] = LocalizationEditorUtils.Report(group);
            if (_selected != null) _selected = _groups.FirstOrDefault(group => group.Folder == _selected.Folder && group.Name == _selected.Name);
        }

        private void OnGUI()
        {
            DrawToolbar();
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(_selected == null));
            foreach (var group in _groups) DrawGroup(group);
            if (_groups.Count == 0) EditorGUILayout.HelpBox("No string tables. Create a DataTable of LocalizedTextRow named {group}.{language}.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            if (_selected != null) DrawReport(_selected);
        }

        private void DrawToolbar()
        {
            using var _ = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);
            var config = LocalizationConfig.Get();
            var languages = new[] { string.Empty }.Concat(LocalizationSystem.LoadedLanguages.OrderBy(language => language, StringComparer.Ordinal))
                .Append(LocalizationSystem.PseudoLanguage).Distinct().ToArray();
            int index = Mathf.Max(0, Array.IndexOf(languages, config.Language.Value ?? string.Empty));
            GUILayout.Label("Preview Language", EditorStyles.miniLabel, GUILayout.Width(100));
            int selected = EditorGUILayout.Popup(index, languages.Select(language => string.IsNullOrEmpty(language) ? "System" : language).ToArray(),
                EditorStyles.toolbarPopup, GUILayout.Width(120));
            if (selected != index) config.Language.Value = languages[selected];
            GUILayout.Label($"Source: {LocalizationSystem.SourceLanguage}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) Refresh();
        }

        private void DrawGroup(StringTableGroup group)
        {
            var source = LocalizationSystem.SourceLanguage;
            int total = group.Tables.TryGetValue(source, out var sourceTable) ? sourceTable.RowCount : 0;
            using (new EditorGUILayout.HorizontalScope())
            {
                bool isSelected = _selected == group;
                if (GUILayout.Toggle(isSelected, group.Name, EditorStyles.foldout, GUILayout.Width(160)) != isSelected)
                {
                    _selected = isSelected ? null : group;
                }
                foreach (var report in _reports[group])
                {
                    var style = report.Missing.Count > 0 || report.Orphans.Count > 0 ? Styles.Problem : EditorStyles.miniLabel;
                    GUILayout.Label($"{report.Language} {total - report.Missing.Count}/{total}", style, GUILayout.Width(110));
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Export CSV", EditorStyles.miniButtonLeft, GUILayout.Width(80))) Export(group);
                if (GUILayout.Button("Import CSV", EditorStyles.miniButtonRight, GUILayout.Width(80))) Import(group);
            }
            GUILayout.Label(group.Folder, EditorStyles.miniLabel);
            GUILayout.Space(4);
        }

        private void DrawReport(StringTableGroup group)
        {
            EditorGUILayout.Space();
            GUILayout.Label(group.Name, EditorStyles.boldLabel);
            _reportScroll = EditorGUILayout.BeginScrollView(_reportScroll, GUILayout.ExpandHeight(true));
            foreach (var report in _reports[group])
            {
                if (report.Missing.Count == 0 && report.Orphans.Count == 0)
                {
                    GUILayout.Label($"{report.Language}: complete", EditorStyles.miniLabel);
                    continue;
                }
                if (report.Missing.Count > 0)
                {
                    GUILayout.Label($"{report.Language}: {report.Missing.Count} missing", Styles.Problem);
                    EditorGUILayout.SelectableLabel(string.Join("\n", report.Missing), EditorStyles.miniLabel,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight * Mathf.Min(report.Missing.Count, 12)));
                }
                if (report.Orphans.Count > 0)
                {
                    GUILayout.Label($"{report.Language}: {report.Orphans.Count} not in source", Styles.Problem);
                    EditorGUILayout.SelectableLabel(string.Join("\n", report.Orphans), EditorStyles.miniLabel,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight * Mathf.Min(report.Orphans.Count, 12)));
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void Export(StringTableGroup group)
        {
            var path = EditorUtility.SaveFilePanel("Export string table group", string.Empty, group.Name, "csv");
            if (string.IsNullOrEmpty(path)) return;
            LocalizationEditorUtils.ExportCsv(group, path);
            Debug.Log($"[Ceres] Exported {group.Name} to {path}.");
        }

        private void Import(StringTableGroup group)
        {
            var path = EditorUtility.OpenFilePanel("Import string table group", string.Empty, "csv");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                int written = LocalizationEditorUtils.ImportCsv(group, path);
                Debug.Log($"[Ceres] Imported {written} texts into {group.Name}.");
            }
            catch (FormatException exception)
            {
                Debug.LogError(exception.Message);
            }
            Refresh();
            GUIUtility.ExitGUI();
        }

        private static class Styles
        {
            public static readonly GUIStyle Problem = new(EditorStyles.miniLabel) { normal = { textColor = new Color(0.95f, 0.55f, 0.4f) } };
        }
    }
}
