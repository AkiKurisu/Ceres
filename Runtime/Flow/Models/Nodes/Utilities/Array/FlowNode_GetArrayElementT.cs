﻿using System;
using System.Collections.Generic;
using Ceres.Annotations;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Retrieve an element from an array at a given index.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    [CeresGroup("Utilities/Array")]
    [CeresLabel("Get")]
    [CeresMetadata("style = ConstNode", "path = Dependency")]
    public sealed class FlowNode_GetArrayElementT<T>: ExecutableNode
    {
        [InputPort(true), CeresLabel(""), HideInGraphEditor]
        public CeresPort<IReadOnlyList<T>> array;
        
        [InputPort, CeresLabel("")]
        public CeresPort<int> index;
        
        [OutputPort, CeresLabel("")]
        public CeresPort<T> element;
        
        protected override UniTask Execute(ExecutionContext executionContext)
        {
            element.Value = array.Value[index.Value];
            return UniTask.CompletedTask;
        }
    }
}