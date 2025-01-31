using System;
using System.Reflection;
using Ceres.Annotations;
using Chris;
using Chris.Serialization;
using Chris.Serialization.Editor;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    [Ordered]
    public class WrapFieldResolver<T> : FieldResolver<WrapField<T>, T>
    {
        public WrapFieldResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override WrapField<T> CreateEditorField(FieldInfo fieldInfo)
        {
            return new WrapField<T>(fieldInfo.Name, fieldInfo);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo fieldInfo)
        {
            return fieldValueType == typeof(T) && fieldInfo.GetCustomAttribute<WrapFieldAttribute>() != null;
        }
    }
    
    public class WrapField<T> : BaseField<T>
    {
        private SerializedObjectWrapper<T> _instance;
        
        private SerializedObject _serializedObject;
        
        private SerializedProperty _serializedProperty;
        
        private SoftObjectHandle _wrapperHandle;

        private readonly FieldInfo _fieldInfo;

        private WrapField(string label, FieldInfo fieldInfo, IMGUIContainer container) : base(label, container)
        {
            _fieldInfo = fieldInfo;
            container.onGUIHandler = OnGUI;
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                SerializedObjectWrapperManager.DestroyWrapper(_wrapperHandle);
            });
        }
        
        public WrapField(string label, FieldInfo fieldInfo) : this(label, fieldInfo, new IMGUIContainer())
        {

        }
        
        private SerializedObjectWrapper<T> GetInstance()
        {
            if (_instance) return _instance;
            _instance = (SerializedObjectWrapper<T>)SerializedObjectWrapperManager.CreateFieldWrapper(_fieldInfo, ref _wrapperHandle);
            _instance.Value = value;
            _serializedObject = new SerializedObject(_instance);
            _serializedProperty = _serializedObject.FindProperty("m_Value");
            _instance.ValueChange.Subscribe(OnValueChange);
            return _instance;
        }

        private void OnGUI()
        {
            GetInstance();
            _serializedObject.Update();
            EditorGUILayout.PropertyField(_serializedProperty, GUIContent.none);
            _serializedObject.ApplyModifiedProperties();
        }
        
        public sealed override T value
        {
            get => base.value;
            set
            {
                var instance = GetInstance();
                if (value == null)
                {
                    instance.Value = (T)Activator.CreateInstance(typeof(T));
                }
                else
                {
                    instance.Value = ReflectionUtility.DeepCopy(value);
                }
                OnValueChange((T)instance.Value);
            }
        }

        private void OnValueChange(T newValue)
        {
            var oldValue = base.value;
            using var evt = ChangeEvent<T>.GetPooled(oldValue, newValue);
            evt.target = this;
            SendEvent(evt);
            SetValueWithoutNotify(newValue);
        }
    }
}
