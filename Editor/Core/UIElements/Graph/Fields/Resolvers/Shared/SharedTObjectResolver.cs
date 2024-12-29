using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public class SharedTObjectResolver<T> : FieldResolver<SharedTObjectField<T>, SharedUObject<T>> where T : UnityEngine.Object
    {
        public SharedTObjectResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {

        }
        
        protected override SharedTObjectField<T> CreateEditorField(FieldInfo fieldInfo)
        {
            return new SharedTObjectField<T>(fieldInfo.Name, fieldInfo);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo _)
        {
            return fieldValueType.IsSharedTObject() && fieldValueType.GenericTypeArguments.Length == 1 && fieldValueType.GenericTypeArguments[0] == typeof(T);
        }
    }
    public class SharedTObjectField<T> : BaseField<SharedUObject<T>>, IBindableField where T : UnityEngine.Object
    {
        private readonly bool _forceShared;
        
        private readonly VisualElement _foldout;
        
        private readonly Toggle _toggle;
        
        private CeresGraphView _graphView;
        
        private DropdownField _nameDropdown;
        
        private SharedVariable _bindExposedProperty;
        public SharedTObjectField(string label, FieldInfo fieldInfo) : base(label, null)
        {
            _forceShared = fieldInfo.GetCustomAttribute<ForceSharedAttribute>() != null;
            AddToClassList("SharedVariableField");
            _foldout = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };
            Add(_foldout);
            _toggle = new Toggle("Is Shared");
            _toggle.RegisterValueChangedCallback(evt => { value.IsShared = evt.newValue; OnToggle(evt.newValue); NotifyValueChange(); });
            if (_forceShared)
            {
                _toggle.value = true;
                return;
            }
            _foldout.Add(_toggle);
        }
        public void BindGraph(CeresGraphView graph)
        {
            _graphView = graph;
            graph.Blackboard.RegisterCallback<VariableChangeEvent>(evt =>
            {
                if (evt.ChangeType != VariableChangeType.NameChange) return;
                if (evt.Variable != _bindExposedProperty) return;
                _nameDropdown.value = value.Name = evt.Variable.Name;
            });
            OnToggle(_toggle.value);
        }
        private static List<string> GetList(CeresGraphView graphView)
        {
            return graphView.SharedVariables
            .Where(x => x is SharedUObject sharedObject 
                        && sharedObject.GetValueType().IsAssignableTo(typeof(T)) )
            .Select(v => v.Name)
            .ToList();
        }
        private void BindProperty()
        {
            if (_graphView == null) return;
            _bindExposedProperty = _graphView.SharedVariables
                .FirstOrDefault(x => x is SharedUObject sharedObject 
                                     && sharedObject.GetValueType().IsAssignableTo(typeof(T)) 
                                     && x.Name.Equals(value.Name));
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
            value.Name = value.Name ?? string.Empty;
            int index = list.IndexOf(value.Name);
            _nameDropdown = new DropdownField($"Shared {typeof(T).Name}", list, index);
            _nameDropdown.RegisterCallback<MouseEnterEvent>((evt) => { _nameDropdown.choices = GetList(_graphView); });
            _nameDropdown.RegisterValueChangedCallback(evt => { value.Name = evt.newValue; BindProperty(); NotifyValueChange(); });
            _foldout.Insert(0, _nameDropdown);
        }
        private void RemoveNameDropDown()
        {
            if (_nameDropdown != null) _foldout.Remove(_nameDropdown);
            _nameDropdown = null;
        }
        private void RemoveValueField()
        {
            if (ValueField != null) _foldout.Remove(ValueField);
            ValueField = null;
        }
        private void AddValueField()
        {
            ValueField = new ObjectField()
            {
                objectType = typeof(T)
            };
            ValueField.RegisterValueChangedCallback(evt => { value.Value = (T)evt.newValue; NotifyValueChange(); });
            if (value != null) ValueField.value = value.Value;
            _foldout.Insert(0, ValueField);
        }
        public sealed override SharedUObject<T> value
        {
            get => base.value; set
            {
                if (value != null) base.value = value.Clone() as SharedUObject<T>;
                else base.value = new SharedUObject<T>();
                if (_forceShared)
                {
                    var sharedTObject = base.value;
                    if (sharedTObject != null)
                        sharedTObject.IsShared = true;
                }

                Repaint();
            }
        }
        private ObjectField ValueField { get; set; }
        public void Repaint()
        {
            _toggle.value = value.IsShared;
            if (ValueField != null) ValueField.value = value.Value;
            BindProperty();
            OnToggle(value.IsShared);
            NotifyValueChange();
        }
        protected void NotifyValueChange()
        {
            using ChangeEvent<SharedUObject<T>> changeEvent = ChangeEvent<SharedUObject<T>>.GetPooled(value, value);
            changeEvent.target = this;
            SendEvent(changeEvent);
        }
    }
}
