using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Graph;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Annotations;
using Ceres.Graph.Flow.Properties;
using Ceres.Graph.Flow.Utilities;
namespace Ceres.Editor.Graph.Flow
{
    public class ExecutableNodeSearchWindow : CeresNodeSearchWindow
    {
        private Texture2D _indentationIcon;
        
        protected override void OnInitialize()
        {
            _indentationIcon = new Texture2D(1, 1);
            _indentationIcon.SetPixel(0, 0, new Color(0, 0, 0, 0));
            _indentationIcon.Apply();
        }

        public override List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Node"))
            };

            var searchContext = Context;
            if (!searchContext.HasFunctionTargetType())
            {
                searchContext.ParameterType = null;
            }
            
            var (groups, nodeTypes) = SearchTypes(new[] { typeof(ExecutableNode) }, searchContext);
            var builder = new CeresNodeSearchEntryBuilder(_indentationIcon, searchContext.AllowGeneric, searchContext.ParameterType);
            
            if (searchContext.ParameterType == null)
            {
                BuildBasicEntries(builder);
                BuildExecutableFunctionEntries(builder, GraphView.EditorWindow.Container.GetType(), true);
            }
            else
            {
                BuildExecutableFunctionEntries(builder, searchContext.ParameterType, false);
                BuildDelegateEntries(builder, searchContext.ParameterType);
            }
            
            foreach (var subGroup in groups)
            {
                builder.AddAllEntries(subGroup, 1);
            }
            
            foreach (var type in nodeTypes)
            {
                builder.AddEntry(type, 1);
            }
            
