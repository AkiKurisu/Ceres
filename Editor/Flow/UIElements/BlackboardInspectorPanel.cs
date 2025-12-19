using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Graph;
using Chris.Serialization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using CBlackboard = Ceres.Graph.Blackboard;

namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Display an inspector ui for blackboard
    /// </summary>
    public class BlackboardInspectorPanel : VisualElement
    {
        private class Row : VisualElement
        {
            public Label Label { get; }
            
            public VisualElement Content { get; }
            
            public Row(SharedVariable sharedVariable)
            {
                AddToClassList("blackboard-inspector-row");

                Label = new Label($"{sharedVariable.GetType().Name} : {sharedVariable.Name}");
                Add(Label);
                
                Content = new VisualElement();
                Content.AddToClassList("blackboard-inspector-row-content");
                Add(Content);
            }
        }
        
        private readonly HashSet<ObserveProxyVariable> _observeProxies = new();
        
        private CBlackboard _blackboard;

        private CeresGraph _instance;

        private long _currentTimestamp;
        
        private readonly Action<CeresGraph> _onUpdate;
        
        private readonly Func<CeresGraph> _getGraphFunc;
        
        private readonly Func<long> _getTimestampFunc;

        private bool _isDisposed;
        
        private string _subtitle;
        
        private Label _subtitleLabel;
        
        /// <summary>
        /// Subtitle displayed on the right side of the title. If empty or null, the subtitle will be hidden.
        /// </summary>
        public string Subtitle
        {
            get => _subtitle;
            set
            {
                _subtitle = value;
                UpdateSubtitle();
            }
        }
        
        public BlackboardInspectorPanel(Func<CeresGraph> getGraphFunc, Func<long> getTimestampFunc, Action<CeresGraph> onUpdate)
        {
            _getGraphFunc = getGraphFunc;
            _instance = getGraphFunc();
            _blackboard = _instance.Blackboard;
            _onUpdate = onUpdate;
            _getTimestampFunc = getTimestampFunc;
            _currentTimestamp = _getTimestampFunc();
            
            // Load stylesheet
            styleSheets.Add(CeresGraphView.GetOrLoadStyleSheet("Ceres/Flow/BlackboardInspectorPanel"));
            AddToClassList("blackboard-inspector-panel");
            
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanelEvent);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanelEvent);
            
            BuildContent();
        }
        
        private void BuildContent()
        {
            // Clean up existing observe proxies before rebuilding
            foreach (var proxy in _observeProxies)
            {
                proxy.Dispose();
            }
            _observeProxies.Clear();
            
            Clear();
            
            var factory = FieldResolverFactory.Get();
            
            // Title container with main title and subtitle
            var titleContainer = new VisualElement();
            titleContainer.AddToClassList("blackboard-inspector-title-container");
            
            var titleLabel = new Label("Exposed Variables")
            {
                name = "blackboard-inspector-title"
            };
            titleContainer.Add(titleLabel);
            
            _subtitleLabel = new Label()
            {
                name = "blackboard-inspector-subtitle"
            };
            titleContainer.Add(_subtitleLabel);
            
            UpdateSubtitle();
            Add(titleContainer);
            
            var variables = _blackboard.GetExposedVariables();
            if (variables.Length == 0)
            {
                var emptyLabel = new Label("No exposed variables to display")
                {
                    name = "blackboard-inspector-empty"
                };
                Add(emptyLabel);
                return;
            }
            
            foreach (var variable in variables)
            {
                var row = new Row(variable);
                
                var fieldResolver = factory.Create(variable.GetType().GetField("value", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public));
                var valueField = fieldResolver.EditorField;
                fieldResolver.Restore(variable);
                fieldResolver.RegisterValueChangeCallback(obj =>
                {
                    var targetVariable = _instance.variables.First(sharedVariable => sharedVariable.Name == variable.Name);
                    targetVariable.SetValue(obj);
                    _onUpdate(_instance);
                });
                
                if (Application.isPlaying)
                {
                    var observeProxy = variable.Observe();
                    observeProxy.Register(x => fieldResolver.Value = x);
                    fieldResolver.Value = variable.GetValue();
                    // Disable since you should only edit global variable in source
                    if (variable.IsGlobal) valueField.SetEnabled(false);
                    valueField.tooltip = "Global variable can only be edited in source at runtime";
                    _observeProxies.Add(observeProxy);
                }
                
                if (valueField is TextField field)
                {
                    field.multiline = true;
                }
                
                if (variable is SharedUObject sharedUObject)
                {
                    var objectField = (ObjectField)valueField;
                    objectField.objectType = sharedUObject.GetValueType();
                    row.Label.text = $"{variable.GetType().Name} ({objectField.objectType.Name}):  {variable.Name}";
                }
                
                if (variable is SharedObject sharedObject)
                {
                    var info = typeof(SharedObject).GetField("serializedObject", BindingFlags.Public | BindingFlags.Instance);
                    var resolver = (SerializedObjectFieldResolver)FieldResolverFactory.Get().Create(info);
                    var baseField = resolver.BaseField;
                    baseField.value = (SerializedObjectBase)info!.GetValue(sharedObject);
                    baseField.label = string.Empty;
                    baseField.onJsonValueUpdate = serializedObjectBase =>
                    {
                        var targetVariable = _instance.variables.First(sharedVariable => sharedVariable.Name == variable.Name);
                        info!.SetValue(targetVariable, serializedObjectBase);
                        _onUpdate(_instance);
                    };
                    row.Content.Add(baseField);
                }
                else
                {
                    valueField.AddToClassList("blackboard-inspector-value-field");
                    row.Content.Add(valueField);
                }
                
                // Button container for right-aligned buttons
                var buttonContainer = new VisualElement();
                buttonContainer.AddToClassList("blackboard-inspector-button-container");
                
                // Is Global Field
                var globalToggle = new Button
                {
                    text = "Is Global",
                    tooltip = "Set variable whether can be bound to global variables"
                };
                globalToggle.AddToClassList("blackboard-inspector-global-toggle");
                if (!Application.isPlaying)
                {
                    globalToggle.clicked += () =>
                    {
                        variable.IsGlobal = !variable.IsGlobal;
                        SetToggleButtonClass(globalToggle, variable.IsGlobal);
                        _onUpdate(_instance);
                    };
                }
                SetToggleButtonClass(globalToggle, variable.IsGlobal);
                buttonContainer.Add(globalToggle);
                
                // Delete Variable
                if (!Application.isPlaying)
                {
                    var deleteButton = new Button(() =>
                    {
                        Remove(row);
                        _instance.variables.RemoveAll(sharedVariable => sharedVariable.Name == variable.Name);
                        _onUpdate(_instance);
                    });
                    deleteButton.AddToClassList("blackboard-inspector-delete-button");
                    
                    var iconContent = EditorGUIUtility.IconContent("d_TreeEditor.Trash");
                    deleteButton.style.backgroundImage = iconContent.image as Texture2D;
                    deleteButton.style.unityBackgroundImageTintColor = new StyleColor(new Color(1f, 0.7f, 0.7f, 1f));
                    deleteButton.tooltip = "Delete shared variable";
                    buttonContainer.Add(deleteButton);
                }
                
                row.Content.Add(buttonContainer);
                Add(row);
            }

            return;
            
            void SetToggleButtonClass(Button button, bool isOn)
            {
                button.RemoveFromClassList("global-on");
                button.RemoveFromClassList("global-off");
                button.AddToClassList(isOn ? "global-on" : "global-off");
            }
        }

        private void OnDetachFromPanelEvent(DetachFromPanelEvent _)
        {
            _isDisposed = true;
            foreach (var proxy in _observeProxies)
            {
                proxy.Dispose();
            }
            _observeProxies.Clear();
            _instance?.Dispose();
            _instance = null;
        }
        
        private void OnAttachToPanelEvent(AttachToPanelEvent _)
        {
            // Schedule periodic checks for external changes
            schedule.Execute(CheckAndRefresh).Every(200);
        }
        
        /// <summary>
        /// Check if persistent data has changed and refresh UI if needed
        /// </summary>
        private void CheckAndRefresh()
        {
            if (_isDisposed) return;
            
            var sourceTimestamp = _getTimestampFunc();
            if (_currentTimestamp != sourceTimestamp)
            {
                _instance?.Dispose();
                _instance = _getGraphFunc();
                _currentTimestamp = sourceTimestamp;
                _blackboard = _instance.Blackboard;
                BuildContent();
            }
        }
        
        /// <summary>
        /// Update subtitle visibility and text
        /// </summary>
        private void UpdateSubtitle()
        {
            if (_subtitleLabel == null) return;
            
            if (string.IsNullOrEmpty(_subtitle))
            {
                _subtitleLabel.style.display = DisplayStyle.None;
            }
            else
            {
                _subtitleLabel.style.display = DisplayStyle.Flex;
                _subtitleLabel.text = _subtitle;
            }
        }
    }
}
