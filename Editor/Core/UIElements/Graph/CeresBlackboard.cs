using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Chris;
using Chris.Serialization;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UObject = UnityEngine.Object;
namespace Ceres.Editor.Graph
{
    /// <summary>
    /// Blackboard row for <see cref="SharedVariable"/>
    /// </summary>
    public class CeresBlackboardVariableRow: BlackboardRow
    {
        private static readonly CustomStyleProperty<Color> PortColorProperty = new("--port-color");

        public SharedVariable Variable { get; }
        
        public BlackboardField BlackboardField { get; }

        private readonly StyleSheet _portSheet;
        
        public CeresBlackboardVariableRow(SharedVariable variable, BlackboardField blackboardField, VisualElement propertyView) : base(blackboardField, propertyView)
        {
            Variable = variable;
            BlackboardField = blackboardField;
            _portSheet = UIElementResourceUtil.LoadResource<StyleSheet>("StyleSheets/GraphView/Port.uss");
            styleSheets.Add(_portSheet);
            styleSheets.Add(CeresGraphView.GetOrLoadStyleSheet("Ceres/CeresPortElement"));
            AddToClassList("port");
            AddToClassList("CeresPortElement");
            AddToClassList("type" + ReflectionUtility.GetGenericArgumentType(variable.GetType()).Name);
            AddToClassList("type" + variable.GetValueType().Name);
        }
        
        protected override void OnCustomStyleResolved(ICustomStyle styles)
        {
            base.OnCustomStyleResolved(styles);
            if (styles.TryGetValue(PortColorProperty, out var color))
            {
                var border = this.Q<Pill>().Children().First().Children().First();
                border.style.borderTopColor = border.style.borderBottomColor =
                    border.style.borderRightColor = border.style.borderLeftColor = color;
                /* Once we get color remove port sheet */
                styleSheets.Remove(_portSheet);
            }
        }
    }
    
    public class CeresBlackboard : Blackboard, IVariableSource
    {
        public bool AlwaysExposed { get; set; }

        private ScrollView ScrollView { get; }
        
        public List<SharedVariable> SharedVariables { get; }

        private readonly HashSet<ObserveProxyVariable> _observeProxies = new();
        
        private bool _isDestroyed;
        
        private const string NullObjectType = "Object Type: <Null>";

        public const string SharedVariableSection = "Shared Variables";
        
        public CeresBlackboard(IVariableSource source ,GraphView graphView) : base(graphView)
        {
            SharedVariables = source.SharedVariables;
            var header = this.Q("header");
            header.style.height = new StyleLength(50);
            
            styleSheets.Add(CeresGraphView.GetOrLoadStyleSheet("Ceres/CeresBlackboard"));
            Add(ScrollView = new ScrollView());
            
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<DetachFromPanelEvent>(OnDispose);
            if (!Application.isPlaying) InitRequestDelegate();
        }
        
        protected CeresBlackboard(CeresGraphView graphView) : this(graphView, graphView)
        {
            
        }

        public BlackboardSection GetOrAddSection(string sectionName)
        {
            var section = ScrollView.Query<BlackboardSection>()
                                    .ToList()
                                    .FirstOrDefault(x => x.title == sectionName);
            if (section == null)
            {
                ScrollView.Add(section = new BlackboardSection { title = sectionName });
            }
            return section;
        }
        
        private void OnDispose(DetachFromPanelEvent _)
        {
            _isDestroyed = true;
            foreach (var proxy in _observeProxies)
            {
                proxy.Dispose();
            }
        }

        protected virtual void CreateBlackboardMenu(GenericMenu menu)
        {
            CreateBuiltInSharedVariableMenu(menu);
        }
        
        protected void CreateBuiltInSharedVariableMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Int"), false, () => AddVariable(new SharedInt(), true));
            menu.AddItem(new GUIContent("Float"), false, () => AddVariable(new SharedFloat(), true));
            menu.AddItem(new GUIContent("Bool"), false, () => AddVariable(new SharedBool(), true));
            menu.AddItem(new GUIContent("Vector2Int"), false, () => AddVariable(new SharedVector2Int(), true));
            menu.AddItem(new GUIContent("Vector2"), false, () => AddVariable(new SharedVector2(), true));
            menu.AddItem(new GUIContent("Vector3"), false, () => AddVariable(new SharedVector3(), true));
            menu.AddItem(new GUIContent("Vector3Int"), false, () => AddVariable(new SharedVector3Int(), true));
            menu.AddItem(new GUIContent("String"), false, () => AddVariable(new SharedString(), true));
            menu.AddItem(new GUIContent("Unity Object"), false, () => AddVariable(new SharedUObject(), true));
            menu.AddItem(new GUIContent("Object"), false, () => AddVariable(new SharedObject(), true));
        }
        
