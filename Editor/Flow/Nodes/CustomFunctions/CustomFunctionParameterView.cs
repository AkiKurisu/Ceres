using System;
using System.Linq;
using Ceres.Graph.Flow.CustomFunctions;
using Ceres.Utilities;
using Chris.Serialization;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph.Flow.CustomFunctions
{
    public class CustomFunctionParameterView: VisualElement
    {
        protected CustomFunctionParameterView()
        {
            _arrayToggle = new Toggle("Is Array");
            _arrayToggle.RegisterValueChangedCallback(changeEvent =>
            {
                if (Parameter == null) return;
                Parameter.isArray = changeEvent.newValue;
                NotifyValueChange();
            });
            Add(_arrayToggle);
            
            _typeContainer = new TypeContainer(typeof(object));
            Add(_typeContainer);
            
            var button = new Button
            {
                text = "Assign Type"
            };
            button.clicked += () =>
            {
                var provider = ScriptableObject.CreateInstance<ObjectTypeSearchWindow>();
                provider.Initialize(type =>
                {
                    if (type == null)
                    {
                        Parameter.serializedTypeString = string.Empty;
                        _typeContainer.SetType(typeof(object));
                    }
                    else
                    {
                        Parameter.serializedTypeString = SerializedType.ToString(type);
                        _typeContainer.SetType(type);
                    }

                    NotifyValueChange();
                }, typeof(object), types => types.Where(x=> x.IsCeresSerializable()));
                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);
            };
            Add(button);
        }

        private CustomFunctionParameter _parameter;

        private readonly Toggle _arrayToggle;

        private readonly TypeContainer _typeContainer;

        protected CustomFunctionParameter Parameter
        {
            get => _parameter;
            set
            {
                if (_parameter == value) return;
                _parameter = value;
                UpdateView();
            }
        }

        /// <summary>
        /// Update parameter view
        /// </summary>
        public virtual void UpdateView()
        {
            Type objectType;
            try
            {
                objectType =  SerializedType.FromString(Parameter.serializedTypeString);
            }
            catch
            {
                objectType = null;
            }

            _typeContainer.SetType(objectType ?? typeof(object));
            _arrayToggle.value = Parameter.isArray;
        }

        protected void NotifyValueChange()
        {
            using var changeEvent = ChangeEvent<CustomFunctionParameter>.GetPooled(Parameter, Parameter);
            changeEvent.target = this;
            SendEvent(changeEvent);
        }
    }

    public class CustomFunctionInputParameterView : CustomFunctionParameterView
    {
        private readonly TextField _nameText;

        private CustomFunctionInputParameter InputParameter => (CustomFunctionInputParameter)Parameter;

        public CustomFunctionInputParameterView()
        {
            Insert(0, _nameText = new TextField());
            _nameText.RegisterValueChangedCallback(changeEvent =>
            {
                if (Parameter == null) return;
                InputParameter.parameterName = changeEvent.newValue;
                NotifyValueChange();
            });
        }
        
        public void BindParameter(int index, CustomFunctionParameter inputParameter)
        {
            _nameText.label = $"Parameter {index}";
            Parameter = inputParameter;
        }
        
        public override void UpdateView()
        {
            base.UpdateView();
            _nameText.value = InputParameter.parameterName;
        }
    }
    
    public class CustomFunctionOutputParameterView : CustomFunctionParameterView
    {
        private readonly Toggle _hasReturnToggle;

        private CustomFunctionOutputParameter OutputParameter => (CustomFunctionOutputParameter)Parameter;

        public CustomFunctionOutputParameterView()
        {
            Insert(0, _hasReturnToggle = new Toggle("Has Return"));
            _hasReturnToggle.RegisterValueChangedCallback(changeEvent =>
            {
                if (Parameter == null) return;
                OutputParameter.hasReturn = changeEvent.newValue;
                NotifyValueChange();
            });
        }
        
        public void BindParameter(CustomFunctionOutputParameter outputParameter)
        {
            Parameter = outputParameter;
        }
        
        public override void UpdateView()
        {
            base.UpdateView();
            _hasReturnToggle.value = OutputParameter.hasReturn;
        }
    }
}