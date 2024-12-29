using System;
using System.Reflection;
using Chris.Serialization;
using Chris.Serialization.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    [Ordered]
    public class SerializedObjectFieldResolver : FieldResolver<SerializedObjectField , SerializedObjectBase>
    {
        public SerializedObjectFieldResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override SerializedObjectField CreateEditorField(FieldInfo fieldInfo)
        {
            return new SerializedObjectField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo fieldInfo)
        {
            return typeof(SerializedObjectBase).IsAssignableFrom(fieldValueType);
        }
    }
    
    public class SerializedObjectField : BaseField<SerializedObjectBase>
    {
        private SoftObjectHandle _wrapperHandle;
        
        public SerializedObjectField(string label, IMGUIContainer container) : base(label, container)
        {
            container.onGUIHandler = OnGUI;
        }
        
        public SerializedObjectField(string label) : this(label, new IMGUIContainer())
        {

        }

        private void OnGUI()
        {
            var elementType = value?.GetBoxType();
            if (elementType == null)
            {
                return;
            }
            var handle = new SoftObjectHandle(value.objectHandle);
            var wrapper = SerializedObjectWrapperManager.CreateWrapper(elementType, ref handle);
            value.objectHandle = handle.Handle;
            if (!wrapper) return;

            EditorGUI.BeginChangeCheck();
            SerializedObjectWrapperDrawer.DrawGUILayout(wrapper);
            if (EditorGUI.EndChangeCheck())
            {
                value.jsonData = JsonUtility.ToJson(wrapper.Value);
            }
        }

        private void Restore()
        {
            var elementType = _value.GetBoxType();
            if (elementType == null)
            {
                return;
            }
            var handle = new SoftObjectHandle(_value.objectHandle);
            var wrapper = SerializedObjectWrapperManager.CreateWrapper(elementType, ref handle);
            _value.objectHandle = handle.Handle;
            if (!wrapper) return;
            if (!string.IsNullOrEmpty(value.jsonData))
            {
                try
                {
                    wrapper.Value = JsonUtility.FromJson(_value.jsonData, elementType);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private SerializedObjectBase _value;

        public override SerializedObjectBase value
        {
            get => _value;
            set
            {
                _value = value;
                if (_value != null)
                {
                    Restore();
                }
            }
        }
    }
}
