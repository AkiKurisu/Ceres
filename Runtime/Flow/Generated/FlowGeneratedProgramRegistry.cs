using System;
using System.Collections.Generic;
using System.Threading;
using Chris.Events;
using Cysharp.Threading.Tasks;
using R3.Chris;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Ceres.Graph.Flow
{
    public interface IFlowExecutableProgram : IDisposable
    {
        bool TryExecuteEvent(UObject contextObject, string eventName, EventBase evtBase = null);
    }

    public interface IFlowProgramRuntime
    {
        IFlowExecutableProgram Program { get; }
    }

    public interface IFlowGeneratedRuntimeContainer : IFlowGraphContainer
    {
        FlowGeneratedProgramInfo GeneratedRuntimeInfo { get; }
    }

    public interface IFlowEventDelegateTarget
    {
        UObject CurrentContextObject { get; }

        bool TryExecuteEvent(UObject contextObject, string eventName, EventBase evtBase = null);
    }

    public interface IFlowEventHandlerProvider
    {
        CallbackEventHandler GetEventHandler(UObject contextObject);
    }

    public interface IFlowEventExecutionHandler
    {
        bool CanExecuteCustomEvent(long eventTypeId);

        void ExecuteCustomEvent(EventBase eventBase);
    }

    public abstract class FlowGeneratedProgram : IFlowExecutableProgram, IFlowEventDelegateTarget,
        IFlowEventHandlerProvider, IDisposableUnregister
    {
        private sealed class EventHandler : CallbackEventHandler, IDisposable, IFlowEventExecutionHandler
        {
            public override IEventCoordinator Coordinator => EventSystem.Instance;

            private FlowGeneratedProgram _program;

            private UObject _contextObject;

            public EventHandler(FlowGeneratedProgram program, UObject contextObject)
            {
                _program = program;
                _contextObject = contextObject;
            }

            public override void SendEvent(EventBase e, DispatchMode dispatchMode = DispatchMode.Default)
            {
                e.Target = this;
                Coordinator.EventDispatcher.Dispatch(e, Coordinator, dispatchMode);
            }

            protected override void ExecuteDefaultAction(EventBase evt)
            {
                base.ExecuteDefaultAction(evt);
                ExecuteCustomEvent(evt);
            }

            public bool CanExecuteCustomEvent(long eventTypeId)
            {
                return _program != null && _program.TryGetCustomEventName(eventTypeId, out _);
            }

            public void ExecuteCustomEvent(EventBase eventBase)
            {
                if (_program == null || !_contextObject) return;
                if (!_program.TryGetCustomEventName(eventBase.EventTypeId, out var eventName)) return;
                _program.TryExecuteEvent(_contextObject, eventName, eventBase);
            }

            public void Dispose()
            {
                _program = null;
                _contextObject = null;
            }
        }

        private readonly List<IDisposable> _disposables = new();

        private readonly List<UObject> _executionContexts = new();

        private EventHandler _eventHandler;

        protected FlowGraphData GraphData { get; private set; }

        protected Blackboard Blackboard { get; private set; }

        public UObject CurrentContextObject =>
            _executionContexts.Count > 0 ? _executionContexts[^1] : null;

        protected FlowGeneratedProgram(FlowGraphData graphData)
            : this(graphData, true)
        {
        }

        protected FlowGeneratedProgram(FlowGraphData graphData, bool createBlackboard)
        {
            GraphData = graphData;
            Blackboard = createBlackboard ? FlowGeneratedRuntimeUtility.CreateBlackboard(graphData) : null;
        }

        public abstract bool TryExecuteEvent(UObject contextObject, string eventName, EventBase evtBase = null);

        protected virtual bool TryGetCustomEventName(long eventTypeId, out string eventName)
        {
            eventName = CustomExecutionEvent.GetEventName(eventTypeId);
            return !string.IsNullOrEmpty(eventName);
        }

        public CallbackEventHandler GetEventHandler(UObject contextObject)
        {
            _eventHandler ??= new EventHandler(this, contextObject);
            return _eventHandler;
        }

        protected void PushExecutionContext(UObject contextObject)
        {
            _executionContexts.Add(contextObject);
        }

        protected void PopExecutionContext(UObject contextObject)
        {
            for (var i = _executionContexts.Count - 1; i >= 0; i--)
            {
                if (_executionContexts[i] != contextObject) continue;
                _executionContexts.RemoveAt(i);
                return;
            }
        }

        protected void RegisterDisposable(IDisposable disposable)
        {
            if (disposable != null)
            {
                _disposables.Add(disposable);
            }
        }

        void IDisposableUnregister.Register(IDisposable disposable)
        {
            RegisterDisposable(disposable);
        }

        protected void RunEvent(UniTask task, EventBase evtBase)
        {
            evtBase?.Acquire();
            RunEventAsync(task, evtBase).Forget();
        }

        private static async UniTaskVoid RunEventAsync(UniTask task, EventBase evtBase)
        {
            try
            {
                await task;
            }
            finally
            {
                evtBase?.Dispose();
            }
        }

        protected static CancellationTokenSourceHandle GetCancellation(UObject context, string eventName = null)
        {
            return CancellationTokenSourceHandle.Get(context, eventName);
        }

        public virtual void Dispose()
        {
            for (var i = 0; i < _disposables.Count; i++)
            {
                _disposables[i]?.Dispose();
            }

            _disposables.Clear();
            _executionContexts.Clear();
            _eventHandler?.Dispose();
            _eventHandler = null;
            FlowGeneratedRuntimeUtility.DisposeBlackboard(Blackboard);
            Blackboard = null;
            GraphData = null;
        }
    }

    public sealed class FlowGeneratedActionInvoker<TTarget>
    {
        private readonly ExecutableAction<TTarget> _action;

        private FlowGeneratedActionInvoker(ExecutableAction<TTarget> action)
        {
            _action = action;
        }

        public static FlowGeneratedActionInvoker<TTarget> Create(bool isStatic, string methodName, int parameterCount)
        {
            var functionType = isStatic ? ExecutableFunctionType.StaticMethod : ExecutableFunctionType.InstanceMethod;
            var function = ExecutableReflection<TTarget>.GetFunction(functionType, methodName, parameterCount);
            return new FlowGeneratedActionInvoker<TTarget>(function.ExecutableAction);
        }

        public void Prewarm() => _action.Prewarm();

        public void Prewarm<T1>() => _action.Prewarm<T1>();

        public void Prewarm<T1, T2>() => _action.Prewarm<T1, T2>();

        public void Prewarm<T1, T2, T3>() => _action.Prewarm<T1, T2, T3>();

        public void Prewarm<T1, T2, T3, T4>() => _action.Prewarm<T1, T2, T3, T4>();

        public void Prewarm<T1, T2, T3, T4, T5>() => _action.Prewarm<T1, T2, T3, T4, T5>();

        public void Prewarm<T1, T2, T3, T4, T5, T6>() => _action.Prewarm<T1, T2, T3, T4, T5, T6>();

        public void Invoke(TTarget target) => _action.Invoke(target);

        public void Invoke<T1>(TTarget target, T1 arg1) => _action.Invoke(target, arg1);

        public void Invoke<T1, T2>(TTarget target, T1 arg1, T2 arg2) => _action.Invoke(target, arg1, arg2);

        public void Invoke<T1, T2, T3>(TTarget target, T1 arg1, T2 arg2, T3 arg3) =>
            _action.Invoke(target, arg1, arg2, arg3);

        public void Invoke<T1, T2, T3, T4>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4) =>
            _action.Invoke(target, arg1, arg2, arg3, arg4);

        public void Invoke<T1, T2, T3, T4, T5>(TTarget target, T1 arg1, T2 arg2, T3 arg3, T4 arg4,
            T5 arg5) => _action.Invoke(target, arg1, arg2, arg3, arg4, arg5);

        public void Invoke<T1, T2, T3, T4, T5, T6>(TTarget target, T1 arg1, T2 arg2, T3 arg3,
            T4 arg4, T5 arg5, T6 arg6) => _action.Invoke(target, arg1, arg2, arg3, arg4, arg5, arg6);
    }

    public sealed class FlowGeneratedFuncInvoker<TTarget>
    {
        private readonly ExecutableFunc<TTarget> _func;

        private FlowGeneratedFuncInvoker(ExecutableFunc<TTarget> func)
        {
            _func = func;
        }

        public static FlowGeneratedFuncInvoker<TTarget> Create(bool isStatic, string methodName, int parameterCount)
        {
            var functionType = isStatic ? ExecutableFunctionType.StaticMethod : ExecutableFunctionType.InstanceMethod;
            var function = ExecutableReflection<TTarget>.GetFunction(functionType, methodName, parameterCount);
            return new FlowGeneratedFuncInvoker<TTarget>(function.ExecutableFunc);
        }

        public void Prewarm<TR>() => _func.Prewarm<TR>();

        public void Prewarm<T1, TR>() => _func.Prewarm<T1, TR>();

        public void Prewarm<T1, T2, TR>() => _func.Prewarm<T1, T2, TR>();

        public void Prewarm<T1, T2, T3, TR>() => _func.Prewarm<T1, T2, T3, TR>();

        public void Prewarm<T1, T2, T3, T4, TR>() => _func.Prewarm<T1, T2, T3, T4, TR>();

        public void Prewarm<T1, T2, T3, T4, T5, TR>() => _func.Prewarm<T1, T2, T3, T4, T5, TR>();

        public void Prewarm<T1, T2, T3, T4, T5, T6, TR>() => _func.Prewarm<T1, T2, T3, T4, T5, T6, TR>();

        public TR Invoke<TR>(TTarget target) => _func.Invoke<TR>(target);

        public TR Invoke<T1, TR>(TTarget target, T1 arg1) => _func.Invoke<T1, TR>(target, arg1);

        public TR Invoke<T1, T2, TR>(TTarget target, T1 arg1, T2 arg2) =>
            _func.Invoke<T1, T2, TR>(target, arg1, arg2);

        public TR Invoke<T1, T2, T3, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3) =>
            _func.Invoke<T1, T2, T3, TR>(target, arg1, arg2, arg3);

        public TR Invoke<T1, T2, T3, T4, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3,
            T4 arg4) => _func.Invoke<T1, T2, T3, T4, TR>(target, arg1, arg2, arg3, arg4);

        public TR Invoke<T1, T2, T3, T4, T5, TR>(TTarget target, T1 arg1, T2 arg2, T3 arg3,
            T4 arg4, T5 arg5) => _func.Invoke<T1, T2, T3, T4, T5, TR>(target, arg1, arg2, arg3, arg4, arg5);

        public TR Invoke<T1, T2, T3, T4, T5, T6, TR>(TTarget target, T1 arg1, T2 arg2,
            T3 arg3, T4 arg4, T5 arg5, T6 arg6) =>
            _func.Invoke<T1, T2, T3, T4, T5, T6, TR>(target, arg1, arg2, arg3, arg4, arg5, arg6);
    }

    public readonly struct CancellationTokenSourceHandle : IDisposable
    {
        private const string OnDestroyEventName = "OnDestroy";

        private readonly CancellationToken _token;

        public CancellationToken Token => _token;

        private CancellationTokenSourceHandle(CancellationToken token)
        {
            _token = token;
        }

        public static CancellationTokenSourceHandle Get(UObject context, string eventName = null)
        {
            var token = GetCancellationToken(context);
            if (token.IsCancellationRequested && string.Equals(eventName, OnDestroyEventName, StringComparison.Ordinal))
            {
                token = CancellationToken.None;
            }

            return new CancellationTokenSourceHandle(token);
        }

        private static CancellationToken GetCancellationToken(UObject context)
        {
            return context switch
            {
                GameObject gameObject => gameObject.GetCancellationTokenOnDestroy(),
                MonoBehaviour monoBehaviour => monoBehaviour.GetCancellationTokenOnDestroy(),
                Component component => component.GetCancellationTokenOnDestroy(),
                _ => CancellationToken.None
            };
        }

        public void ThrowIfCancellationRequested()
        {
            _token.ThrowIfCancellationRequested();
        }

        public void Dispose()
        {
        }
    }

    public static class FlowGeneratedProgramRegistry
    {
        public delegate IFlowExecutableProgram ProgramFactory(FlowGraphData graphData);

        private readonly struct Entry
        {
            public readonly string GraphHash;

            public readonly string TypeName;

            public readonly ProgramFactory Factory;

            public Entry(string graphHash, string typeName, ProgramFactory factory)
            {
                GraphHash = graphHash;
                TypeName = typeName;
                Factory = factory;
            }
        }

        private static readonly Dictionary<string, Entry> Entries = new();

        public static void Register(string programId, string graphHash, string generatedTypeName, ProgramFactory factory)
        {
            if (string.IsNullOrEmpty(programId) || factory == null) return;
            Entries[programId] = new Entry(graphHash, generatedTypeName, factory);
        }

        public static void Clear()
        {
            Entries.Clear();
        }

        public static bool TryCreate(FlowGeneratedProgramInfo info, FlowGraphData graphData,
            out IFlowExecutableProgram program)
        {
            program = null;
            if (info == null || !info.enabled || string.IsNullOrEmpty(info.GetProgramId()))
            {
                return false;
            }

            if (info.generatorVersion != FlowGeneratedRuntimeUtility.CurrentProgramInfoVersion)
            {
                return false;
            }

            if (!FlowGeneratedRuntimeUtility.AreGeneratedRuntimeOptionsCurrent(info))
            {
                return false;
            }

            if (!Entries.TryGetValue(info.GetProgramId(), out var entry))
            {
                return false;
            }

            var currentHash = FlowGeneratedRuntimeUtility.CalculateGraphHash(graphData);
            if (entry.GraphHash != currentHash || info.graphHash != currentHash)
            {
                return false;
            }

#if UNITY_EDITOR
            if (!info.AreFunctionDependenciesCurrent())
            {
                return false;
            }
#endif

            program = entry.Factory(graphData);
            return program != null;
        }
    }
}
