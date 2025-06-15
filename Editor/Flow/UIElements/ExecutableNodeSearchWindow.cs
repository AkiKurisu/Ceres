using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Ceres.Annotations;
using Ceres.Graph;
using Ceres.Utilities;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Annotations;
using Ceres.Graph.Flow.Properties;
using Ceres.Graph.Flow.Utilities;
using UnityEngine.Pool;

namespace Ceres.Editor.Graph.Flow
{
    public class ExecutableNodeSearchWindow : CeresNodeSearchWindow
    {
        private readonly struct FunctionCandidate: IGrouped
        {
            public readonly Type TargetType;

            public readonly MethodInfo MethodInfo;
            
            public FunctionCandidate(Type targetType, MethodInfo methodInfo)
            {
                TargetType = methodInfo.IsStatic ? methodInfo.DeclaringType : targetType;
                MethodInfo = methodInfo;
                var atr = MethodInfo.GetCustomAttribute<CeresGroupAttribute>();
                atr ??= TargetType!.GetCustomAttribute<CeresGroupAttribute>();
                Group = atr?.Group;
                var function =  ExecutableReflection.GetFunction(methodInfo);
                Name = string.IsNullOrEmpty(function.Attribute.SearchName) ? ExecutableFunction.GetFunctionName(methodInfo, false) : function.Attribute.SearchName;
                Signature = CeresLabel.GetMethodSignature(methodInfo, false);
            }

            public string GetGroupNameOrDefault()
            {
                return SubClassSearchUtility.GetFirstGroupNameOrDefault(Group);
            }

            public string Group { get; }
            
            public string Name { get; }
            
            public string Signature { get; }
        }
        
        private readonly struct FunctionEntryNameCache : IDisposable
        {
            private readonly Dictionary<string, int> _nameCounts;
            
            private readonly Dictionary<FunctionCandidate, string> _nameCache;

            public FunctionEntryNameCache(List<FunctionCandidate> candidates)
            {
                _nameCounts = DictionaryPool<string, int>.Get();
                _nameCache = DictionaryPool<FunctionCandidate, string>.Get();
                
                foreach (var candidate in candidates)
                {
                    if (_nameCounts.TryGetValue(candidate.Name, out var count))
                        _nameCounts[candidate.Name] = count + 1;
                    else
                        _nameCounts[candidate.Name] = 1;
                }
                
                foreach (var candidate in candidates)
                {
                    var displayName = _nameCounts[candidate.Name] == 1 ? candidate.Name : candidate.Signature;
                    _nameCache[candidate] = displayName;
                }
            }

            public string GetName(FunctionCandidate candidate)
            {
                return _nameCache.TryGetValue(candidate, out var name) ? name : candidate.Signature;
            }

            public void Dispose()
            {
                DictionaryPool<string, int>.Release(_nameCounts);
                DictionaryPool<FunctionCandidate, string>.Release(_nameCache);
            }
        }
        
        private Texture2D _indentationIcon;

        private MethodInfo[] _containerImplementableEventMethodInfos;

        private Type[] _generatedExecutableEventTypes;
        
        protected override void OnInitialize()
        {
            Width = 400;
            _indentationIcon = new Texture2D(1, 1);
            _indentationIcon.SetPixel(0, 0, new Color(0, 0, 0, 0));
            _indentationIcon.Apply();
            _containerImplementableEventMethodInfos = GraphView.GetContainerType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(x=> x.GetCustomAttribute<ImplementableEventAttribute>() != null)
                .ToArray();
            var referencedAssemblies = SubClassSearchUtility.GetRuntimeReferencedAssemblies();
            _generatedExecutableEventTypes = SubClassSearchUtility.FindSubClassTypes(referencedAssemblies, typeof(CustomExecutionEvent)).ToArray();
        }

