using System;
using System.Linq;
using UnityEngine.UIElements;

namespace Ceres.Settings.UI
{
    internal abstract class SettingRow
    {
        public const string FocusedClassName = "settings-row--focused";
        public const string DisabledClassName = "settings-row--disabled";

        protected readonly Func<SettingsSession> Session;

        private readonly Label _detail;

        private readonly VisualElement _control;

        public Setting Setting { get; }

        public VisualElement Container { get; }

        public VisualElement Element { get; }

        public SettingEditState State { get; private set; }

        protected SettingRow(Setting setting, VisualTreeAsset template, string controlName, Func<SettingsSession> session)
        {
            Setting = setting;
            Session = session;
            var instance = template.Instantiate();
            Container = instance;
            Element = SettingsWindowTemplates.Require<VisualElement>(instance, SettingsWindowTemplates.RowName, template);
            Element.focusable = true;
            SettingsWindowTemplates.Require<Label>(instance, SettingsWindowTemplates.RowLabelName, template).text = setting.Label;
            _detail = instance.Q<Label>(SettingsWindowTemplates.RowDetailName);
            _control = SettingsWindowTemplates.Require<VisualElement>(instance, controlName, template);
            _control.focusable = false;
        }

        public static SettingRow Create(Setting setting, SettingsWindowTemplates templates, Func<SettingsSession> session)
        {
            return setting switch
            {
                ToggleSetting toggle => new ToggleRow(toggle, templates.toggle, session),
                SliderSetting slider => new SliderRow(slider, templates.slider, session),
                IChoiceSetting choice => new ChoiceRow(setting, choice, IsShort(choice, templates) ? templates.choice : templates.choiceList, session),
                ActionSetting action => new ActionRow(action, templates.action, session),
                _ => throw new NotSupportedException($"[Ceres] Setting '{setting.Id}' of type {setting.GetType().Name} has no row.")
            };
        }

        private static bool IsShort(IChoiceSetting choice, SettingsWindowTemplates templates)
        {
            return choice.Options.Count <= templates.ShortChoiceCount
                   && choice.Options.All(option => option.Label.Length <= templates.ShortChoiceLength);
        }

        public void Refresh()
        {
            State = Setting.EvaluateEditState();
            Container.style.display = State.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
            Element.EnableInClassList(DisabledClassName, !State.IsEnabled);
            _control.SetEnabled(State.IsEnabled);
            if (_detail != null) _detail.text = State.IsEnabled || string.IsNullOrEmpty(State.Reason) ? Setting.Description : State.Reason;
            RefreshValue();
        }

        protected abstract void RefreshValue();

        public virtual void Step(int direction)
        {
        }

        public virtual void Submit()
        {
        }
    }

    internal sealed class ToggleRow : SettingRow
    {
        private readonly ValueSetting<bool> _setting;

        private readonly Toggle _toggle;

        public ToggleRow(ValueSetting<bool> setting, VisualTreeAsset template, Func<SettingsSession> session)
            : base(setting, template, SettingsWindowTemplates.RowToggleName, session)
        {
            _setting = setting;
            _toggle = SettingsWindowTemplates.Require<Toggle>(Element, SettingsWindowTemplates.RowToggleName, template);
            _toggle.RegisterValueChangedCallback(evt => Write(evt.newValue));
        }

        private void Write(bool value)
        {
            if (_setting.ApplyMode == SettingApplyMode.Confirm) Session().Stage(_setting, value);
            else _setting.Value = value;
        }

        protected override void RefreshValue()
        {
            _toggle.SetValueWithoutNotify(Session().TryGetStaged(_setting, out bool staged) ? staged : _setting.Value);
        }

        public override void Step(int direction) => Write(direction > 0);

        public override void Submit() => Write(!_toggle.value);
    }

    internal sealed class SliderRow : SettingRow
    {
        private readonly SliderSetting _setting;

        private readonly Slider _slider;

        private readonly Label _value;

        private readonly Toggle _toggle;