        private void InitRequestDelegate()
        {
            addItemRequested = _ =>
            {
                var menu = new GenericMenu();
                CreateBlackboardMenu(menu);
                menu.ShowAsContext();
            };
            editTextRequested = EditTextRequested;
        }

        private void EditTextRequested(Blackboard blackboard, VisualElement element, string newValue)
        {
            var oldPropertyName = ((BlackboardField)element).text;
            var index = SharedVariables.FindIndex(x => x.Name == oldPropertyName);
            if (string.IsNullOrEmpty(newValue))
            {
                ScrollView.RemoveAt(index + 1);
                SharedVariables.RemoveAt(index);
                return;
            }
            if (SharedVariables.Any(x => x.Name == newValue))
            {
                EditorUtility.DisplayDialog("Error", "A variable with the same name already exists!",
                    "OK");
                return;
            }
            var targetIndex = SharedVariables.FindIndex(x => x.Name == oldPropertyName);
            SharedVariables[targetIndex].Name = newValue;
            NotifyVariableChanged(SharedVariables[targetIndex], VariableChangeType.Name);
            ((BlackboardField)element).text = newValue;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            contentContainer.style.height = layout.height - 50;
        }
        
        public void EditVariable(string variableName)
        {
            var index = SharedVariables.FindIndex(x => x.Name == variableName);
            if (index < 0) return;
            var field = ScrollView.Query<BlackboardField>().AtIndex(index);
            ScrollView.ScrollTo(field);
            field.OpenTextEditor();
        }
        