        private void OnDestroy()
        {
            DestroyImmediate(_indentationIcon);
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
                BuildFlowGraphFunctionEntries(builder);
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

            /* Build default execution event */
            var eventNodes = GraphView.NodeViews.OfType<ExecutionEventBaseNodeView>().ToList();
            builder.AddGroupEntry("Select Events", 1);
            builder.AddEntry(new SearchTreeEntry(new GUIContent($"New Execution Event", _indentationIcon))
            {
                level = 2, 
                userData = new CeresNodeSearchEntryData
                {
                    NodeType = typeof(ExecutionEvent)
                }
            });
            
                        
            /* Build implementable events */
            var methods = _containerImplementableEventMethodInfos
                .Where(x=> eventNodes.All(evt => evt.GetEventName() != x.Name))
                .ToArray();            
            if (methods.Any())
            {
                foreach (var method in methods)
                {
                    builder.AddEntry(new SearchTreeEntry(new GUIContent($"Implement {method.Name}", _indentationIcon))
                    {
                        level = 2,
                        userData = new CeresNodeSearchEntryData
                        {
                            NodeType = ExecutableNodeReflectionHelper.PredictEventNodeType(method),
                            Data = new ExecutableEventNodeViewFactoryProxy { MethodInfo = method }
                        }
                    });
                }
            }

            /* Build custom events */
            var validGeneratedEventTypes = _generatedExecutableEventTypes
                .Where(x => eventNodes.All(evt => evt.GetEventName() != CustomExecutionEvent.GetEventName(x)))
                .ToArray();
            if (!validGeneratedEventTypes.Any()) return;
            builder.AddGroupEntry("Implement Custom Events", 2);
            foreach (var eventType in validGeneratedEventTypes)
            {
                builder.AddEntry(new SearchTreeEntry(new GUIContent(
                    $"Implement {CustomExecutionEvent.GetEventName(eventType)}", _indentationIcon))
                {
                    level = 3,
                    userData = new CeresNodeSearchEntryData
                    {
                        NodeType = eventType
                    }
                });
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
                CeresLogger.LogWarning($"Event delegate does not support arguments out range of {maxParameters}");
                return;
            }
            builder.AddGroupEntry("Select Events", 1);
            builder.AddEntry(new SearchTreeEntry(new GUIContent($"New Execution Event", _indentationIcon))
            {
                level = 2, 
                userData = new CeresNodeSearchEntryData
                {
                    NodeType = ExecutableNodeReflectionHelper.PredictEventNodeType(parameterType.GetGenericArguments().Length),
                    Data = new DelegateEventNodeViewFactoryProxy { DelegateType = parameterType }
                }
            });
        }

        private static readonly Type[] SupportedDelegateTypes = {
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

            /* Get inner delegate type */
            if (parameterType.IsGenericType) parameterType = parameterType.GetGenericTypeDefinition();
            return SupportedDelegateTypes.Contains(parameterType);
        }

