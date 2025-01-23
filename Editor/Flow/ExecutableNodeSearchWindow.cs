using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Graph;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Ceres.Utilities;
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
                BuildPropertyEntries(builder, GraphView.GetContainerType(), true);
                BuildExecutableFunctionEntries(builder, GraphView.GetContainerType(), true);
            }
            else
            {
                BuildPropertyEntries(builder, searchContext.ParameterType, false);
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
        
        private void BuildDelegateEntries(CeresNodeSearchEntryBuilder builder, Type parameterType)
        {
            /* Build delegate events */
            if (!IsDelegatePort(parameterType)) return;
            int parametersLength = parameterType.GetGenericArguments().Length;
            const int maxParameters = 6;
            if (parametersLength > maxParameters)
            {
                /* Not support uber version */
                CeresAPI.LogWarning($"Event delegate not support arguments out range of {maxParameters}");
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

        private static readonly Type[] DelegateTypes = {
            typeof(Action),
            typeof(Action<>),
            typeof(Action<,>),
            typeof(Action<,,>),
            typeof(Action<,,,>),
            typeof(Action<,,,,>),
            typeof(Action<,,,,,>)
        };
        
        private static bool IsDelegatePort(Type parameterType)
        {
            if (parameterType.IsAssignableTo(typeof(EventDelegateBase)))
            {
                return true;
            }

            if (parameterType.IsGenericType) parameterType = parameterType.GetGenericTypeDefinition();
            return DelegateTypes.Contains(parameterType);
        }

        private void BuildPropertyEntries(CeresNodeSearchEntryBuilder builder, Type targetType, bool isSelfTarget)
        { 
            /* Build properties */
            var properties = targetType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x=> x.CanRead || x.CanWrite)
                .ToArray();
            
            if (isSelfTarget)
            {
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

                var variables = GraphView.Blackboard.SharedVariables;
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
            }
            else
            {
                if (properties.Any())
                {
                    builder.AddGroupEntry("Select Properties", 1);
                }
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
                                PropertyInfo = property,
                                IsSelfTarget = isSelfTarget
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
                                PropertyInfo = property,
                                IsSelfTarget = isSelfTarget
                            }
                        }
                    });
                }
            }
        }
        
        private readonly struct FunctionCandidate
        {
            public readonly Type TargetType;

            public readonly MethodInfo MethodInfo;
            
            public bool IsScriptMethod { get; }

            public FunctionCandidate(Type targetType, MethodInfo methodInfo)
            {
                TargetType = methodInfo.IsStatic ? methodInfo.DeclaringType : targetType;
                MethodInfo = methodInfo;
                IsScriptMethod = methodInfo.IsStatic && ExecutableFunction.IsScriptMethod(methodInfo);
            }
        }

        private void BuildExecutableFunctionEntries(CeresNodeSearchEntryBuilder builder, Type targetType, bool includeStatic)
        {
            var types = CeresPort.GetCompatibleTypes(targetType).Concat(new[] { targetType });

            var methods = types
                .SelectMany(type => ExecutableFunctionRegistry.Get().GetFunctions(type)
                    .Select(x=> new FunctionCandidate(targetType, x)))
                .ToArray();
            
            if (includeStatic)
            {
                var staticFunctions = ExecutableFunctionRegistry.Get().GetStaticFunctions()
                                                                .Select(x => new FunctionCandidate(targetType, x));
                methods = methods.Concat(staticFunctions).ToArray();
            }
            
            if(methods.Any())
            {
                builder.AddGroupEntry("Execute Functions", 1);
                var groupedMethods = methods
                .Where(x=> x.IsScriptMethod)
                .GroupBy(methodInfo =>
                {
                    var libraryType = methodInfo.TargetType;
                    return SubClassSearchUtility.GetFirstGroupNameOrDefault(libraryType);
                })
                .Where(x => !string.IsNullOrEmpty(x.Key))
                .ToArray();
                
                var rawMethods = methods.Except(groupedMethods
                        .SelectMany(x => x))
                    .ToList();
                
                foreach (var methodsInGroup in groupedMethods)
                {
                    var groupName = methodsInGroup.Key;
                    builder.AddEntry(new SearchTreeGroupEntry(new GUIContent($"Select {groupName}"), 2));
                    foreach (var method in methodsInGroup)
                    {
                        AddFunctionEntry(method, 3);
                    }
                }
                foreach (var method in rawMethods)
                {
                    AddFunctionEntry(method, 2);
                }
            }

            void AddFunctionEntry(FunctionCandidate candidate, int level)
            {
                var functionName = ExecutableFunction.GetFunctionName(candidate.MethodInfo, false);
                builder.AddEntry(new SearchTreeEntry(new GUIContent($"{functionName}", _indentationIcon))
                {
                    level = level,
                    userData = new CeresNodeSearchEntryData
                    {
                        NodeType = PredictFunctionNodeType(candidate.MethodInfo),
                        SubType = candidate.TargetType,
                        Data = new ExecuteFunctionNodeViewFactoryProxy { MethodInfo = candidate.MethodInfo }
                    }
                });
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
                    5 => typeof(FlowNode_ExecuteFunctionTVoid<,,,,,>),
                    6 => typeof(FlowNode_ExecuteFunctionTVoid<,,,,,,>),
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
                5 => typeof(FlowNode_ExecuteFunctionTReturn<,,,,,,>),
                6 => typeof(FlowNode_ExecuteFunctionTReturn<,,,,,,,>),
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
                5 => typeof(ExecutionEvent<,,,,>),
                6 => typeof(ExecutionEvent<,,,,,>),
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
