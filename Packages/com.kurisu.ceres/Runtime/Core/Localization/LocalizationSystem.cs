using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Ceres.DataDriven;
using Ceres.Resource;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace Ceres.Localization
{
    /// <summary>
    /// Text of the current language for keys, falling back to the source language and then to the key
    /// </summary>
    public static class LocalizationSystem
    {
        public const string TableLabel = "Localization";

        public const string PseudoLanguage = "qps-ploc";

        private static readonly Dictionary<string, Dictionary<string, string>> Texts = new();

        private static readonly HashSet<string> Reported = new();

        private static readonly Subject<Unit> ChangedSubject = new();

        private static string _language;

        private static UniTask? _loading;

        private static IDisposable _configSubscription;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Texts.Clear();
            Reported.Clear();
            _language = null;
            _loading = null;
            _configSubscription?.Dispose();
            _configSubscription = null;
            IsLoaded = false;
#if UNITY_EDITOR
            _editorTablesLoaded = false;
#endif
        }

        public static string SourceLanguage => LocalizationConfig.Get().SourceLanguage;

        /// <summary>
        /// Language shown now, resolved from the config, the system language and the source language
        /// </summary>
        public static string Language
        {
            get
            {
                EnsureEditorTables();
                return _language ??= ResolveLanguage();
            }
        }

        public static IEnumerable<string> LoadedLanguages
        {
            get
            {
                EnsureEditorTables();
                return Texts.Keys;
            }
        }

        public static bool IsLoaded { get; private set; }

        /// <summary>
        /// Raised when the language changes or tables are added
        /// </summary>
        public static Observable<Unit> Changed => ChangedSubject;

        /// <summary>
        /// Load every table with <see cref="TableLabel"/> and follow the language config
        /// </summary>
        public static UniTask InitializeAsync()
        {
            _loading ??= LoadAsync().Preserve();
            return _loading.Value;
        }

        private static async UniTask LoadAsync()
        {
            ObserveConfig();
            try
            {
                await ResourceSystem.EnsureAssetExistsAsync<DataTable>(TableLabel);
            }
            catch (InvalidResourceRequestException)
            {
                IsLoaded = true;
                NotifyChanged();
                return;
            }
            var handle = ResourceSystem.LoadAssetsAsync<DataTable>(TableLabel);
            var tables = await handle.WithCancellation(CancellationToken.None);
            foreach (var table in tables) Add(table);
            ResourceSystem.Release(handle);
            IsLoaded = true;
            NotifyChanged();
        }

        /// <summary>
        /// Add a string table named <c>{group}.{language}</c>, overriding earlier text for the same keys
        /// </summary>
        public static void AddTable(DataTable table)
        {
            Add(table);
            NotifyChanged();
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            EnsureEditorTables();
            var language = Language;
            if (language == PseudoLanguage) return Pseudo(Find(SourceLanguage, key) ?? key);
            var text = Find(language, key);
            if (text != null) return text;
            Report(language, key);
            return Find(SourceLanguage, key) ?? key;
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), args);
        }

        /// <summary>
        /// Language code of a table from its <c>{group}.{language}</c> name, or null
        /// </summary>
        public static string GetTableLanguage(string tableName)
        {
            int dot = tableName.LastIndexOf('.');
            return dot > 0 && dot < tableName.Length - 1 ? tableName[(dot + 1)..] : null;
        }

        private static void Add(DataTable table)
        {
            if (!table || table.GetRowStructType() != typeof(LocalizedTextRow)) return;
            var language = GetTableLanguage(table.name);
            if (language == null)
            {
                Debug.LogWarning($"[Ceres] String table '{table.name}' is not named {{group}}.{{language}}.");
                return;
            }
            if (!Texts.TryGetValue(language, out var texts)) Texts[language] = texts = new Dictionary<string, string>();
            var rows = table.GetAllRows<LocalizedTextRow>();
            var ids = table.RowIds;
            for (int i = 0; i < rows.Length; i++)
            {
                texts[ids[i]] = rows[i].text;
            }
        }

        private static void ObserveConfig()
        {
            _configSubscription ??= LocalizationConfig.Get().Language.Skip(1).Subscribe(_ => NotifyChanged());
        }

        private static void NotifyChanged()
        {
            _language = null;
            ChangedSubject.OnNext(Unit.Default);
        }

        private static string Find(string language, string key)
        {
            return Texts.TryGetValue(language, out var texts) && texts.TryGetValue(key, out var text) && !string.IsNullOrEmpty(text)
                ? text
                : null;
        }

        private static string ResolveLanguage()
        {
            var configured = LocalizationConfig.Get().Language.Value;
            if (!string.IsNullOrEmpty(configured)) return configured;
            var system = GetSystemLanguage(Application.systemLanguage);
            return system != null && Texts.ContainsKey(system) ? system : SourceLanguage;
        }

        private static string GetSystemLanguage(SystemLanguage language)
        {
            return language switch
            {
                SystemLanguage.Japanese => "ja",
                SystemLanguage.English => "en",
                SystemLanguage.Chinese or SystemLanguage.ChineseSimplified => "zh-Hans",
                SystemLanguage.ChineseTraditional => "zh-Hant",
                SystemLanguage.Korean => "ko",
                _ => null
            };
        }

        private static void Report(string language, string key)
        {
            if (!IsLoaded || !(Application.isEditor || Debug.isDebugBuild)) return;
            if (Reported.Add(language + "\n" + key)) Debug.LogWarning($"[Ceres] Localization key '{key}' has no {language} text.");
        }

        private static string Pseudo(string text)
        {
            var builder = new StringBuilder("[");
            int depth = 0;
            foreach (char c in text)
            {
                if (c == '{') depth++;
                builder.Append(depth > 0 ? c : PseudoChar(c));
                if (c == '}' && depth > 0) depth--;
            }
            builder.Append(' ', 1).Append('~', Mathf.CeilToInt(text.Length * 0.3f)).Append(']');
            return builder.ToString();
        }

        private static char PseudoChar(char c)
        {
            const string plain = "aceinouyACEINOUY";
            const string accented = "àçéîñöûýÀÇÉÎÑÖÛÝ";
            int index = plain.IndexOf(c);
            return index >= 0 ? accented[index] : c;
        }

#if UNITY_EDITOR
        private static bool _editorTablesLoaded;

        private static void EnsureEditorTables()
        {
            if (Application.isPlaying || _editorTablesLoaded) return;
            _editorTablesLoaded = true;
            ObserveConfig();
            Texts.Clear();
            foreach (var table in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(DataTable))
                         .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                         .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<DataTable>))
            {
                Add(table);
            }
            _language = null;
            IsLoaded = true;
        }

        internal static void ReloadEditorTables()
        {
            if (Application.isPlaying) return;
            _editorTablesLoaded = false;
            NotifyChanged();
        }
#else
        private static void EnsureEditorTables()
        {
        }
#endif
    }
}
