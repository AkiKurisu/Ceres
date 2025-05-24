using System;
using System.Linq;
using System.Reflection;
using Ceres.Editor.Graph.Flow.CustomFunctions;
using UnityEngine;
using Chris;
using Ceres.Utilities;
using Ceres.Graph;
using Ceres.Graph.Flow.Properties;
using Ceres.Graph.Flow.Utilities;
using Ceres.Editor.Graph.Flow.Properties;
using Ceres.Editor.Graph.Flow.Utilities;
using Ceres.Graph.Flow.CustomFunctions;

namespace Ceres.Editor.Graph.Flow
{
    public interface IExecutableNodeViewFactoryProxy
    {
        ExecutableNodeView Create(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData, Rect rect);
    }

    public class PropertyNodeViewFactoryProxy : IExecutableNodeViewFactoryProxy
    {
        public string PropertyName;

        public SharedVariable SharedVariable;

        public PropertyInfo PropertyInfo;

        public bool IsSelfTarget;

        public bool IsStatic;
        
        public ExecutableNodeView Create(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData, Rect rect)
        {
            PropertyNodeView propertyNodeView;
            if (entryData.NodeType.IsSubclassOf(typeof(PropertyNode_PropertyValue)))
            {
                var parameters = new[] { PropertyInfo.DeclaringType, PropertyInfo.PropertyType };
                var nodeView = (PropertyNode_PropertyValueNodeView)NodeViewFactory.Get().CreateInstanceResolved(entryData.NodeType, searchWindow.GraphView, parameters);
                nodeView.SetPropertyFlags(IsSelfTarget, IsStatic);
                propertyNodeView = nodeView;
            }
            else if (entryData.NodeType.IsSubclassOf(typeof(PropertyNode_SharedVariableValue)))
            {
                var parameters = new[]
                {
                    SharedVariable.GetType(),  /* Variable type */
                    ReflectionUtility.GetGenericArgumentType(SharedVariable.GetType()), /* Contained value type */
                    SharedVariable.GetValueType() /* Display value type */
                };
                propertyNodeView = (PropertyNodeView)NodeViewFactory.Get().CreateInstanceResolved(entryData.NodeType, searchWindow.GraphView, parameters);
            }
            else if (entryData.NodeType == typeof(PropertyNode_GetSelfTReference<>))
            {
                var parameters = new[]
                {
                    searchWindow.GraphView.GetContainerType()
                };
                propertyNodeView = (PropertyNodeView)NodeViewFactory.Get().CreateInstanceResolved(entryData.NodeType, searchWindow.GraphView, parameters);
            }
            else
            {
                throw new ArgumentException($"[Ceres] Can not create property node view for {entryData.NodeType}");
            }
            searchWindow.GraphView.AddNodeView(propertyNodeView, rect);
            propertyNodeView!.SetPropertyName(PropertyName);
            return propertyNodeView;
        }
    }

    public class ExecuteEventNodeViewFactoryProxy : IExecutableNodeViewFactoryProxy
    {
        public string EventName;

        public ExecutableNodeView Create(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData, Rect rect)
        {
            var propertyNodeView = (FlowNode_ExecuteEventNodeView)searchWindow.CreateNodeView(entryData);
            searchWindow.GraphView.AddNodeView(propertyNodeView, rect);
            propertyNodeView!.SetEventName(EventName);
            return propertyNodeView;
        }
    }

    public class ExecutableEventNodeViewFactoryProxy : IExecutableNodeViewFactoryProxy
    {
        public MethodInfo MethodInfo;

        public ExecutableNodeView Create(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData, Rect rect)
        {
            var nodeView = CreateGenericNodeView(searchWindow, entryData);
            searchWindow.GraphView.AddNodeView(nodeView, rect);
            nodeView!.SetMethodInfo(MethodInfo);
            return nodeView;
        }
        
        private ExecutionEventBaseNodeView CreateGenericNodeView(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData)
        {
            /* Fill parameter types */
            var parameters = MethodInfo.GetParameters().Select(x => x.ParameterType).ToList();
            return (ExecutionEventBaseNodeView)NodeViewFactory.Get().CreateInstanceResolved(entryData.NodeType, searchWindow.GraphView, parameters.ToArray());
        }
    }
    
    public class DelegateEventNodeViewFactoryProxy : IExecutableNodeViewFactoryProxy
    {
        public Type DelegateType;
        