        public SliderRow(SliderSetting setting, VisualTreeAsset template, Func<SettingsSession> session)
            : base(setting, template, SettingsWindowTemplates.RowSliderName, session)
        {
            _setting = setting;
            _slider = SettingsWindowTemplates.Require<Slider>(Element, SettingsWindowTemplates.RowSliderName, template);
            _slider.lowValue = setting.Min;
            _slider.highValue = setting.Max;
            _slider.RegisterValueChangedCallback(evt => Write(evt.newValue));
            _value = Element.Q<Label>(SettingsWindowTemplates.RowValueName);
            _toggle = Element.Q<Toggle>(SettingsWindowTemplates.RowToggleName);
            if (_toggle == null) return;
            if (setting.Toggle == null)
            {
                _toggle.style.display = DisplayStyle.None;
                return;
            }
            _toggle.focusable = false;
            _toggle.RegisterValueChangedCallback(evt => Write(setting.Toggle, evt.newValue));
        }

        private void Write<T>(ValueSetting<T> setting, T value)
        {
            if (setting.ApplyMode == SettingApplyMode.Confirm) Session().Stage(setting, value);
            else setting.Value = value;
        }

        private void Write(float value) => Write(_setting, value);

        protected override void RefreshValue()
        {
            float value = Session().TryGetStaged(_setting, out float staged) ? staged : _setting.Value;
            _slider.SetValueWithoutNotify(value);
            if (_value != null) _value.text = _setting.Format(value);
            if (_setting.Toggle == null || _toggle == null) return;
            var state = _setting.Toggle.EvaluateEditState();
            _toggle.style.display = state.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _toggle.SetEnabled(state.IsEnabled);
            _toggle.SetValueWithoutNotify(Session().TryGetStaged(_setting.Toggle, out bool stagedToggle) ? stagedToggle : _setting.Toggle.Value);
        }

        public override void Step(int direction)
        {
            float step = _setting.Step > 0 ? _setting.Step : (_setting.Max - _setting.Min) / 20f;
            Write(_slider.value + step * direction);
        }
    }

    internal sealed class ChoiceRow : SettingRow
    {
        private readonly IChoiceSetting _choice;

        private readonly ISettingsChoiceControl _control;

        public ChoiceRow(Setting setting, IChoiceSetting choice, VisualTreeAsset template, Func<SettingsSession> session)
            : base(setting, template, SettingsWindowTemplates.RowChoiceName, session)
        {
            _choice = choice;
            _control = SettingsWindowTemplates.Require<ISettingsChoiceControl>(Element, SettingsWindowTemplates.RowChoiceName, template);
            _control.UnmatchedText = choice.UnmatchedLabel;
            _control.SetChoices(choice.Options.Select(option => option.Label).ToArray());
            ((VisualElement)_control).RegisterCallback<ChangeEvent<int>>(evt => Write(evt.newValue));
        }

        private int Current => Session().TryGetStagedChoice(_choice, out int staged) ? staged : _choice.SelectedIndex;

        private void Write(int index)
        {
            if (index < 0) return;
            if (_choice.ApplyMode == SettingApplyMode.Confirm) Session().StageChoice(_choice, index);
            else _choice.SelectedIndex = index;
        }

        protected override void RefreshValue()
        {
            for (int i = 0; i < _choice.Options.Count; i++)
            {
                var state = _choice.Options[i].EvaluateEditState();
                _control.SetOptionState(i, state.IsVisible, state.IsEnabled);
            }
            _control.SetValueWithoutNotify(Current);
        }

        public override void Step(int direction)
        {
            for (int i = Current + direction; i >= 0 && i < _choice.Options.Count; i += direction)
            {
                if (!_choice.Options[i].EvaluateEditState().IsEnabled) continue;
                Write(i);
                return;
            }
        }
    }

    internal sealed class ActionRow : SettingRow
    {
        private readonly ActionSetting _setting;

        public ActionRow(ActionSetting setting, VisualTreeAsset template, Func<SettingsSession> session)
            : base(setting, template, SettingsWindowTemplates.RowButtonName, session)
        {
            _setting = setting;
            var button = SettingsWindowTemplates.Require<Button>(Element, SettingsWindowTemplates.RowButtonName, template);
            button.text = setting.ActionLabel;
            button.clicked += setting.Invoke;
        }

        protected override void RefreshValue()
        {
        }

        public override void Submit() => _setting.Invoke();
    }
}
