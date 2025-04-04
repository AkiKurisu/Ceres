﻿using System;
using Ceres.Annotations;
using Chris.Serialization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

namespace Ceres.Graph.Flow.CustomFunctions
{
    [Serializable]
    public class CustomFunctionParameter
    {
        public string serializedTypeString;

        public bool isArray;

        public Type GetParameterType()
        {
            var type = SerializedType.FromString(serializedTypeString);
            type ??= typeof(object);
            if (isArray)
            {
                type = type.MakeArrayType();
            }

            return type;
        }
    }

    [Serializable]
    public class CustomFunctionInputParameter: CustomFunctionParameter
    {
        public string parameterName;
    }

    [Serializable]
    [CeresGroup("Hidden")]
    [CeresLabel("Function Input")]
    [CeresMetadata("style = CustomFunctionInput")]
    public class CustomFunctionInput: ExecutableEvent, ISerializationCallbackReceiver, IReadOnlyPortArrayNode
    {
        [OutputPort(false), CeresLabel("")]
        public NodePort exec;
        
        /* Proxy port, ports connected it will map to internal port */
        [OutputPort]
        public CeresPort<CeresPort>[] outputs = Array.Empty<CeresPort<CeresPort>>();
        
        [HideInGraphEditor]
        public CustomFunctionInputParameter[] parameters;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            Assert.IsNotNull(executionContext.GetEvent());
            var evt = executionContext.GetEventT<ExecuteSubFlowEvent>();
            if (evt.Args != null)
            {
                for (var i = 0; i < evt.Args.Count; ++i)
                {
                    outputs[i].Value = evt.Args[i];
                }
            }
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }

        public override string GetEventName()
        {
            return nameof(CustomFunctionInput);
        }

        public void OnBeforeSerialize()
        {
            
        }

        public void OnAfterDeserialize()
        {
            outputs = new CeresPort<CeresPort>[parameters?.Length ?? 0];
            for (int i = 0; i < outputs.Length; i++)
            {
                outputs[i] = new CeresPort<CeresPort>();
            }
        }

        public int GetPortArrayLength()
        {
            return parameters?.Length ?? 0;
        }

        public string GetPortArrayFieldName()
        {
            return nameof(outputs);
        }
    }
}