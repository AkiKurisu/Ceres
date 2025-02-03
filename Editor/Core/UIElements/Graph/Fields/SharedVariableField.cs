using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Graph;
using Chris.Serialization;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public abstract class SharedVariableField<TVariable, TKValue> : BaseField<TVariable>, IBindableField where TVariable : SharedVariable<TKValue>, new()
    {
        private readonly bool _forceShared;
        
        private readonly VisualElement _sharedVariableContainer;
        
        private readonly Toggle _toggle;
        
        private CeresGraphView _graphView;
        
        private DropdownField _nameDropdown;
        
        private SharedVariable _bindExposedProperty;
        
        private readonly Type _bindType;

        protected BaseField<TKValue> ValueField { get; private set; }

        protected SharedVariableField(string label, Type objectType, FieldInfo fieldInfo) : base(label, null)
        {
            _forceShared = fieldInfo.GetCustomAttribute<ForceSharedAttribute>() != null;
            AddToClassList("SharedVariableField");
            _sharedVariableContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };
            Add(_sharedVariableContainer);
            _bindType = objectType;
            _toggle = new Toggle("Is Shared");
            _toggle.RegisterValueChangedCallback(evt => { value.IsShared = evt.newValue; OnToggle(evt.newValue); NotifyValueChange(); });
            if (_forceShared)
            {
                _toggle.value = true;
                return;
            }
            _sharedVariableContainer.Add(_toggle);
        }
        
        public void BindGraph(CeresGraphView graph)
        {
            _graphView = graph;
            _graphView.Blackboard.RegisterCallback<VariableChangeEvent>(OnVariableChange);
            OnToggle(_toggle.value);
        }
        
        private void OnVariableChange(VariableChangeEvent evt)
        {
            if (evt.ChangeType != VariableChangeType.Name) return;
            if (evt.Variable != _bindExposedProperty) return;
            _nameDropdown.value = value.Name = evt.Variable.Name;
        }
        
        private static List<string> GetList(CeresGraphView graphView)
        {
            return graphView.SharedVariables
                            .Where(x => x.GetType() == typeof(TVariable))
                            .Select(v => v.Name)
                            .ToList();
        }
        
        private void BindProperty()
        {
            if (_graphView == null) return;
            _bindExposedProperty = _graphView.SharedVariables.FirstOrDefault(x => x.GetType() == typeof(TVariable) && x.Name.Equals(value.Name));
        }
        
        private void OnToggle(bool isShared)
        {
            if (isShared)
            {
                RemoveNameDropDown();
                if (value != null && _graphView != null) AddNameDropDown();
                RemoveValueField();
            }
            else
            {
                RemoveNameDropDown();
                RemoveValueField();
                AddValueField();
            }
        }
        
        private void AddNameDropDown()
        {
            var list = GetList(_graphView);
            value.Name ??= string.Empty;
            int index = list.IndexOf(value.Name);
            string dropDownLabelText = SerializedType.GenericType(_bindType).Name;
            _nameDropdown = new DropdownField(dropDownLabelText, list, index);
            _nameDropdown.RegisterCallback<MouseEnterEvent>((evt) => { _nameDropdown.choices = GetList(_graphView); });
            _nameDropdown.RegisterValueChangedCallback(evt => { value.Name = evt.newValue; BindProperty(); NotifyValueChange(); });
            _sharedVariableContainer.Insert(0, _nameDropdown);
        }
        
        private void RemoveNameDropDown()
        {
            if (_nameDropdown != null) _sharedVariableContainer.Remove(_nameDropdown);
            _nameDropdown = null;
        }
        
        private void RemoveValueField()
        {
            if (ValueField != null) _sharedVariableContainer.Remove(ValueField);
            ValueField = null;
        }
        
        private void AddValueField()
        {
            ValueField = CreateValueField();
            ValueField.label = "Value";
            ValueField.RegisterValueChangedCallback(evt => { value.Value = evt.newValue; NotifyValueChange(); });
            if (value != null) ValueField.value = value.Value;
            _sharedVariableContainer.Insert(0, ValueField);
        }
        
        protected abstract BaseField<TKValue> CreateValueField();
        
        public sealed override TVariable value
        {
            get => base.value; set
            {
                if (value != null) base.value = value.Clone() as TVariable;
                else base.value = new TVariable();
                if (_forceShared) base.value!.IsShared = true;
                Repaint();
                NotifyValueChange();
            }
        }

        public void SetValue(TKValue newValue)
        {
            value.Value = newValue;
            if (ValueField != null) ValueField.value = value.Value;
        }
        
        /// <summary>
        /// Repaint <see cref="SharedVariableField{TVariable,TKValue}"/> and rebind property
        /// </summary>
        public void Repaint()
        {
            _toggle.value = value.IsShared;
            if (ValueField != null) ValueField.value = value.Value;
            BindProperty();
            OnToggle(value.IsShared);
            OnRepaint();
        }
        
        protected void NotifyValueChange()
        {
            using ChangeEvent<TVariable> changeEvent = ChangeEvent<TVariable>.GetPooled(value, value);
            changeEvent.target = this;
            SendEvent(changeEvent);
        }
        
        protected virtual void OnRepaint() { }
    }

}

;