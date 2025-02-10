﻿using System;
using System.Collections.Generic;
using Ceres.Annotations;
using Chris.Events;
using Cysharp.Threading.Tasks;

namespace Ceres.Graph.Flow
{
    /// <summary>
    /// Base class for <see cref="ExecutableEvent"/> generated from <see cref="EventBase{T}"/> by Ceres.SourceGenerator
    /// </summary>
    [Serializable]
    [CeresMetadata("style = ImplementableEvent")]
    public abstract class GeneratedExecutableEvent: ExecutableEvent
    {
        private static readonly Dictionary<long, string> Id2EventNameMap = new();
        
        [OutputPort(false), CeresLabel("")]
        public NodePort exec;
        
        protected sealed override UniTask Execute(ExecutionContext executionContext)
        {
            LocalExecute(executionContext);
            executionContext.SetNext(exec.GetT<ExecutableNode>());
            return UniTask.CompletedTask;
        }

        protected virtual void LocalExecute(ExecutionContext executionContext)
        {
            
        }
        
        
        protected static void RegisterEventId<TEventBase>(string eventName) where TEventBase: EventBase<TEventBase>, new()
        {
            Id2EventNameMap[EventBase<TEventBase>.TypeId()] = eventName;
        }
        
        internal static string GetEventName(long eventId)
        {
            return Id2EventNameMap.GetValueOrDefault(eventId);
        }

        internal static string GetEventBaseName(Type type)
        {
            return type.Name[(nameof(ExecutableEvent).Length + 1)..];
        }
    }

    public abstract class GeneratedExecutableEvent<TEventBase> : GeneratedExecutableEvent
        where TEventBase: EventBase<TEventBase>, new()
    {
        // ReSharper disable once MemberCanBePrivate.Global
        protected static readonly string EventName = $"{nameof(GeneratedExecutableEvent)}_{typeof(TEventBase).Name}";
        
        static GeneratedExecutableEvent()
        {
            /* Register event name with event id for lookup purpose */
            RegisterEventId<TEventBase>(EventName);
        }
    }
}