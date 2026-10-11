using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.UIElements;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Settings.UI
{
    /// <summary>
    /// Binds a product settings window UXML and its row templates to a registry; shared by the runtime host and previews
    /// </summary>
    [UIToolkitView("settings-")]
    public sealed partial class SettingsWindowView : IDisposable
    {
        private sealed class Page
        {
            public SettingsPage Model;
            public Button Tab;
            public VisualElement TabContainer;
            public VisualElement Content;
            public readonly List<(VisualElement Container, Label Header, List<SettingRow> Rows)> Sections = new();
            public IEnumerable<SettingRow> Rows => Sections.SelectMany(section => section.Rows);
        }

        private const float RefreshInterval = 0.5f;

        public const string TabSelectedClassName = "settings-tab--selected";
        public const string SectionFirstClassName = "settings-section--first";

        [UIToolkitQuery] private VisualElement _root;
        [UIToolkitQuery] private Button _close;
        [UIToolkitQuery] private VisualElement _nav;
        [UIToolkitQuery] private VisualElement _content;
        [UIToolkitQuery] private VisualElement _detail;
        [UIToolkitQuery] private Label _detailTitle;
        [UIToolkitQuery] private Label _detailDescription;
        [UIToolkitQuery] private VisualElement _detailImpact;
        [UIToolkitQuery] private Label _detailReason;
        [UIToolkitQuery] private Label _detailRestart;
        [UIToolkitQuery] private Button _resetPage;
        [UIToolkitQuery] private Button _revert;
        [UIToolkitQuery] private Button _apply;
        [UIToolkitQuery] private VisualElement _confirm;
        [UIToolkitQuery] private Label _confirmMessage;
        [UIToolkitQuery] private Button _confirmRevert;
        [UIToolkitQuery] private Button _confirmKeep;

        private readonly UIToolkitBindingScope _scope = new();
        private readonly List<Page> _pages = new();
        private readonly SettingsWindowText _text;
        private readonly SettingsWindowTemplates _templates;
        private readonly Action _onClose;
        private SettingsRegistry _registry;
        private SettingsSession _session;
        private IDisposable _changes;
        private Page _page;
        private SettingRow _focused;
        private bool _dirty;
        private bool _wasConfirming;
        private float _nextRefresh;

        public bool IsOpen => _session != null;

        public SettingsSession Session => _session;

        public string CurrentPageId => _page?.Model.Id;

        public SettingsWindowView(VisualElement root, SettingsWindowTemplates templates, SettingsWindowText text, Action close)
        {
            BindGeneratedElements(root);
            _templates = templates;
            _text = text;
            _onClose = close;
            _scope.Click(_close, () => _onClose?.Invoke());
            _scope.Click(_resetPage, () => Act(() => _session.ResetPage(_page.Model)));
            _scope.Click(_revert, () => Act(() => _session.RevertAll()));
            _scope.Click(_apply, () => Act(() => _session.ApplyStaged()));
            _scope.Click(_confirmKeep, () => Act(() => _session.Keep()));
            _scope.Click(_confirmRevert, () => Act(() => _session.RevertConfirmation()));
            _scope.Callback<NavigationMoveEvent>(_content, OnNavigationMove, TrickleDown.TrickleDown);
            _scope.Callback<NavigationSubmitEvent>(_content, _ => Act(() =>
            {
                if (_focused != null && _focused.State.IsEnabled) _focused.Submit();
            }));
            _scope.Callback<NavigationCancelEvent>(_root, _ => Cancel());
            _scope.Callback<KeyDownEvent>(_root, OnKeyDown);
            _root.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Show the registry and start a session that saves changed configs when the window closes; without a page the last page shows
        /// </summary>
        public void Open(SettingsRegistry registry, string pageId = null)
        {
            if (IsOpen) Close();
            pageId ??= CurrentPageId;
            if (_registry != registry) Build(registry);
            _session = new SettingsSession(registry);
            _changes = Observable.Merge(registry.Settings.Select(setting => setting.Changed).ToArray())
                .Subscribe(_ => _dirty = true);
            _root.style.display = DisplayStyle.Flex;
            ShowPage(_pages.FirstOrDefault(page => page.Model.Id == pageId) ?? _pages.FirstOrDefault());
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            _changes?.Dispose();
            _changes = null;
            _session.Close();
            _session = null;
            _root.style.display = DisplayStyle.None;
            _root.focusController?.focusedElement?.Blur();
        }

        /// <summary>
        /// Back out of the innermost state: an unconfirmed change first, then the window
        /// </summary>
        public void Cancel()
        {
            if (!IsOpen) return;
            if (_session.IsConfirming) Act(() => _session.RevertConfirmation());
            else _onClose?.Invoke();
        }

        public void SetTouch(bool touch)
        {
            _root.EnableInClassList("ceres-touch", touch);
            if (_nav is not ScrollView scroll) return;
            scroll.mode = touch ? ScrollViewMode.Horizontal : ScrollViewMode.Vertical;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        }

        /// <summary>
        /// Keep the window inside the safe area, in panel coordinates with a top-left origin
        /// </summary>
        public void SetSafeArea(Vector2 panelSize, Rect safeArea)
        {
            _root.style.paddingLeft = safeArea.xMin;
            _root.style.paddingTop = safeArea.yMin;
            _root.style.paddingRight = Mathf.Max(0, panelSize.x - safeArea.xMax);
            _root.style.paddingBottom = Mathf.Max(0, panelSize.y - safeArea.yMax);
        }

        public void Update()
        {
            if (!IsOpen) return;
            _session.Update();
            if (_session.IsConfirming || _wasConfirming || _dirty || Time.realtimeSinceStartup >= _nextRefresh) Refresh();
        }

        public void Dispose()
        {
            Close();
            _scope.Dispose();
        }

        private void Build(SettingsRegistry registry)
        {
            _registry = registry;
            _pages.Clear();
            _nav.Clear();
            _content.Clear();
            foreach (var model in registry.Pages)
            {
                var page = new Page { Model = model, Content = new VisualElement(), TabContainer = _templates.tab.Instantiate() };
                page.Tab = SettingsWindowTemplates.Require<Button>(page.TabContainer, SettingsWindowTemplates.TabName, _templates.tab);
                page.Tab.text = model.Label;
                page.Tab.focusable = false;
                page.Tab.clicked += () =>
                {
                    ShowPage(page);
                    Refresh();
                };
                foreach (var section in model.Sections)
                {
                    var container = _templates.section.Instantiate();
                    var header = SettingsWindowTemplates.Require<Label>(container, SettingsWindowTemplates.SectionName, _templates.section);
                    header.text = section.Label;
                    page.Content.Add(container);
                    var rows = new List<SettingRow>();
                    foreach (var setting in section.Settings)
                    {
                        var row = SettingRow.Create(setting, _templates, () => _session);
                        row.Element.RegisterCallback<PointerEnterEvent>(_ => Focus(row));
                        row.Element.RegisterCallback<FocusInEvent>(_ => Focus(row));
                        row.Element.RegisterCallback<PointerDownEvent>(_ => Focus(row));
                        row.Element.RegisterCallback<ClickEvent>(_ => _dirty = true);
                        rows.Add(row);
                        page.Content.Add(row.Container);
                    }
                    page.Sections.Add((container, header, rows));
                }
                _nav.Add(page.TabContainer);
                _content.Add(page.Content);
                _pages.Add(page);
            }
        }

        private void ShowPage(Page page)
        {
            _page = page;
            foreach (var candidate in _pages)
            {
                bool current = candidate == page;
                candidate.Content.style.display = current ? DisplayStyle.Flex : DisplayStyle.None;
                candidate.Tab.EnableInClassList(TabSelectedClassName, current);
            }
            if (_content is ScrollView scroll) scroll.scrollOffset = Vector2.zero;
            _focused = null;
            _dirty = true;
        }

        private void Focus(SettingRow row)
        {
            if (_focused == row) return;
            _focused?.Element.RemoveFromClassList(SettingRow.FocusedClassName);
            _focused = row;
            row.Element.AddToClassList(SettingRow.FocusedClassName);
            RefreshDetail();
        }

        private void Act(Action action)
        {
            if (!IsOpen) return;
            action();
            Refresh();
        }

        private void Refresh()
        {
            _dirty = false;
            _nextRefresh = Time.realtimeSinceStartup + RefreshInterval;
            foreach (var page in _pages)
            {
                bool anyVisible = false;
                foreach (var (container, header, rows) in page.Sections)
                {
                    foreach (var row in rows) row.Refresh();
                    bool sectionVisible = rows.Any(row => row.State.IsVisible);
                    container.style.display = sectionVisible && !string.IsNullOrEmpty(header.text) ? DisplayStyle.Flex : DisplayStyle.None;
                    anyVisible |= sectionVisible;
                }
                page.TabContainer.style.display = anyVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            var firstSection = _page?.Sections.Select(section => section.Container)
                .FirstOrDefault(container => container.style.display == DisplayStyle.Flex);
            foreach (var page in _pages)
            {
                foreach (var section in page.Sections) section.Header.EnableInClassList(SectionFirstClassName, section.Container == firstSection);
            }
            if (_focused == null || !_focused.State.IsVisible)
            {
                var first = _page?.Rows.FirstOrDefault(row => row.State.IsVisible);
                if (first != null) Focus(first);
            }
            RefreshDetail();
            RefreshFooter();
        }

        private void RefreshFooter()
        {
            _revert.SetEnabled(_session.HasChanges || _session.HasStaged);
            _resetPage.SetEnabled(_page != null);
            _apply.style.display = _session.HasStaged ? DisplayStyle.Flex : DisplayStyle.None;
            bool confirming = _session.IsConfirming;
            _confirm.style.display = confirming ? DisplayStyle.Flex : DisplayStyle.None;
            if (confirming)
            {
                _confirmMessage.text = string.Format(_text.ConfirmMessage, Mathf.CeilToInt(_session.ConfirmRemaining));
                if (!_wasConfirming) _confirmKeep.Focus();
            }
            _wasConfirming = confirming;
        }

        private void RefreshDetail()
        {
            var setting = _focused?.Setting;
            _detail.style.visibility = setting == null ? Visibility.Hidden : Visibility.Visible;
            if (setting == null) return;
            _detailTitle.text = setting.Label;
            _detailDescription.text = setting.Description;
            _detailDescription.style.display = string.IsNullOrEmpty(setting.Description) ? DisplayStyle.None : DisplayStyle.Flex;
            _detailImpact.Clear();
            AddImpact(_text.Gpu, setting.Impact.Gpu);
            AddImpact(_text.Vram, setting.Impact.Vram);
            AddImpact(_text.Cpu, setting.Impact.Cpu);
            var reasons = new List<string>();
            if (!_focused.State.IsEnabled && !string.IsNullOrEmpty(_focused.State.Reason)) reasons.Add(_focused.State.Reason);
            if (setting is IChoiceSetting choice)
            {
                foreach (var option in choice.Options)
                {
                    var state = option.EvaluateEditState();
                    if (state.IsVisible && !state.IsEnabled && !string.IsNullOrEmpty(state.Reason)) reasons.Add($"{option.Label}: {state.Reason}");
                }
            }
            _detailReason.text = string.Join("\n", reasons);
            _detailReason.style.display = reasons.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _detailRestart.text = _text.RestartRequired;
            _detailRestart.style.display = setting.RequiresRestart ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void AddImpact(string name, SettingImpactLevel level)
        {
            if (level == SettingImpactLevel.None) return;
            var row = _templates.impact.Instantiate();
            SettingsWindowTemplates.Require<Label>(row, SettingsWindowTemplates.ImpactNameName, _templates.impact).text = name;
            SettingsWindowTemplates.Require<Label>(row, SettingsWindowTemplates.ImpactLevelName, _templates.impact).text = _text.ImpactLevel(level);
            _detailImpact.Add(row);
        }

        private void OnNavigationMove(NavigationMoveEvent evt)
        {
            if (!IsOpen || _page == null) return;
            switch (evt.direction)
            {
                case NavigationMoveEvent.Direction.Up:
                case NavigationMoveEvent.Direction.Down:
                    var rows = _page.Rows.Where(row => row.State.IsVisible).ToList();
                    if (rows.Count == 0) break;
                    int index = rows.IndexOf(_focused) + (evt.direction == NavigationMoveEvent.Direction.Down ? 1 : -1);
                    var target = rows[Mathf.Clamp(index, 0, rows.Count - 1)];
                    target.Element.Focus();
                    (_content as ScrollView ?? _content.GetFirstAncestorOfType<ScrollView>())?.ScrollTo(target.Container);
                    break;
                case NavigationMoveEvent.Direction.Left:
                case NavigationMoveEvent.Direction.Right:
                    if (_focused == null || !_focused.State.IsEnabled) break;
                    Act(() => _focused.Step(evt.direction == NavigationMoveEvent.Direction.Right ? 1 : -1));
                    break;
                default:
                    return;
            }
            evt.StopPropagation();
#if UNITY_2023_2_OR_NEWER
            _content.focusController?.IgnoreEvent(evt);
#endif
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            int direction = evt.keyCode switch
            {
                KeyCode.Q or KeyCode.PageUp => -1,
                KeyCode.E or KeyCode.PageDown => 1,
                _ => 0
            };
            if (direction == 0 || _page == null) return;
            var visible = _pages.Where(page => page.TabContainer.style.display != DisplayStyle.None).ToList();
            if (visible.Count == 0) return;
            int index = Mathf.Clamp(visible.IndexOf(_page) + direction, 0, visible.Count - 1);
            ShowPage(visible[index]);
            Refresh();
            evt.StopPropagation();
        }
    }
}