        private void BuildPropertyEntries(CeresNodeSearchEntryBuilder builder, Type targetType, bool isSelfTarget)
        { 
            /* Build properties */
            var properties = targetType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(x=> x.CanRead || x.CanWrite)
                .ToArray();
            
            /* Self reference and graph variables */
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

            /* Getter and Setter of scripting properties */
            foreach (var property in properties)
            {
                var getMethod = property.GetGetMethod();
                if(getMethod?.IsPublic ?? false)
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
                                IsSelfTarget = isSelfTarget,
                                IsStatic = getMethod.IsStatic
                            }
                        }
                    });
                }

                var setMethod = property.GetSetMethod();
                if (setMethod?.IsPublic ?? false)
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
                                IsSelfTarget = isSelfTarget,
                                IsStatic = setMethod.IsStatic
                            }
                        }
                    });
                }
            }
            
            /* Always included properties */
            if (isSelfTarget)
            {
                foreach (var tuple in ExecutableFunctionRegistry.Get().GetAlwaysIncludedProperties())
                {
                    var type = tuple.type;
                    builder.AddGroupEntry($"Select {type.Name}", 2);
                    foreach (var property in tuple.propertyInfos)
                    {
                        var getMethod = property.GetGetMethod();
                        if (getMethod?.IsPublic ?? false)
                        {
                            builder.AddEntry(
                                new SearchTreeEntry(new GUIContent(
                                    $"Get {property.Name}", _indentationIcon))
                                {
                                    level = 3,
                                    userData = new CeresNodeSearchEntryData
                                    {
                                        NodeType = typeof(PropertyNode_GetPropertyTValue<,>),
                                        Data = new PropertyNodeViewFactoryProxy
                                        {
                                            PropertyName = property.Name,
                                            PropertyInfo = property,
                                            IsSelfTarget = false,
                                            IsStatic = true
                                        }
                                    }
                                });
                        }

                        var setMethod = property.GetSetMethod();
                        if (setMethod?.IsPublic ?? false)
                        {
                            builder.AddEntry(
                                new SearchTreeEntry(new GUIContent(
                                    $"Set {property.Name}", _indentationIcon))
                                {
                                    level = 3,
                                    userData = new CeresNodeSearchEntryData
                                    {
                                        NodeType = typeof(PropertyNode_SetPropertyTValue<,>),
                                        Data = new PropertyNodeViewFactoryProxy
                                        {
                                            PropertyName = property.Name,
                                            PropertyInfo = property,
                                            IsSelfTarget = false,
                                            IsStatic = true
                                        }
                                    }
                                });
                        }
                    }
                }
            }
        }
        
        private void BuildExecutableFunctionEntries(CeresNodeSearchEntryBuilder builder, 
            Type targetType, bool includeStaticAndNonPublic)
        {
            var graphContainerType = GraphView.GetContainerType();
            var types = CeresPort.GetCompatibleTypes(targetType).Concat(new[] { targetType });

            var methodCandidates = types
                .SelectMany(type => ExecutableFunctionRegistry.Get().GetFunctions(type)
                    .Select(x=> new FunctionCandidate(targetType, x)))
                .Where(x=>
                {
                    if (x.MethodInfo.IsPublic || includeStaticAndNonPublic) return true;
                    /* Visibility is aligned with method access modifier */
                    if (x.MethodInfo.IsPrivate && graphContainerType == targetType) return true;
                    return x.MethodInfo.IsFamily && graphContainerType.IsAssignableTo(targetType);
                })
                .ToArray();
            
            if (includeStaticAndNonPublic)
            {
                var staticFunctions = ExecutableFunctionRegistry.Get().GetStaticFunctions()
                                                                .Select(x => new FunctionCandidate(targetType, x));
                methodCandidates = methodCandidates.Concat(staticFunctions).ToArray();
            }
            
            if (methodCandidates.Any())
            {
                builder.AddGroupEntry("Execute Functions", 1);
                var groupedMethodCandidates = methodCandidates
                    .GroupBy(candidate => candidate.GetGroupNameOrDefault())
                    .Where(grouping => !string.IsNullOrEmpty(grouping.Key))
                    .ToArray();
                
                var rawMethodCandidates = methodCandidates.Except(groupedMethodCandidates
                        .SelectMany(grouping => grouping))
                    .ToList();
                
                foreach (var candidateGrouping in groupedMethodCandidates)
                {
                    AddAllFunctionEntries(candidateGrouping, 2);
                }

                using var cache = new FunctionEntryNameCache(rawMethodCandidates);
                foreach (var functionCandidate in rawMethodCandidates)
                {
                    AddFunctionEntry(functionCandidate, 2, cache.GetName(functionCandidate));
                }
            }
            return;

            void AddAllFunctionEntries(IGrouping<string, FunctionCandidate> group, int level, int subCount = 1)
            {
                var groupName = group.Key;
                builder.AddEntry(new SearchTreeGroupEntry(new GUIContent($"Select {groupName}"), level));
                var subGroups = group.SubGroup(subCount).ToArray();
                var left = group.Except(subGroups.SelectMany(grouping => grouping)).ToList();
                foreach (var subGroup in subGroups)
                {
                    AddAllFunctionEntries(subGroup,level + 1, subCount + 1);
                }
                using var cache = new FunctionEntryNameCache(left);
                foreach (var functionCandidate in left)
                {
                    AddFunctionEntry(functionCandidate, level + 1, cache.GetName(functionCandidate));
                }
            }
            
            void AddFunctionEntry(FunctionCandidate candidate, int level, string displayName)
            {
                builder.AddEntry(new SearchTreeEntry(new GUIContent(displayName, _indentationIcon))
                {
                    level = level,
                    userData = new CeresNodeSearchEntryData
                    {
                        NodeType = ExecutableNodeReflectionHelper.PredictFunctionNodeType(candidate.MethodInfo),
                        SubType = candidate.TargetType,
                        Data = new ExecuteFunctionNodeViewFactoryProxy { MethodInfo = candidate.MethodInfo }
                    }
                });
            }
        }

        private void BuildFlowGraphFunctionEntries(CeresNodeSearchEntryBuilder builder)
        {
            var flowGraphFunctions = FlowGraphFunctionRegistry.Get().GetFlowGraphFunctions(GraphView.GetContainerType());
            if (!flowGraphFunctions.Any()) return;
            
            builder.AddGroupEntry("Execute Functions", 1);
            foreach (var flowGraphFunction in flowGraphFunctions)
            {
                AddFlowGraphFunctionEntry(flowGraphFunction,2);
            }
            return;
            
            void AddFlowGraphFunctionEntry(FlowGraphFunction flowGraphFunction, int level)
            {
                var functionName = flowGraphFunction.Asset.name;
                builder.AddEntry(new SearchTreeEntry(new GUIContent(functionName, _indentationIcon))
                {
                    level = level,
                    userData = new CeresNodeSearchEntryData
                    {
                        NodeType = ExecutableNodeReflectionHelper.PredictCustomFunctionNodeType(flowGraphFunction.ReturnType, flowGraphFunction.InputTypes),
                        Data = new ExecuteFlowGraphFunctionNodeViewFactoryProxy { Function = flowGraphFunction }
                    }
                });
            }
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