        public void AddVariable(SharedVariable variable, bool fireEvents)
        {
            /* New variable */
            if (string.IsNullOrEmpty(variable.Name)) variable.Name = variable.GetType().Name;
            var localPropertyName = variable.Name;
            int index = 1;
            while (SharedVariables.Any(x => x.Name == localPropertyName))
            {
                localPropertyName = $"{variable.Name}{index++}";
            }
            variable.Name = localPropertyName;
            if (AlwaysExposed) variable.IsExposed = true;
            SharedVariables.Add(variable);
            
            /* Create field */
            var field = new BlackboardField { text = localPropertyName, typeText = variable.GetType().Name };
            field.capabilities &= ~Capabilities.Movable;
            if (Application.isPlaying)
            {
                field.capabilities &= ~Capabilities.Renamable;
            }
            field.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                RemoveVariable(variable, true);
            });
            
            /* Create editable value field */
            var info = variable.GetType().GetField("value", BindingFlags.NonPublic | BindingFlags.Instance);
            VisualElement valueField = null;
            IFieldResolver fieldResolver = null;
            if(info!.FieldType != typeof(object))
            {
                fieldResolver = FieldResolverFactory.Get().Create(info);
            }
            if (fieldResolver != null)
            {
                valueField = fieldResolver.EditorField;
                fieldResolver.Restore(variable);
                fieldResolver.RegisterValueChangeCallback((obj) =>
                {
                    var variableIndex = SharedVariables.FindIndex(x => x.Name == variable.Name);
                    SharedVariables[variableIndex].SetValue(obj);
                    NotifyVariableChanged(variable, VariableChangeType.Value);
                });
                if (Application.isPlaying)
                {
                    var observe = variable.Observe();
                    observe.Register(x => fieldResolver.Value = x);
                    _observeProxies.Add(observe);
                    fieldResolver.Value = variable.GetValue();
                    // Disable since global variable should only be edited in source
                    if (variable.IsGlobal)
                    {
                        valueField.SetEnabled(false);
                        valueField.tooltip = "Global variable can only edited in source at runtime";
                    }
                }
            }
            var row = CreateVariableBlackboardRow(variable, field, valueField);
            AddVariableRow(variable, row);
            if (fireEvents) NotifyVariableChanged(variable, VariableChangeType.Create);
        }

        protected virtual void AddVariableRow(SharedVariable variable, BlackboardRow blackboardRow)
        {
            GetOrAddSection(SharedVariableSection).Add(blackboardRow);
        }

        /// <summary>
        /// Whether variable can be exposed in blackboard
        /// </summary>
        /// <param name="variable"></param>
        /// <returns></returns>
        protected virtual bool CanVariableExposed(SharedVariable variable)
        {
            return true;
        }

        /// <summary>
        /// Create <see cref="BlackboardRow"/> for variable
        /// </summary>
        /// <param name="variable"></param>
        /// <param name="blackboardField"></param>
        /// <param name="valueField"></param>
        /// <returns></returns>
        protected virtual BlackboardRow CreateVariableBlackboardRow(SharedVariable variable, BlackboardField blackboardField, VisualElement valueField)
        {
            var propertyView = new VisualElement
            {
                name = "BlackboardProperty"
            };
            if (!AlwaysExposed && CanVariableExposed(variable))
            {
                var toggle = new Toggle("Exposed")
                {
                    value = variable.IsExposed
                };
                if (Application.isPlaying)
                {
                    toggle.SetEnabled(false);
                }
                else
                {
                    toggle.RegisterValueChangedCallback(x =>
                    {
                        var index = SharedVariables.FindIndex(sharedVariable => sharedVariable.Name == variable.Name);
                        SharedVariables[index].IsExposed = x.newValue;
                        NotifyVariableChanged(variable, VariableChangeType.Value);
                    });
                }
                propertyView.Add(toggle);
            }
            
            if(valueField != null)
            {
                propertyView.Add(valueField);
                /* Special case for UObject that need type settings */
                if (variable is SharedUObject sharedUObject)
                {
                    propertyView.Add(CreateTypeSettingsView(sharedUObject, (ObjectField)valueField));
                }
            }
            else
            {
                /* Special case for object that not use m_Value field and need type settings */
                if (variable is SharedObject sharedObject)
                {
                    var info = typeof(SharedObject).GetField("serializedObject", BindingFlags.Public | BindingFlags.Instance);
                    var resolver = (SerializedObjectFieldResolver)FieldResolverFactory.Get().Create(info);
                    var field = resolver.BaseField;
                    field.value = (SerializedObjectBase)info!.GetValue(sharedObject);
                    field.label = string.Empty;
                    propertyView.Add(field);
                    propertyView.Add(CreateTypeSettingsView(sharedObject));
                }
            }
            var sa = new CeresBlackboardVariableRow(variable, blackboardField, propertyView);
            sa.AddManipulator(new ContextualMenuManipulator((evt) => BuildBlackboardMenu(evt, sa)));
            return sa;
        }
        public void RemoveVariable(SharedVariable variable, bool fireEvents)
        {
            if(_isDestroyed) return;
            var row = ScrollView.Query<CeresBlackboardVariableRow>()
                                .ToList()
                                .FirstOrDefault(x=>x.Variable == variable);
            
            /* Delete when blackboard field view can be deleted */
            if (row != null && (row.BlackboardField.capabilities & Capabilities.Deletable) != 0)
            {
                row.RemoveFromHierarchy();
                SharedVariables.Remove(variable);
                if (fireEvents) NotifyVariableChanged(variable, VariableChangeType.Delete);
            }
        }

        private void NotifyVariableChanged(SharedVariable sharedVariable, VariableChangeType changeType)
        {
            using var changeEvent = VariableChangeEvent.GetPooled(sharedVariable, changeType);
            changeEvent.target = this;
            SendEvent(changeEvent);
        }

        protected virtual void BuildBlackboardMenu(ContextualMenuPopulateEvent evt, CeresBlackboardVariableRow variableRow)
        {
            evt.menu.MenuItems().Clear();
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Delete", _ =>
            {
                RemoveVariable(variableRow.Variable, true);
            }));
            evt.menu.MenuItems().Add(new CeresDropdownMenuAction("Duplicate", _ =>
           {
               AddVariable(variableRow.Variable.Clone(), true);
           }));
        }

        /// <summary>
        /// Create type settings for <see cref="SharedUObject"/>
        /// </summary>
        /// <param name="sharedUObject"></param>
        /// <param name="objectField"></param>
        /// <returns></returns>
        protected VisualElement CreateTypeSettingsView(SharedUObject sharedUObject, ObjectField objectField)
        {
            var placeHolder = new VisualElement();
            objectField.objectType = sharedUObject.serializedType;
            var constraintTypeName = "Object Type: " + objectField.objectType.Name;
            var button = new Button()
            {
                text = constraintTypeName
            };
            button.clicked += () =>
             {
                 var provider = ScriptableObject.CreateInstance<ObjectTypeSearchWindow>();
                 provider.Initialize(type =>
                 {
                     if (type == null)
                     {
                         sharedUObject.serializedType = SerializedType<UObject>.Default;
                         button.text = NullObjectType;
                         objectField.objectType = typeof(UObject);
                     }
                     else
                     {
                         objectField.objectType = type;
                         sharedUObject.serializedType = SerializedType<UObject>.FromType(type);
                         button.text = "Object Type: " + type.Name;
                     }

                     NotifyVariableChanged(sharedUObject, VariableChangeType.Type);
                 });
                 SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);
             };

            placeHolder.Add(button);
            return placeHolder;
        }

        protected static bool IsSupportSerializedType(Type type)
        {
            // Ensure SerializedObjectBase can serialize element
            if (type.IsGenericType) return false;
                        
            if(typeof(List<>).IsAssignableFrom(type) || type.IsArray)
            {
                return false;
            }

            // Can not serialize UObject reference directly
            if (typeof(UObject).IsAssignableFrom(type) || type == typeof(UObject))
            {
                return false;
            }

            if (type.Name == "<>c")
            {
                return false;
            }
                        
            if (typeof(Attribute).IsAssignableFrom(type))
            {
                return false;
            }
                        
            if (type.Assembly.GetName().Name.Contains("Editor"))
            {
                return false;
            }

            if (ReflectionUtility.IsSerializableNumericTypes(type))
            {
                return true;
            }
                        
            if (ReflectionUtility.IsUnityBuiltinTypes(type))
            {
                return true;
            }

            if (Attribute.IsDefined(type, typeof(SerializableAttribute), false))
            {
                return true;
            }
                        
            return false;
        }

        /// <summary>
        /// Create type settings for <see cref="SharedObject"/>
        /// </summary>
        /// <param name="sharedObject"></param>
        /// <returns></returns>
        protected VisualElement CreateTypeSettingsView(SharedObject sharedObject)
        {
            var placeHolder = new VisualElement();
            string constraintTypeName;
            Type objectType = null;
            try
            {
                objectType =  SerializedType.FromString(sharedObject.serializedObject.serializedTypeString);
                constraintTypeName = "Object Type: " + objectType.Name;
            }
            catch
            {
                constraintTypeName = NullObjectType;
            }
            
            var toggle = new Toggle("Is Array")
            {
                value = sharedObject.serializedObject.isArray
            };
            toggle.RegisterValueChangedCallback(evt =>
            {
                sharedObject.serializedObject.isArray = evt.newValue;
            });
            toggle.SetEnabled(objectType is not null);
            placeHolder.Add(toggle);
            
            var button = new Button()
            {
                text = constraintTypeName,
                tooltip = objectType?.FullName ?? string.Empty
            };
            button.clicked += () =>
            {
                var provider = ScriptableObject.CreateInstance<ObjectTypeSearchWindow>();
                provider.Initialize(type =>
                {
                    if (type == null)
                    {
                        sharedObject.serializedObject.serializedTypeString = string.Empty;
                        button.text = NullObjectType;
                        button.tooltip = string.Empty;
                        toggle.SetEnabled(false);
                    }
                    else
                    {
                        sharedObject.serializedObject.serializedTypeString = SerializedType.ToString(type);
                        button.text = "Object Type: " + type.Name;
                        button.tooltip = type.FullName;
                        toggle.SetEnabled(true);
                    }
                    NotifyVariableChanged(sharedObject, VariableChangeType.Type);
                }, typeof(object), types => types.Where(IsSupportSerializedType));
                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);
            };
            placeHolder.Add(button);
            return placeHolder;
        }
    }
}