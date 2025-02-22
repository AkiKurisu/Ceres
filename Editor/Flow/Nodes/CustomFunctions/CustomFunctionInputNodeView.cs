﻿using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.CustomFunctions;
using Ceres.Utilities;
using Chris.Serialization;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph.Flow.CustomFunctions
{
    public class CustomFunctionInputParameterView: VisualElement
    {
        private readonly TextField _nameText;
        
        public CustomFunctionInputParameterView()
        {
            Add(_nameText = new TextField());
            _nameText.RegisterValueChangedCallback(evt =>
            {
                if (Parameter == null) return;
                Parameter.parameterName = evt.newValue;
                NotifyValueChange();
            });
            _arrayToggle = new Toggle("Is Array");
            _arrayToggle.RegisterValueChangedCallback(evt =>
            {
                if (Parameter == null) return;
                Parameter.isArray = evt.newValue;
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

        private CustomFunctionInputParameter _parameter;

        private readonly Toggle _arrayToggle;

        private readonly TypeContainer _typeContainer;

        public CustomFunctionInputParameter Parameter
        {
            get => _parameter;
            set
            {
                if (_parameter == value) return;
                _parameter = value;
                UpdateView();
            }
        }

        private void UpdateView()
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
            _nameText.value = Parameter.parameterName;
        }

        public void BindParameter(int index, CustomFunctionInputParameter inputParameter)
        {
            _nameText.label = $"Parameter {index}";
            Parameter = inputParameter;
        }

        private void NotifyValueChange()
        {
            using var changeEvent = ChangeEvent<CustomFunctionInputParameter>.GetPooled(Parameter, Parameter);
            changeEvent.target = this;
            SendEvent(changeEvent);
        }
    }
    
    public class CustomFunctionInputSettingsView: NodeSettingsView
    {
        public List<CustomFunctionInputParameter> Parameters { get; } = new();
        
        private readonly ListView _listView;

        public Action OnListElementChange;
        
        public CustomFunctionInputSettingsView()
        {
            _listView = CreateListView();
            SettingsElement.AddToClassList(nameof(CustomFunctionInputSettingsView));
            SettingsElement.styleSheets.Add(CeresGraphView.GetOrLoadStyleSheet("Ceres/Flow/CustomFunctionInputSettingsView"));
            SettingsElement.Add(_listView);
            RefreshListViewItems();
        }
        
        private ListView CreateListView()
        {
            var view = new ListView(Parameters, 70f, OnMakeListItem, OnBindListItem)
            {
                selectionType = SelectionType.Multiple,
                reorderable = false,
                showAddRemoveFooter = true
            };
            view.Q<Button>("unity-list-view__add-button").clickable = new Clickable(OnRequestAddListItem);
            view.Q<Button>("unity-list-view__remove-button").clickable = new Clickable(OnRequestRemoveListItem);
            return view;
        }

        private void OnRequestRemoveListItem()
        {
            if (Parameters.Count == 0)
            {
                return;
            }
            if (_listView.selectedIndices.Any())
            {
                foreach (var removeParam in _listView.selectedIndices.Select(i => Parameters[i]))
                {
                    Parameters.Remove(removeParam);
                }
            }
            else
            {
                Parameters.RemoveAt(Parameters.Count - 1);
            }
            _listView.RefreshItems();
            OnListElementChange?.Invoke();
        }

        private void OnBindListItem(VisualElement e, int i)
        {
            ((CustomFunctionInputParameterView)e).BindParameter(i, Parameters[i]);
        }

        private VisualElement OnMakeListItem()
        {
            var view = new CustomFunctionInputParameterView();
            view.RegisterCallback<ChangeEvent<CustomFunctionInputParameter>>(_ =>
            {
                OnListElementChange?.Invoke();
            });
            return view;
        }
        
        private void OnRequestAddListItem()
        {
            if (Parameters.Count >= 6) return;
            Parameters.Add(new CustomFunctionInputParameter());
            _listView.RefreshItems();
            OnListElementChange?.Invoke();
        }
        
        public void RefreshListViewItems()
        {
            if (_listView == null) return;
            _listView.itemsSource = Parameters; 
            _listView.RefreshItems();
            OnListElementChange?.Invoke();
        }
    }
    
    
    [CustomNodeView(typeof(CustomFunctionInput))]
    public class CustomFunctionInputNodeView : ExecutableNodeView
    {
        private readonly CustomFunctionInputSettingsView _settingsView;
        
        private int _portLength;

        private readonly List<CeresPortView> _dynamicPortViews = new();

        private readonly PortArrayNodeReflection _nodeReflection;
        
        public CustomFunctionInputNodeView(Type type, CeresGraphView graphView) : base(type, graphView)
        {
            _nodeReflection = PortArrayNodeReflection.Get(type);
            _settingsView = this.CreateSettingsView<CustomFunctionInputSettingsView>();
            _settingsView.OnListElementChange = OnListElementChange;
            NodeElement.capabilities &= ~Capabilities.Copiable;
            NodeElement.capabilities &= ~Capabilities.Deletable;
        }

        private void OnListElementChange()
        {
            while (_portLength > _settingsView.Parameters.Count)
            {
                RemovePort(_portLength - 1);
            }
            while (_portLength < _settingsView.Parameters.Count)
            {
                AddPort(_portLength);
            }

            ReorderDynamicPorts();
        }

        public override void SetNodeInstance(CeresNode ceresNode)
        {
            var input = (CustomFunctionInput)ceresNode;
            if (input.parameters != null)
            {
                _settingsView.Parameters.AddRange(input.parameters);
                _settingsView.RefreshListViewItems();
            }
            base.SetNodeInstance(input);
        }

        public override ExecutableNode CompileNode()
        {
            var input = (CustomFunctionInput)base.CompileNode();
            input.parameters = _settingsView.Parameters.ToArray();
            return input;
        }

        private void AddPort(int index)
        {
            var portData = CeresPortData.FromFieldInfo(_nodeReflection.PortArrayField);
            portData.arrayIndex = index;
            _portLength++;
            var newPortView = PortViewFactory.CreateInstance(_nodeReflection.PortArrayField, this, portData);
            AddPortView(newPortView);
            _dynamicPortViews.Add(newPortView);
            newPortView.SetDisplayName(GetPortArrayElementDisplayName(index));
            newPortView.SetDisplayType(GetPortArrayElementDisplayType(index));
        }

        private void RemovePort(int index)
        {
            var portView = _dynamicPortViews[index];
            _portLength--;
            _dynamicPortViews.RemoveAt(index);
            RemovePortView(portView);
        }

        private void ReorderDynamicPorts()
        {
            for (int i = 0; i < _portLength; i++)
            {
                _dynamicPortViews[i].PortData.arrayIndex = i;
                _dynamicPortViews[i].SetDisplayName(GetPortArrayElementDisplayName(i));
                _dynamicPortViews[i].SetDisplayType(GetPortArrayElementDisplayType(i));
            }
        }

        private string GetPortArrayElementDisplayName(int index)
        {
            return _settingsView.Parameters[index].parameterName;
        }
        
        private Type GetPortArrayElementDisplayType(int index)
        {
            return _settingsView.Parameters[index].GetParameterType();
        }
    }
}