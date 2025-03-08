using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Graph.Flow;
using UnityEditor;

namespace Ceres.Editor.Graph.Flow
{
    public class FlowGraphFunction
    {
        public readonly Type ContainerType;
        
        public readonly FlowGraphFunctionAsset Asset;

        public readonly string[] InputParameterNames;
        
        public readonly Type[] InputTypes;
        
        public readonly Type ReturnType;

        public FlowGraphFunction(FlowGraphFunctionAsset asset)
        {
            ContainerType = asset.GetRuntimeType();
            Asset = asset;
            InputParameterNames = asset.serializedInfo.inputParameters.Select(parameter => parameter.parameterName).ToArray();
            InputTypes = asset.serializedInfo.inputParameters.Select(parameter => parameter.GetParameterType()).ToArray();
            ReturnType = asset.serializedInfo.returnParameter.hasReturn ?
                         asset.serializedInfo.returnParameter.GetParameterType() : typeof(void);
        }
    }
    
    public class FlowGraphFunctionRegistry
    {
        private static FlowGraphFunctionRegistry _instance;

        private readonly List<FlowGraphFunction> _functions;

        static FlowGraphFunctionRegistry()
        {
            /* Clear cache when asset update */
            FlowGraphFunctionAsset.OnFunctionUpdate = _ => _instance = null;
        }
        
        private FlowGraphFunctionRegistry()
        {
            _functions = AssetDatabase.FindAssets($"t:{nameof(FlowGraphFunctionAsset)}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<FlowGraphFunctionAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Select(asset => new FlowGraphFunction(asset))
                .ToList();
        }
        
        public static FlowGraphFunctionRegistry Get()
        {
            return _instance ??= new FlowGraphFunctionRegistry();
        }

        public FlowGraphFunction[] GetFlowGraphFunctions(Type runtimeType)
        {
            return _functions.Where(function => function.ContainerType == null 
                                                || function.ContainerType.IsAssignableFrom(runtimeType)).ToArray();
        }

        public FlowGraphFunction FindFlowGraphFunctionFromAsset(FlowGraphFunctionAsset asset)
        {
            return _functions.FirstOrDefault(function => function.Asset == asset);
        }
    }
}
