using System;
using System.Reflection;
using Ceres.Graph.Flow.CustomFunctions;
using Ceres.Graph.Flow.Utilities;

namespace Ceres.Graph.Flow
{
    public static class ExecutableNodeReflectionHelper
    {
        public static Type PredictFunctionNodeType(MethodInfo methodInfo)
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
        
        public static Type PredictCustomFunctionNodeType(Type returnType, Type[] inputTypes)
        {
            int parametersLength = inputTypes.Length;
            if (returnType == typeof(void))
            {
                return parametersLength switch
                {
                    0 => typeof(FlowNode_ExecuteCustomFunctionTVoid),
                    1 => typeof(FlowNode_ExecuteCustomFunctionTVoid<>),
                    2 => typeof(FlowNode_ExecuteCustomFunctionTVoid<,>),
                    3 => typeof(FlowNode_ExecuteCustomFunctionTVoid<,,>),
                    4 => typeof(FlowNode_ExecuteCustomFunctionTVoid<,,,>),
                    5 => typeof(FlowNode_ExecuteCustomFunctionTVoid<,,,,>),
                    6 => typeof(FlowNode_ExecuteCustomFunctionTVoid<,,,,,>),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            return parametersLength switch
            {
                0 => typeof(FlowNode_ExecuteCustomFunctionTReturn<>),
                1 => typeof(FlowNode_ExecuteCustomFunctionTReturn<,>),
                2 => typeof(FlowNode_ExecuteCustomFunctionTReturn<,,>),
                3 => typeof(FlowNode_ExecuteCustomFunctionTReturn<,,,>),
                4 => typeof(FlowNode_ExecuteCustomFunctionTReturn<,,,,>),
                5 => typeof(FlowNode_ExecuteCustomFunctionTReturn<,,,,,>),
                6 => typeof(FlowNode_ExecuteCustomFunctionTReturn<,,,,,,>),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        public static Type PredictEventNodeType(int parametersLength)
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
        
        public static Type PredictEventNodeType(MethodInfo methodInfo)
        {
            int parametersLength = methodInfo.GetParameters().Length;
            return PredictEventNodeType(parametersLength);
        }
    }
}