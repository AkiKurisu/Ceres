using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Properties;
using Ceres.Graph.Flow.Utilities;
using Chris;
namespace Ceres.Editor.Graph.Flow
{
    public interface IExecutableNodeViewFactoryProxy
    {
        void Create(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData, Rect rect);
    }

    public class PropertyNodeViewFactoryProxy : IExecutableNodeViewFactoryProxy
    {
        public string PropertyName;

        public SharedVariable SharedVariable;

        public PropertyInfo PropertyInfo;
        
        public void Create(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData, Rect rect)
        {
            PropertyNodeView propertyNodeView;
            if (entryData.NodeType == typeof(PropertyNode_GetPropertyTValue<,>) || entryData.NodeType == typeof(PropertyNode_SetPropertyTValue<,>))
            {
                var parameters = new[] { PropertyInfo.DeclaringType, PropertyInfo.PropertyType };
                propertyNodeView = (PropertyNodeView)NodeViewFactory.Get().CreateInstanceResolved(entryData.NodeType, searchWindow.GraphView, parameters);
            }
            else if (entryData.NodeType == typeof(PropertyNode_GetSharedVariableTValue<,,>) ||
                     entryData.NodeType == typeof(PropertyNode_SetSharedVariableTValue<,,>))
            {
                var parameters = new[]
                {
                    SharedVariable.GetType(),  /* Variable type */
                    ReflectionUtility.GetGenericArgumentType(SharedVariable.GetType()), /* Contained value type */
                    SharedVariable.GetValueType() /* Display value type */
                };
                propertyNodeView = (PropertyNodeView)NodeViewFactory.Get().CreateInstanceResolved(entryData.NodeType, searchWindow.GraphView, parameters);
            }
            else
            {
                throw new ArgumentException($"[Ceres] Can not create property node view for {entryData.NodeType}");
            }
            searchWindow.GraphView.AddNodeView(propertyNodeView, rect);
            propertyNodeView!.SetPropertyName(PropertyName);
        }
    }

    public class ExecuteEventNodeViewFactoryProxy : IExecutableNodeViewFactoryProxy
    {
        public string EventName;

        public void Create(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData, Rect rect)
        {
            var propertyNodeView = (FlowNode_ExecuteEventNodeView)searchWindow.CreateNodeView(entryData);
            searchWindow.GraphView.AddNodeView(propertyNodeView, rect);
            propertyNodeView!.SetEventName(EventName);
        }
    }

    public class ExecutableEventNodeViewFactoryProxy : IExecutableNodeViewFactoryProxy
    {
        public MethodInfo MethodInfo;

        public void Create(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData, Rect rect)
        {
            ExecutableEventNodeView nodeView;
            if (entryData.NodeType.IsAssignableTo(typeof(ExecutionEventUber)))
            {
                nodeView = (ExecutionEventUberNodeView)searchWindow.CreateNodeView(entryData);
            }
            else
            {
                nodeView = CreateGenericNodeView(searchWindow, entryData);
            }
            searchWindow.GraphView.AddNodeView(nodeView, rect);
            nodeView!.SetMethodInfo(MethodInfo);
        }
        
        private ExecutableEventNodeView CreateGenericNodeView(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData)
        {
            /* Fill parameter types */
            var parameters = MethodInfo.GetParameters().Select(x => x.ParameterType).ToList();
            return (ExecutableEventNodeView)NodeViewFactory.Get().CreateInstanceResolved(entryData.NodeType, searchWindow.GraphView, parameters.ToArray());
        }
    }

    public class ExecuteFunctionNodeViewFactoryProxy : IExecutableNodeViewFactoryProxy
    {
        public MethodInfo MethodInfo;

        public void Create(ExecutableNodeSearchWindow searchWindow, CeresNodeSearchEntryData entryData, Rect rect)
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
            if (nodeView == null) return;
            
            searchWindow.GraphView.AddNodeView(nodeView, rect);
            nodeView!.SetMethodInfo(MethodInfo);
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
}