        public ExecutableNodeView Create(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData, Rect rect)
        {
            var parameters = DelegateType.GetGenericArguments();
            var nodeView = (ExecutionEventBaseNodeView)NodeViewFactory.Get().CreateInstanceResolved(entryData.NodeType, searchWindow.GraphView, parameters);
            searchWindow.GraphView.AddNodeView(nodeView, rect);
            return nodeView;
        }
    }

    public class ExecuteFunctionNodeViewFactoryProxy : IExecutableNodeViewFactoryProxy
    {
        public MethodInfo MethodInfo;

        public ExecutableNodeView Create(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData, Rect rect)
        {
            ExecuteFunctionNodeView nodeView = null;
            if (entryData.NodeType.IsAssignableTo(typeof(FlowNode_ExecuteFunctionUber)))
            {
                nodeView = CreateUberNodeView(searchWindow, entryData);
            }
            else if (entryData.NodeType.IsAssignableTo(typeof(FlowNode_ExecuteFunctionVoid)))
            {
                nodeView = CreateVoidNodeView(searchWindow, entryData);
            }
            else if (entryData.NodeType.IsAssignableTo(typeof(FlowNode_ExecuteFunctionReturn)))
            {
                nodeView = CreateReturnNodeView(searchWindow, entryData);
            }
            
            searchWindow.GraphView.AddNodeView(nodeView, rect);
            nodeView!.SetMethodInfo(MethodInfo);
            return nodeView;
        }
        
        private FlowNode_ExecuteFunctionUberNodeView CreateUberNodeView(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData)
        {
            /* Fill target type */
            var parameters = new[] { GetTargetType(entryData) };
            return (FlowNode_ExecuteFunctionUberNodeView)NodeViewFactory.Get().CreateInstanceResolved(entryData.NodeType, searchWindow.GraphView, parameters.ToArray());
        }
        
        private FlowNode_ExecuteFunctionVoidNodeView CreateVoidNodeView(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData)
        {
            /* Fill parameter types */
            var parameters = MethodInfo.GetParameters().Select(x => x.ParameterType).ToList();
            parameters.Insert(0, GetTargetType(entryData));
            return (FlowNode_ExecuteFunctionVoidNodeView)NodeViewFactory.Get().CreateInstanceResolved(entryData.NodeType, searchWindow.GraphView, parameters.ToArray());
        }
        
        private FlowNode_ExecuteFunctionReturnNodeView CreateReturnNodeView(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData)
        {
            /* Fill parameter types */
            var parameters = MethodInfo.GetParameters().Select(x => x.ParameterType).ToList();
            parameters.Insert(0, GetTargetType(entryData));
            /* Fill return type */
            parameters.Add(MethodInfo.ReturnType);
            return (FlowNode_ExecuteFunctionReturnNodeView)NodeViewFactory.Get().CreateInstanceResolved(entryData.NodeType, searchWindow.GraphView, parameters.ToArray());
        }

        private Type GetTargetType(CeresNodeSearchEntryData entryData)
        {
            return MethodInfo.IsStatic ? MethodInfo.DeclaringType : entryData.SubType;
        }
    }

    public class ExecuteFlowGraphFunctionNodeViewFactoryProxy : IExecutableNodeViewFactoryProxy
    {
        public FlowGraphFunction Function;
        
        public ExecutableNodeView Create(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData, Rect rect)
        {
           ExecuteCustomFunctionNodeView nodeView = null;
            if (entryData.NodeType.IsAssignableTo(typeof(FlowNode_ExecuteCustomFunctionVoid)))
            {
                nodeView = CreateVoidNodeView(searchWindow, entryData);
            }
            else if (entryData.NodeType.IsAssignableTo(typeof(FlowNode_ExecuteCustomFunctionReturn)))
            {
                nodeView = CreateReturnNodeView(searchWindow, entryData);
            }
            
            searchWindow.GraphView.AddNodeView(nodeView, rect);
            nodeView!.SetFlowGraphFunction(Function);
            return nodeView;
        }
        
        private ExecuteCustomFunctionNodeView CreateVoidNodeView(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData)
        {
            return (ExecuteCustomFunctionNodeView)NodeViewFactory.Get().CreateInstanceResolved(entryData.NodeType, searchWindow.GraphView, Function.InputTypes);
        }
        
        private ExecuteCustomFunctionNodeView CreateReturnNodeView(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData)
        {
            /* Fill parameter types */
            var parameters = Function.InputTypes.ToList();
            /* Fill return type */
            parameters.Add(Function.ReturnType);
            return (ExecuteCustomFunctionNodeView)NodeViewFactory.Get().CreateInstanceResolved(entryData.NodeType, searchWindow.GraphView, parameters.ToArray());
        }
    }
}