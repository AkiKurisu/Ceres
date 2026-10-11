using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ceres.Settings
{
    public interface ISettingsContributor
    {
        void Contribute(SettingsRegistry registry);
    }

    public sealed class SettingsSection
    {
        private readonly List<Setting> _settings = new();

        public string Id { get; }

        public string Label { get; }

        public SettingsPage Page { get; }

        public IReadOnlyList<Setting> Settings => _settings;

        internal SettingsSection(SettingsPage page, string id, string label)
        {
            Page = page;
            Id = id;
            Label = label;
        }

        public TSetting Add<TSetting>(TSetting setting) where TSetting : Setting
        {
            Page.Registry.Register(setting);
            setting.Section = this;
            foreach (var companion in setting.Companions)
            {
                Page.Registry.Register(companion);
                companion.Section = this;
            }
            _settings.Add(setting);
            return setting;
        }
    }

    public sealed class SettingsPage
    {
        private readonly List<SettingsSection> _sections = new();

        public string Id { get; }

        public string Label { get; }

        public int Order { get; }

        public SettingsRegistry Registry { get; }

        public IReadOnlyList<SettingsSection> Sections => _sections;

        internal SettingsPage(SettingsRegistry registry, string id, string label, int order)
        {
            Registry = registry;
            Id = id;
            Label = label;
            Order = order;
        }

        /// <summary>
        /// Get or add a section; a section without label continues the previous group without a header
        /// </summary>
        public SettingsSection Section(string id, string label = null)
        {
            var section = _sections.FirstOrDefault(s => s.Id == id);
            if (section != null) return section;
            section = new SettingsSection(this, id, label);
            _sections.Add(section);
            return section;
        }

        /// <summary>
        /// Settings of every section, including companions shown inside other settings' rows
        /// </summary>
        public IEnumerable<Setting> Settings => _sections.SelectMany(section => section.Settings)
            .SelectMany(setting => setting.Companions.Prepend(setting));
    }

    public sealed class SettingsRegistry
    {
        private static readonly List<ISettingsContributor> Contributors = new();

        private static SettingsRegistry _instance;

        private readonly List<SettingsPage> _pages = new();

        private readonly Dictionary<string, Setting> _settings = new();

        public IReadOnlyList<SettingsPage> Pages => _pages;

        public IEnumerable<Setting> Settings => _pages.SelectMany(page => page.Settings);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Contributors.Clear();
            _instance = null;
        }

        /// <summary>
        /// Register a contributor of the process-wide registry, normally during module initialization
        /// </summary>
        public static void AddContributor(ISettingsContributor contributor)
        {
            Contributors.Add(contributor);
            if (_instance != null) contributor.Contribute(_instance);
        }

        /// <summary>
        /// Get the process-wide registry, built from registered contributors on first use
        /// </summary>
        public static SettingsRegistry Get()
        {
            return _instance ??= Create(Contributors);
        }

        /// <summary>
        /// Build a standalone registry, for previews and tests
        /// </summary>
        public static SettingsRegistry Create(IEnumerable<ISettingsContributor> contributors)
        {
            var registry = new SettingsRegistry();
            foreach (var contributor in contributors)
            {
                contributor.Contribute(registry);
            }
            return registry;
        }

        public SettingsPage Page(string id, string label = null, int order = 0)
        {
            var page = _pages.FirstOrDefault(p => p.Id == id);
            if (page != null) return page;
            page = new SettingsPage(this, id, label ?? id, order);
            _pages.Insert(_pages.FindLastIndex(p => p.Order <= order) + 1, page);
            return page;
        }

        public Setting Find(string id)
        {
            return _settings.GetValueOrDefault(id);
        }

        internal void Register(Setting setting)
        {
            if (!_settings.TryAdd(setting.Id, setting))
            {
                throw new ArgumentException($"[Ceres] Setting id '{setting.Id}' is already registered.");
            }
        }
    }
}
