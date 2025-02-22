using System;
using System.Linq;
using Ceres.Utilities;
using Chris.Serialization;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph
{
    internal class SerializedObjectSettings : VisualElement
    {
        public Action<Type> OnTypeChange { get; set; }

        private SerializedObjectBase _serializedObject;

        private readonly Toggle _arrayToggle;

        private readonly TypeContainer _typeContainer;

        public SerializedObjectBase SerializedObject
        {
            get => _serializedObject;
            set
            {
                if (_serializedObject == value) return;
                _serializedObject = value;
                UpdateView();
            }
        }

        public SerializedObjectSettings()
        {
            _arrayToggle = new Toggle("Is Array");
            _arrayToggle.RegisterValueChangedCallback(evt =>
            {
                SerializedObject.isArray = evt.newValue;
            });
            Add(_arrayToggle);
            
            _typeContainer = new TypeContainer(typeof(object));
            Add(_typeContainer);
            
            var button = new Button
            {
                text = "Assign Object Type"
            };
            button.clicked += () =>
            {
                var provider = ScriptableObject.CreateInstance<ObjectTypeSearchWindow>();
                provider.Initialize(type =>
                {
                    if (type == null)
                    {
                        SerializedObject.serializedTypeString = string.Empty;
                        _typeContainer.SetType(typeof(object));
                    }
                    else
                    {
                        SerializedObject.serializedTypeString = SerializedType.ToString(type);
                        _typeContainer.SetType(type);
                    }

                    OnTypeChange?.Invoke(type);
                }, typeof(object), types => types.Where(x => x.IsCeresSerializable()));
                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);
            };
            Add(button);
        }
        

        public SerializedObjectSettings(SerializedObjectBase serializedObject): this()
        {
            SerializedObject = serializedObject;
        }

        private void UpdateView()
        {
            Type objectType;
            try
            {
                objectType =  SerializedType.FromString(_serializedObject.serializedTypeString);
            }
            catch
            {
                objectType = null;
            }

            _typeContainer.SetType(objectType ?? typeof(object));
            _arrayToggle.value = _serializedObject.isArray;
        }
    }
}