            entries.AddRange(builder.GetEntries());
            return entries;
        }

        private void BuildBasicEntries(CeresNodeSearchEntryBuilder builder)
        {
            /* Build properties */
            var containerType = GraphView.GetContainerType();
            var variables = GraphView.Blackboard.SharedVariables;
            var properties = containerType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x=> x.CanRead && x.CanWrite)
                .ToArray();
            
            builder.AddGroupEntry("Select Properties", 1);
            builder.AddEntry(new SearchTreeEntry(new GUIContent("Get Self Reference", _indentationIcon))
            {
                level = 2,
                userData = new CeresNodeSearchEntryData
                {
                    NodeType = typeof(PropertyNode_GetSelfTReference<>),
                    Data = new PropertyNodeViewFactoryProxy
                    {
                        PropertyName = "Self"
                    }
                }
            });
            
            if(variables.Any() || properties.Any())
            {
                foreach (var variable in variables)
                {
                    builder.AddEntry(new SearchTreeEntry(new GUIContent($"Get {variable.Name}", _indentationIcon))
                    {
                        level = 2,
                        userData = new CeresNodeSearchEntryData
                        {
                            NodeType = typeof(PropertyNode_GetSharedVariableTValue<,,>),
                            Data = new PropertyNodeViewFactoryProxy
                            {
                                PropertyName = variable.Name, 
                                SharedVariable = variable
                            }
                        }
                    });
                    builder.AddEntry(new SearchTreeEntry(new GUIContent($"Set {variable.Name}", _indentationIcon))
                    {
                        level = 2,
                        userData = new CeresNodeSearchEntryData
                        {
                            NodeType = typeof(PropertyNode_SetSharedVariableTValue<,,>),
                            Data = new PropertyNodeViewFactoryProxy
                            {
                                PropertyName = variable.Name, 
                                SharedVariable = variable
                            }
                        }
                    });
                }
                foreach (var property in properties)
                {
                    if(property.GetGetMethod()?.IsPublic ?? false)
                    {
                        builder.AddEntry(new SearchTreeEntry(new GUIContent($"Get {property.Name}", _indentationIcon))
                        {
                            level = 2,
                            userData = new CeresNodeSearchEntryData
                            {
                                NodeType = typeof(PropertyNode_GetPropertyTValue<,>),
                                Data = new PropertyNodeViewFactoryProxy
                                {
                                    PropertyName = property.Name,
                                    PropertyInfo = property
                                }
                            }
                        });
                    }

                    if (property.GetSetMethod()?.IsPublic ?? false)
                    {
                        builder.AddEntry(new SearchTreeEntry(new GUIContent($"Set {property.Name}", _indentationIcon))
                        {
                            level = 2,
                            userData = new CeresNodeSearchEntryData
                            {
                                NodeType = typeof(PropertyNode_SetPropertyTValue<,>),
                                Data = new PropertyNodeViewFactoryProxy
                                {
                                    PropertyName = property.Name,
                                    PropertyInfo = property
                                }
                            }
                        });
                    }
                }
            }
                
            var events = GraphView.NodeViews.OfType<ExecutionEventNodeView>()
                .Where(x=>!x.NodeType.IsGenericType)
                .ToArray();

            /* Build execute events */
            if (events.Any())
            {
                builder.AddGroupEntry("Execute Events", 1);
               
                foreach (var eventView in events)
                {
                    builder.AddEntry(new SearchTreeEntry(new GUIContent($"Execute {eventView.GetEventName()}", _indentationIcon))
                    {
                        level = 2, 
                        userData = new CeresNodeSearchEntryData
                        {
                            NodeType = typeof(FlowNode_ExecuteEvent),
                            Data = new ExecuteEventNodeViewFactoryProxy { EventName = eventView.GetEventName() }
                        }
                    });
                }
            }

            /* Build custom events */
            var eventNodes = GraphView.NodeViews.OfType<ExecutableEventNodeView>().ToList();
            var container = ((FlowGraphEditorWindow)GraphView.EditorWindow).ContainerT;
            var methods = container.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(x=> x.GetCustomAttribute<ImplementableEventAttribute>() != null 
                       && eventNodes.All(evt => evt.GetEventName() != x.Name))
                .ToArray();
            builder.AddGroupEntry("Select Events", 1);
            builder.AddEntry(new SearchTreeEntry(new GUIContent($"New Execution Event", _indentationIcon))
            {
                level = 2, 
                userData = new CeresNodeSearchEntryData
                {
                    NodeType = typeof(ExecutionEvent)
                }
            });
            if(methods.Any())
            {
                builder.AddGroupEntry("Implement Custom Events", 2);
                foreach (var method in methods)
                {
                    builder.AddEntry(new SearchTreeEntry(new GUIContent($"Implement {method.Name}", _indentationIcon))
                    {
                        level = 3, 
                        userData = new CeresNodeSearchEntryData
                        {
                            NodeType = PredictEventNodeType(method),
                            Data = new ExecutableEventNodeViewFactoryProxy { MethodInfo = method }
                        }
                    });
                }
            }
        }
        
        private void BuildDelegateEntries(CeresNodeSearchEntryBuilder builder,  Type parameterType)
        {
            /* Build delegate events */
            if (!parameterType.IsAssignableTo(typeof(EventDelegateBase)))
            {
                return;
            }
            int parametersLength = parameterType.GetGenericArguments().Length;
            if (parametersLength == 0 || parametersLength > 4)
            {
                /* Only support generic version */
                return;
            }
            builder.AddGroupEntry("Select Events", 1);
            builder.AddEntry(new SearchTreeEntry(new GUIContent($"New Execution Event", _indentationIcon))
            {
                level = 2, 
                userData = new CeresNodeSearchEntryData
                {
                    NodeType = PredictEventNodeType(parameterType.GetGenericArguments().Length),
                    Data = new DelegateEventNodeViewFactoryProxy { DelegateType = parameterType }
                }
            });
        }

        private void BuildExecutableFunctionEntries(CeresNodeSearchEntryBuilder builder, Type targetType, bool includeStatic)
        {
            var methods = ExecutableFunctionRegistry.Get().GetFunctions(targetType);
            if (includeStatic)
            {
                methods = methods.Concat(ExecutableFunctionRegistry.Get().GetStaticFunctions()).ToArray();
            }
            if(methods.Any())
            {
                builder.AddGroupEntry("Execute Functions", 1);
                foreach (var method in methods)
                {
                    var functionName = ExecutableFunctionRegistry.GetFunctionName(method, false);
                    builder.AddEntry(new SearchTreeEntry(new GUIContent($"{functionName}", _indentationIcon))
                    {
                        level = 2, 
                        userData = new CeresNodeSearchEntryData
                        {
                            NodeType = PredictFunctionNodeType(method),
                            SubType = targetType,
                            Data = new ExecuteFunctionNodeViewFactoryProxy { MethodInfo = method }
                        }
                    });
                }
            }
        }

        private static Type PredictFunctionNodeType(MethodInfo methodInfo)
        {
            int parametersLength = methodInfo.GetParameters().Length;
            if (methodInfo.ReturnType == typeof(void))
            {
                return parametersLength switch
                {
                    0 => typeof(FlowNode_ExecuteFunctionTVoid<>),
                    1 => typeof(FlowNode_ExecuteFunctionTVoid<,>),
                    2 => typeof(FlowNode_ExecuteFunctionTVoid<,,>),
                    3 => typeof(FlowNode_ExecuteFunctionTVoid<,,,>),
                    4 => typeof(FlowNode_ExecuteFunctionTVoid<,,,,>),
                    _ => typeof(FlowNode_ExecuteFunctionT<>)
                };
            }

            return parametersLength switch
            {
                0 => typeof(FlowNode_ExecuteFunctionTReturn<,>),
                1 => typeof(FlowNode_ExecuteFunctionTReturn<,,>),
                2 => typeof(FlowNode_ExecuteFunctionTReturn<,,,>),
                3 => typeof(FlowNode_ExecuteFunctionTReturn<,,,,>),
                4 => typeof(FlowNode_ExecuteFunctionTReturn<,,,,,>),
                _ => typeof(FlowNode_ExecuteFunctionT<>)
            };
        }
        
        private static Type PredictEventNodeType(int parametersLength)
        {
            return parametersLength switch
            {
                0 => typeof(ExecutionEvent),
                1 => typeof(ExecutionEvent<>),
                2 => typeof(ExecutionEvent<,>),
                3 => typeof(ExecutionEvent<,,>),
                4 => typeof(ExecutionEvent<,,,>),
                _ => typeof(ExecutionEventUber)
            };
        }
        
        private static Type PredictEventNodeType(MethodInfo methodInfo)
        {
            int parametersLength = methodInfo.GetParameters().Length;
            return PredictEventNodeType(parametersLength);
        }

        protected override bool OnSelectEntry(CeresNodeSearchEntryData entryData, Rect rect)
        {
            if (entryData.Data is IExecutableNodeViewFactoryProxy factoryProxy)
            {
                var nodeView = factoryProxy.Create(this, entryData, rect);
                ConnectRequestPort(nodeView);
                return true;
            }
            return base.OnSelectEntry(entryData, rect);
        }
    }

    public static class ExecutableNodeSearchExtensions
    {
        public static bool HasFunctionTargetType(this NodeSearchContext searchContext)
        {
            return searchContext.ParameterType != null && searchContext.ParameterType != typeof(NodeReference);
        }
    }
}
