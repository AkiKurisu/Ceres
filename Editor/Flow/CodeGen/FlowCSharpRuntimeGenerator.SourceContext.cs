using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.CustomFunctions;
using Ceres.Graph.Flow.Properties;
using Ceres.Graph.Flow.Utilities;
using Ceres.Utilities;
using Chris.Serialization;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UObject = UnityEngine.Object;


namespace Ceres.Editor.Graph.Flow.CodeGen
{
    public static partial class FlowCSharpRuntimeGenerator
    {
        internal sealed partial class SourceContext
        {
            private const string SynchronousExitLabel = "Complete";
            private const string SynchronousResultVariable = "__ceresResult";
            private const string FrameArgumentMarker = "/*__CERES_FRAME_ARG__*/";

            private enum CustomFunctionReturnEmission
            {
                None,
                ReturnMethodValue,
                SetSubFlowReturn
            }

            internal enum DependencyCancellationCheck
            {
                EmitIfNodeWillRun,
                AlreadyChecked
            }

            private FlowGraph _graph;

            private readonly string _className;

            private readonly FlowGeneratedRuntimeProfile _profile;

            private readonly FlowGeneratedRuntimeCancellationMode _cancellationMode;

            private readonly FlowGeneratedRuntimeVariableStorageMode _variableStorageMode;

            private readonly FlowGeneratedRuntimeSerializedTypeMode _serializedTypeMode;

            private Dictionary<string, CeresNode> _nodes;

            private readonly Dictionary<string, SharedVariableBinding> _sharedVariableBindings = new();

            private readonly Dictionary<string, LocalVariableBinding> _localVariableBindings = new();

            private readonly Dictionary<string, FunctionInvokerBinding> _functionInvokerBindings = new();

            private readonly Dictionary<string, EventDelegateBinding> _eventDelegateBindings = new();

            private readonly Dictionary<string, SerializedTypeBinding> _serializedTypeBindings = new();

            private readonly Dictionary<string, CustomFunctionBinding> _customFunctionBindings = new();

            private readonly Dictionary<string, FlowGeneratedFunctionDependencyInfo> _functionAssetDependencies = new();

            private readonly HashSet<string> _generatedCustomFunctionMethods = new();

            private readonly Queue<string> _pendingCustomFunctionMethods = new();

            private readonly StringBuilder _members = new();

            private readonly Dictionary<string, FrameSlotInfo> _frameSlots = new();

            private readonly Dictionary<string, ProgramFieldInfo> _programFields = new();

            private readonly HashSet<string> _generatedDependencyMethods = new();

            private readonly Queue<CeresNode> _pendingDependencyMethods = new();

            private readonly HashSet<string> _cancellationHandleFrames = new();

            private readonly StringBuilder _deferredMembers = new();

            private StringBuilder _currentBody;

            private string _frameFieldMarker;

            private string _frameReleaseMarker;

            private string _currentFrameName;

            private bool _currentFramePassByReference;

            private bool _currentFrameNeedsCancellation;

            private bool _currentFrameNeedsEventBase;

            private CustomFunctionReturnEmission _customFunctionReturnEmission;

            private readonly Stack<string> _inlineEventStack = new();

            public SourceContext(FlowGraph graph, string className)
            {
                _graph = graph;
                _className = className;
                _profile = FlowGeneratedRuntimeUtility.CurrentGeneratedRuntimeProfile;
                _cancellationMode = FlowGeneratedRuntimeUtility.CurrentGeneratedRuntimeCancellationMode;
                _variableStorageMode = FlowGeneratedRuntimeUtility.CurrentGeneratedRuntimeVariableStorageMode;
                _serializedTypeMode = FlowGeneratedRuntimeUtility.CurrentGeneratedRuntimeSerializedTypeMode;
                _nodes = graph.nodes.ToDictionary(node => node.Guid);
                foreach (var node in graph.nodes.OfType<PropertyNode_SharedVariableValue>())
                {
                    if (!CanAccessSharedVariable(node, out var variableType, out var variableValueType, out _))
                    {
                        continue;
                    }

                    GetOrCreateGraphVariableBinding(node.propertyName, variableType, variableValueType);
                }

                foreach (var node in graph.nodes.OfType<FlowNode_ExecuteFunction>())
                {
                    if (CanGeneratedCall(node, out var callInfo) && callInfo.UseInvoker)
                    {
                        GetOrCreateFunctionInvokerBinding(node, callInfo);
                    }
                }
            }

            public void ValidateSupport(string assetName)
            {
                var context = new NodeGenerationContext(this, null, null, string.Empty);
                foreach (var evt in _graph.Events)
                {
                    var eventInfo = CreateEventInfo(evt);
                    if (!eventInfo.ArgumentTypes.All(IsVisibleType) || !IsVisibleType(eventInfo.ReturnType))
                    {
                        throw new FlowCSharpRuntimeGenerationException(
                            $"{assetName} uses event {evt.GetEventName()} ({evt.Guid}) with types that generated C# can not access.");
                    }
                }

                foreach (var node in _graph.nodes)
                {
                    if (node is ExecutableEvent) continue;
                    if (!NodeGeneratorFactory.Get().TryGetGenerator(node.GetType(), out var generator) ||
                        !generator.CanGenerate(node, context))
                    {
                        throw new FlowCSharpRuntimeGenerationException(
                            $"{assetName} uses unsupported codegen node {node.GetType().Name} ({node.Guid}). Add an editor node generator before enabling generated runtime.");
                    }
                }
            }

            public FlowGeneratedFunctionDependencyInfo[] GetFunctionDependencies()
            {
                return _functionAssetDependencies.OrderBy(pair => pair.Key)
                    .Select(pair => pair.Value)
                    .ToArray();
            }

            public string Generate()
            {
                var tryExecuteBody = new StringBuilder();
                GenerateTryExecuteEvent(tryExecuteBody);
                var generatedMembers = _members.ToString();

                var body = new StringBuilder();
                AppendGeneratedPreamble(body, ProgramUsingLines);
                body.AppendLine($"namespace {GeneratedNamespace}");
                body.AppendLine("{");
                body.AppendLine($"    public sealed class {_className} : FlowGeneratedProgram");
                body.AppendLine("    {");
                foreach (var binding in _sharedVariableBindings.Values)
                {
                    body.AppendLine($"        private readonly {GetFriendlyTypeName(binding.VariableType)} {binding.FieldName};");
                }
                foreach (var binding in _localVariableBindings.Values)
                {
                    body.AppendLine($"        private {GetFriendlyTypeName(binding.StorageType)} {binding.FieldName};");
                }
                foreach (var binding in _functionInvokerBindings.Values)
                {
                    var invokerType = binding.ReturnType == typeof(void)
                        ? "FlowGeneratedActionInvoker"
                        : "FlowGeneratedFuncInvoker";
                    body.AppendLine($"        private readonly {invokerType}<{GetFriendlyTypeName(binding.TargetType)}> {binding.FieldName};");
                }
                foreach (var binding in _eventDelegateBindings.Values)
                {
                    body.AppendLine($"        private readonly {GetFriendlyTypeName(binding.DelegateType)} {binding.FieldName};");
                }
                foreach (var binding in _serializedTypeBindings.Values)
                {
                    body.AppendLine($"        private static readonly {GetFriendlyTypeName(binding.SerializedType)} {binding.FieldName} = new() {{ serializedTypeString = \"{Escape(binding.SerializedTypeString)}\" }};");
                }
                foreach (var field in _programFields.Values)
                {
                    body.AppendLine($"        private {GetFriendlyTypeName(field.Type)} {field.FieldName};");
                }
                if (_sharedVariableBindings.Count > 0 || _localVariableBindings.Count > 0 ||
                    _functionInvokerBindings.Count > 0 ||
                    _eventDelegateBindings.Count > 0 || _serializedTypeBindings.Count > 0 ||
                    _programFields.Count > 0)
                {
                    body.AppendLine();
                }
                var createBlackboard = _sharedVariableBindings.Count > 0;
                body.AppendLine($"        public {_className}(FlowGraphData graphData) : base(graphData, {createBlackboard.ToString().ToLowerInvariant()})");
                body.AppendLine("        {");
                foreach (var binding in _sharedVariableBindings.Values)
                {
                    body.AppendLine($"            {binding.FieldName} = FlowGeneratedRuntimeUtility.GetRequiredSharedVariable<{GetFriendlyTypeName(binding.VariableType)}>(Blackboard, \"{Escape(binding.VariableName)}\");");
                }
                foreach (var binding in _localVariableBindings.Values)
                {
                    body.AppendLine($"            {binding.FieldName} = {binding.InitializerExpression};");
                }
                foreach (var binding in _functionInvokerBindings.Values)
                {
                    var invokerType = binding.ReturnType == typeof(void)
                        ? "FlowGeneratedActionInvoker"
                        : "FlowGeneratedFuncInvoker";
                    body.AppendLine($"            {binding.FieldName} = {invokerType}<{GetFriendlyTypeName(binding.TargetType)}>.Create({binding.IsStatic.ToString().ToLowerInvariant()}, \"{Escape(binding.MethodName)}\", {binding.ParameterTypes.Length});");
                    body.AppendLine($"            {binding.FieldName}.{BuildPrewarmCall(binding)};");
                }
                foreach (var binding in _eventDelegateBindings.Values)
                {
                    body.AppendLine($"            {binding.FieldName} = new {GetFriendlyTypeName(binding.DelegateType)}();");
                    body.AppendLine($"            {binding.FieldName}.Bind(this, \"{Escape(binding.EventName)}\");");
                }
                foreach (var binding in _serializedTypeBindings.Values)
                {
                    body.AppendLine($"            FlowGeneratedRuntimeUtility.PrewarmSerializedType({binding.FieldName});");
                }
                body.AppendLine("        }");
                body.AppendLine();
                body.Append(tryExecuteBody);
                body.Append(generatedMembers);
                body.AppendLine("    }");
                body.AppendLine("}");
                return body.ToString();
            }

            private void GenerateTryExecuteEvent(StringBuilder body)
            {
                body.AppendLine("        public override bool TryExecuteEvent(UObject contextObject, string eventName, EventBase evtBase = null)");
                body.AppendLine("        {");
                body.AppendLine("            switch (eventName)");
                body.AppendLine("            {");
                foreach (var evt in _graph.Events)
                {
                    GenerateEvent(evt, body);
                }
                body.AppendLine("                default:");
                body.AppendLine("                    return false;");
                body.AppendLine("            }");
                body.AppendLine("        }");
                body.AppendLine();
            }

            private void GenerateEvent(ExecutableEvent evt, StringBuilder switchBody)
            {
                var eventName = evt.GetEventName();
                var methodName = $"Execute_{SanitizeIdentifier(eventName)}_{SafeGuid(evt.Guid)}";
                var frameName = $"Frame_{SanitizeIdentifier(eventName)}_{SafeGuid(evt.Guid)}";
                var eventInfo = CreateEventInfo(evt);

                BeginFrame(frameName);
                var previousReturnEmission = _customFunctionReturnEmission;
                _customFunctionReturnEmission = eventInfo.ReturnType == typeof(void)
                    ? CustomFunctionReturnEmission.None
                    : CustomFunctionReturnEmission.SetSubFlowReturn;
                try
                {
                    _cancellationHandleFrames.Add(frameName);
                    var body = CaptureMethodBody(() =>
                    {
                        Emit("                PushExecutionContext(frame.ContextObject);");
                        GenerateForwardConnection(GetExecConnection(evt, "exec"), frameName, "frame", "                ");
                    });
                    var useStackFrame = ShouldUseStackFrame(body);
                    _currentFramePassByReference = useStackFrame;
                    body = ResolveFrameArgumentMarkers(body, useStackFrame);
                    GenerateFrame(frameName, eventInfo, eventName, _currentFrameNeedsCancellation,
                        _currentFrameNeedsEventBase, useStackFrame);
                    var hasAwait = AppendUniTaskMethod(methodName, frameName, typeof(void), body,
                        true,
                        useStackFrame,
                        "                PopExecutionContext(frame.ContextObject);",
                        "                frame.Release();");

                    switchBody.AppendLine($"                case \"{Escape(eventName)}\":");
                    switchBody.AppendLine("                {");
                    switchBody.AppendLine($"                    var frame = {frameName}.Get(contextObject, evtBase);");
                    switchBody.AppendLine(hasAwait
                        ? $"                    RunEvent({methodName}(frame), evtBase);"
                        : $"                    {methodName}({(useStackFrame ? "ref " : string.Empty)}frame);");
                    switchBody.AppendLine("                    return true;");
                    switchBody.AppendLine("                }");
                    CompleteFrame();
                }
                finally
                {
                    _customFunctionReturnEmission = previousReturnEmission;
                }
            }

            private EventInfo CreateEventInfo(ExecutableEvent evt)
            {
                if (evt is CustomFunctionInput input)
                {
                    var arguments = input.parameters?.Select(parameter => parameter.GetParameterType()).ToArray() ??
                                    Type.EmptyTypes;
                    var output = _graph.nodes.OfType<CustomFunctionOutput>()
                        .FirstOrDefault(node => node.parameter?.hasReturn == true);
                    var returnType = output?.parameter?.hasReturn == true
                        ? output.parameter.GetParameterType()
                        : typeof(void);
                    return EventInfo.FromCustomFunction(arguments, returnType);
                }

                return EventInfo.From(evt);
            }

            private void BeginFrame(string frameName)
            {
                _currentFrameName = frameName;
                _frameSlots.Clear();
                _generatedDependencyMethods.Clear();
                _pendingDependencyMethods.Clear();
                _deferredMembers.Clear();
                _frameFieldMarker = $"            /* __CERES_FRAME_FIELDS_{frameName}__ */";
                _frameReleaseMarker = $"                /* __CERES_FRAME_RELEASE_{frameName}__ */";
                _currentFramePassByReference = false;
                _currentFrameNeedsCancellation = false;
                _currentFrameNeedsEventBase = false;
            }

            private void CompleteFrame()
            {
                FlushDependencyMethods();
                var fieldBuilder = new StringBuilder();
                var releaseBuilder = new StringBuilder();
                foreach (var slot in _frameSlots.Values)
                {
                    fieldBuilder.AppendLine($"            public {GetFriendlyTypeName(slot.Type)} {slot.FieldName};");
                    releaseBuilder.AppendLine($"                {slot.FieldName} = default;");
                }

                _members.Replace(_frameFieldMarker, fieldBuilder.ToString().TrimEnd('\r', '\n'));
                _members.Replace(_frameReleaseMarker, releaseBuilder.ToString().TrimEnd('\r', '\n'));
                _members.Append(ResolveFrameArgumentMarkers(_deferredMembers.ToString(), _currentFramePassByReference));
                _currentFrameName = null;
                _frameFieldMarker = null;
                _frameReleaseMarker = null;
                _frameSlots.Clear();
                _generatedDependencyMethods.Clear();
                _pendingDependencyMethods.Clear();
                _deferredMembers.Clear();
                _currentFramePassByReference = false;
                _currentFrameNeedsCancellation = false;
                _currentFrameNeedsEventBase = false;
                FlushCustomFunctionMethods();
            }

            internal void Emit(string line)
            {
                (_currentBody ?? _members).AppendLine(line);
            }

            private string CaptureMethodBody(Action generate)
            {
                var previousBody = _currentBody;
                var body = new StringBuilder();
                _currentBody = body;
                try
                {
                    generate();
                    return body.ToString();
                }
                finally
                {
                    _currentBody = previousBody;
                }
            }

            private bool AppendUniTaskMethod(string methodName, string frameName, Type resultType, string body,
                params string[] finallyLines)
            {
                return AppendUniTaskMethod(methodName, frameName, resultType, body, false, false, finallyLines);
            }

            private bool AppendUniTaskMethod(string methodName, string frameName, Type resultType, string body,
                bool emitSynchronousVoidMethod, params string[] finallyLines)
            {
                return AppendUniTaskMethod(methodName, frameName, resultType, body, emitSynchronousVoidMethod, false,
                    finallyLines);
            }

            private bool AppendUniTaskMethod(string methodName, string frameName, Type resultType, string body,
                bool emitSynchronousVoidMethod, bool passFrameByReference, params string[] finallyLines)
            {
                var hasAwait = HasAwaitExpression(body);
                if (emitSynchronousVoidMethod && !hasAwait && resultType == typeof(void))
                {
                    _members.AppendLine($"        private void {methodName}({(passFrameByReference ? "ref " : string.Empty)}{frameName} frame)");
                    _members.AppendLine("        {");
                    _members.AppendLine("            try");
                    _members.AppendLine("            {");
                    _members.Append(body);
                    _members.AppendLine("            }");
                    _members.AppendLine("            finally");
                    _members.AppendLine("            {");
                    foreach (var line in finallyLines)
                    {
                        _members.AppendLine(line);
                    }
                    _members.AppendLine("            }");
                    _members.AppendLine("        }");
                    _members.AppendLine();
                    return false;
                }

                var returnTypeName = resultType == typeof(void)
                    ? "UniTask"
                    : $"UniTask<{GetFriendlyTypeName(resultType)}>";
                var hasEarlyExit = false;
                var emittedBody = hasAwait
                    ? body
                    : RewriteSynchronousUniTaskBody(body, resultType, out hasEarlyExit);

                _members.AppendLine($"        private {(hasAwait ? "async " : string.Empty)}{returnTypeName} {methodName}({(passFrameByReference ? "ref " : string.Empty)}{frameName} frame)");
                _members.AppendLine("        {");
                if (!hasAwait && resultType != typeof(void))
                {
                    _members.AppendLine($"            {GetFriendlyTypeName(resultType)} {SynchronousResultVariable} = default;");
                }
                _members.AppendLine("            try");
                _members.AppendLine("            {");
                _members.Append(emittedBody);
                _members.AppendLine("            }");
                _members.AppendLine("            finally");
                _members.AppendLine("            {");
                foreach (var line in finallyLines)
                {
                    _members.AppendLine(line);
                }
                _members.AppendLine("            }");
                if (!hasAwait)
                {
                    if (hasEarlyExit)
                    {
                        _members.AppendLine($"        {SynchronousExitLabel}:");
                    }
                    _members.AppendLine($"            {BuildSynchronousUniTaskReturn(resultType)}");
                }
                _members.AppendLine("        }");
                _members.AppendLine();
                return hasAwait;
            }

            private static bool HasAwaitExpression(string body)
            {
                return body.Contains("await ", StringComparison.Ordinal);
            }

            private bool ShouldUseStackFrame(string body)
            {
                return _profile != FlowGeneratedRuntimeProfile.Debuggable &&
                       !HasAwaitExpression(body);
            }

            private static string ResolveFrameArgumentMarkers(string body, bool passByReference)
            {
                return body.Replace(FrameArgumentMarker, passByReference ? "ref " : string.Empty);
            }

            private static string RewriteSynchronousUniTaskBody(string body, Type resultType, out bool hasEarlyExit)
            {
                hasEarlyExit = false;
                var rewritten = new StringBuilder(body.Length);
                using var reader = new StringReader(body);
                while (reader.ReadLine() is { } line)
                {
                    if (TryRewriteReturnStatement(line, resultType, out var returnLines))
                    {
                        hasEarlyExit = true;
                        rewritten.Append(returnLines);
                        continue;
                    }

                    rewritten.AppendLine(line);
                }

                return rewritten.ToString();
            }

            private static bool TryRewriteReturnStatement(string line, Type resultType, out string rewritten)
            {
                rewritten = null;
                var trimmed = line.TrimStart();
                var indent = line[..(line.Length - trimmed.Length)];
                if (trimmed == "return;")
                {
                    rewritten = $"{indent}goto {SynchronousExitLabel};{Environment.NewLine}";
                    return true;
                }

                if (resultType == typeof(void) ||
                    !trimmed.StartsWith("return ", StringComparison.Ordinal) ||
                    !trimmed.EndsWith(";", StringComparison.Ordinal))
                {
                    return false;
                }

                var expression = trimmed["return ".Length..^1];
                rewritten =
                    $"{indent}{SynchronousResultVariable} = {expression};{Environment.NewLine}" +
                    $"{indent}goto {SynchronousExitLabel};{Environment.NewLine}";
                return true;
            }

            private static string BuildSynchronousUniTaskReturn(Type resultType)
            {
                return resultType == typeof(void)
                    ? "return UniTask.CompletedTask;"
                    : $"return UniTask.FromResult({SynchronousResultVariable});";
            }

            private void GenerateFrame(string frameName, EventInfo eventInfo, string eventName,
                bool includeCancellation, bool includeEventBase, bool useStackFrame)
            {
                _members.AppendLine(useStackFrame
                    ? $"        private struct {frameName}"
                    : $"        private sealed class {frameName}");
                _members.AppendLine("        {");
                if (!useStackFrame)
                {
                    _members.AppendLine($"            private static readonly ObjectPool<{frameName}> Pool = new(() => new {frameName}());");
                }
                _members.AppendLine("            public UObject ContextObject;");
                if (includeEventBase)
                {
                    _members.AppendLine("            public EventBase EventBase;");
                }
                if (includeCancellation)
                {
                    _members.AppendLine("            public CancellationTokenSourceHandle Cancellation;");
                }
                for (var i = 0; i < eventInfo.ArgumentTypes.Length; i++)
                {
                    _members.AppendLine($"            public {GetFriendlyTypeName(eventInfo.ArgumentTypes[i])} Arg{i + 1};");
                }
                if (eventInfo.ReturnType != typeof(void))
                {
                    _members.AppendLine($"            public {GetFriendlyTypeName(eventInfo.ReturnType)} ReturnValue;");
                }
                _members.AppendLine(_frameFieldMarker);
                _members.AppendLine();
                _members.AppendLine($"            public static {frameName} Get(UObject contextObject, EventBase evtBase)");
                _members.AppendLine("            {");
                _members.AppendLine(useStackFrame
                    ? $"                var frame = new {frameName}();"
                    : "                var frame = Pool.Get();");
                _members.AppendLine("                frame.ContextObject = contextObject;");
                if (includeEventBase)
                {
                    _members.AppendLine("                frame.EventBase = evtBase;");
                }
                if (includeCancellation)
                {
                    _members.AppendLine($"                frame.Cancellation = GetCancellation(contextObject, \"{Escape(eventName)}\");");
                }
                eventInfo.GenerateAssignments(_members, "frame", "evtBase", "                ");
                _members.AppendLine("                return frame;");
                _members.AppendLine("            }");
                _members.AppendLine();
                _members.AppendLine("            public void Release()");
                _members.AppendLine("            {");
                for (var i = 0; i < eventInfo.ArgumentTypes.Length; i++)
                {
                    _members.AppendLine($"                Arg{i + 1} = default;");
                }
                if (eventInfo.ReturnType != typeof(void))
                {
                    _members.AppendLine("                ReturnValue = default;");
                }
                _members.AppendLine(_frameReleaseMarker);
                _members.AppendLine("                ContextObject = null;");
                if (includeEventBase)
                {
                    _members.AppendLine("                EventBase = null;");
                }
                if (includeCancellation)
                {
                    _members.AppendLine("                Cancellation.Dispose();");
                    _members.AppendLine("                Cancellation = default;");
                }
                if (!useStackFrame)
                {
                    _members.AppendLine("                Pool.Release(this);");
                }
                _members.AppendLine("            }");
                _members.AppendLine("        }");
                _members.AppendLine();
            }

            internal void GenerateForwardNode(CeresNode node, string frameTypeName, string frameVar, string indent,
                string entryPortId = null, int entryPortIndex = -1)
            {
                if (ShouldEmitPerNodeCancellationChecks())
                {
                    Emit($"{indent}{GetCancellationCheckExpression(frameTypeName, frameVar)};");
                }

                var context = new NodeGenerationContext(this, frameTypeName, frameVar, indent, entryPortId,
                    entryPortIndex);
                if (!NodeGeneratorFactory.Get().TryGetGenerator(node.GetType(), out var generator) ||
                    !generator.CanGenerate(node, context))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Unsupported node {node.GetType().Name} ({node.Guid}) reached during generation.");
                }

                generator.GenerateForward(node, context);
            }

            internal void GenerateForwardConnection(ExecConnection connection, string frameTypeName, string frameVar,
                string indent)
            {
                if (connection.Node != null)
                {
                    GenerateForwardNode(connection.Node, frameTypeName, frameVar, indent, connection.PortId,
                        connection.PortIndex);
                }
            }

            private string GetCancellationCheckExpression(string frameTypeName, string frameVar)
            {
                if (_cancellationHandleFrames.Contains(frameTypeName))
                {
                    _currentFrameNeedsCancellation = true;
                    return $"{frameVar}.Cancellation.ThrowIfCancellationRequested()";
                }

                return $"{frameVar}.CancellationToken.ThrowIfCancellationRequested()";
            }

            private bool ShouldEmitPerNodeCancellationChecks()
            {
                return _cancellationMode switch
                {
                    FlowGeneratedRuntimeCancellationMode.Always => true,
                    FlowGeneratedRuntimeCancellationMode.NeverForSync => false,
                    _ => _profile == FlowGeneratedRuntimeProfile.Debuggable
                };
            }

            private bool ShouldExpandTransitiveDependencyPath()
            {
                return _profile == FlowGeneratedRuntimeProfile.Debuggable;
            }

            internal string GetCancellationTokenExpression(string frameTypeName, string frameVar)
            {
                if (_cancellationHandleFrames.Contains(frameTypeName))
                {
                    _currentFrameNeedsCancellation = true;
                    return $"{frameVar}.Cancellation.Token";
                }

                return $"{frameVar}.CancellationToken";
            }

            internal void GenerateSequence(FlowNode_Sequence node, string frameTypeName, string frameVar, string indent)
            {
                foreach (var next in GetExecConnections(node, "outputs"))
                {
                    GenerateForwardConnection(next, frameTypeName, frameVar, indent);
                }
            }

            internal void GenerateBranch(FlowNode_Branch node, string frameTypeName, string frameVar, string indent)
            {
                var condition = GetValueExpression(node, "condition", typeof(bool), frameTypeName, frameVar, indent);
                Emit($"{indent}if ({condition})");
                Emit($"{indent}{{");
                GenerateForwardConnection(GetExecConnection(node, "trueOutput"), frameTypeName, frameVar,
                    indent + "    ");
                Emit($"{indent}}}");
                Emit($"{indent}else");
                Emit($"{indent}{{");
                GenerateForwardConnection(GetExecConnection(node, "falseOutput"), frameTypeName, frameVar,
                    indent + "    ");
                Emit($"{indent}}}");
            }

            internal void GenerateDebugLog(FlowNode_DebugLog node, string frameTypeName, string frameVar, string indent)
            {
                var message = GetValueExpression(node, "message", typeof(object), frameTypeName, frameVar, indent);
                Emit($"{indent}Debug.unityLogger.Log(LogType.{node.logType}, {message}, {frameVar}.ContextObject);");
                GenerateDefaultNext(node, frameTypeName, frameVar, indent);
            }

            internal void GenerateDebugLogString(FlowNode_DebugLogString node, string frameTypeName, string frameVar,
                string indent)
            {
                var message = GetValueExpression(node, "inString", typeof(string), frameTypeName, frameVar, indent);
                Emit($"{indent}Debug.unityLogger.Log(LogType.{node.logType}, {message}, {frameVar}.ContextObject);");
                GenerateDefaultNext(node, frameTypeName, frameVar, indent);
            }

            internal bool CanGenerateExecuteEvent(FlowNode_ExecuteEvent node)
            {
                return TryResolveExecuteEventTarget(node, out _);
            }

            internal void GenerateExecuteEvent(FlowNode_ExecuteEvent node, string frameTypeName, string frameVar,
                string indent)
            {
                if (!TryResolveExecuteEventTarget(node, out var target))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"ExecuteEvent node {node.Guid} can not resolve no-argument event {node.eventName}.");
                }

                if (_inlineEventStack.Contains(target.Guid))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"ExecuteEvent node {node.Guid} creates a recursive event execution cycle with {target.GetEventName()}.");
                }

                _inlineEventStack.Push(target.Guid);
                try
                {
                    GenerateForwardConnection(GetExecConnection(target, "exec"), frameTypeName, frameVar, indent);
                }
                finally
                {
                    _inlineEventStack.Pop();
                }

                GenerateDefaultNext(node, frameTypeName, frameVar, indent);
            }

            private bool TryResolveExecuteEventTarget(FlowNode_ExecuteEvent node, out ExecutableEvent target)
            {
                target = _graph.Events.FirstOrDefault(evt => evt.GetEventName() == node.eventName) ??
                         FindNode(node.eventName) as ExecutableEvent;
                return target != null && target.GetType() == typeof(ExecutionEvent);
            }

            internal void GenerateExecuteFunctionVoid(FlowNode_ExecuteFunctionVoid node, string frameTypeName,
                string frameVar, string indent)
            {
                if (!CanGeneratedCall(node, out var directCall) || directCall.ReturnType != typeof(void))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Function node {node.Guid} can not be generated as a direct call.");
                }

                Emit($"{indent}{BuildFunctionCallExpression(node, directCall, frameTypeName, frameVar, indent)};");
                GenerateDefaultNext(node, frameTypeName, frameVar, indent);
            }

            internal bool CanGenerateCustomFunction(FlowNode_ExecuteCustomFunction node)
            {
                return TryResolveCustomFunction(node, out var graph, out _, out var returnType, out var parameterTypes) &&
                       ValidateCustomFunctionGraph(graph, returnType, parameterTypes);
            }

            internal void GenerateCustomFunctionCall(FlowNode_ExecuteCustomFunction node, string frameTypeName,
                string frameVar, string indent)
            {
                var binding = GetOrCreateCustomFunctionBinding(node);
                var arguments = new List<string>(binding.ParameterTypes.Length);
                for (var i = 0; i < binding.ParameterTypes.Length; i++)
                {
                    arguments.Add(GetValueExpression(node, $"input{i + 1}", binding.ParameterTypes[i], frameTypeName,
                        frameVar, indent));
                }

                var frameArguments = new List<string>
                {
                    $"{frameVar}.ContextObject",
                    GetCancellationTokenExpression(frameTypeName, frameVar)
                };
                frameArguments.AddRange(arguments);
                var call = $"{binding.MethodName}({binding.FrameName}.Get({string.Join(", ", frameArguments)}))";
                if (binding.ReturnType == typeof(void))
                {
                    Emit($"{indent}await {call};");
                }
                else
                {
                    var slot = EnsureOutputSlot(node, "output", binding.ReturnType);
                    Emit($"{indent}{frameVar}.{slot} = await {call};");
                }

                GenerateDefaultNext(node, frameTypeName, frameVar, indent);
            }

            internal bool TryGetCustomFunctionOutputSlot(FlowNode_ExecuteCustomFunction node, string portId,
                out Type outputType, out string slotField)
            {
                outputType = null;
                slotField = null;
                if (portId != "output" ||
                    !TryResolveCustomFunction(node, out _, out _, out var returnType, out _) ||
                    returnType == typeof(void))
                {
                    return false;
                }

                outputType = returnType;
                slotField = EnsureOutputSlot(node, portId, outputType);
                return true;
            }

            internal void GenerateCustomFunctionOutput(CustomFunctionOutput node, string frameTypeName,
                string frameVar, string indent)
            {
                if (node.parameter?.hasReturn == true)
                {
                    var returnType = node.parameter.GetParameterType();
                    var returnValue = GetValueExpression(node, nameof(CustomFunctionOutput.returnValue), returnType,
                        frameTypeName, frameVar, indent);
                    Emit($"{indent}{frameVar}.ReturnValue = {returnValue};");
                    switch (_customFunctionReturnEmission)
                    {
                        case CustomFunctionReturnEmission.ReturnMethodValue:
                            Emit($"{indent}return {frameVar}.ReturnValue;");
                            break;
                        case CustomFunctionReturnEmission.SetSubFlowReturn:
                            _currentFrameNeedsEventBase = true;
                            Emit($"{indent}FlowGeneratedRuntimeUtility.SetSubFlowReturn({frameVar}.EventBase, {frameVar}.ReturnValue);");
                            Emit($"{indent}return;");
                            break;
                        default:
                            throw new FlowCSharpRuntimeGenerationException(
                                $"Custom function output {node.Guid} has return value outside a return-capable function frame.");
                    }
                    return;
                }

                Emit($"{indent}return;");
            }

            private CustomFunctionBinding GetOrCreateCustomFunctionBinding(FlowNode_ExecuteCustomFunction node)
            {
                if (!TryResolveCustomFunction(node, out var graph, out var key, out var returnType,
                        out var parameterTypes))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Custom function node {node.Guid} can not resolve function {node.functionName}.");
                }

                if (_customFunctionBindings.TryGetValue(key, out var binding))
                {
                    return binding;
                }

                binding = new CustomFunctionBinding(
                    graph,
                    $"Function_{SanitizeIdentifier(string.IsNullOrEmpty(node.functionName) ? "Asset" : node.functionName)}_{_customFunctionBindings.Count}",
                    $"FunctionFrame_{SanitizeIdentifier(string.IsNullOrEmpty(node.functionName) ? "Asset" : node.functionName)}_{_customFunctionBindings.Count}",
                    returnType,
                    parameterTypes);
                _customFunctionBindings.Add(key, binding);
                _pendingCustomFunctionMethods.Enqueue(key);
                return binding;
            }

            private bool TryResolveCustomFunction(FlowNode_ExecuteCustomFunction node, out FlowGraph graph,
                out string key, out Type returnType, out Type[] parameterTypes)
            {
                graph = null;
                key = null;
                returnType = typeof(void);
                parameterTypes = Type.EmptyTypes;

                string assetGuid = null;
                if (node.functionAsset)
                {
                    if (!TryRegisterFunctionDependency(node.functionAsset, out assetGuid))
                    {
                        return false;
                    }

                    graph = node.functionAsset.GetFlowGraph();
                }
                else
                {
                    graph = _graph.FindSubGraph<FlowGraph>(node.functionName);
                }

                if (graph == null)
                {
                    return false;
                }

                graph.AOT();
                if (!TryGetCustomFunctionSignature(graph, out returnType, out parameterTypes) ||
                    !DoesCustomFunctionNodeMatchSignature(node, returnType, parameterTypes))
                {
                    return false;
                }

                key = node.functionAsset
                    ? $"asset:{assetGuid}:{string.Join(",", parameterTypes.Select(type => type.FullName))}:{returnType.FullName}"
                    : $"local:{node.functionName}:{string.Join(",", parameterTypes.Select(type => type.FullName))}:{returnType.FullName}";
                return parameterTypes.All(IsVisibleType) && IsVisibleType(returnType);
            }

            private static bool TryGetCustomFunctionSignature(FlowGraph graph, out Type returnType,
                out Type[] parameterTypes)
            {
                returnType = typeof(void);
                parameterTypes = Type.EmptyTypes;

                var input = graph.Events.OfType<CustomFunctionInput>().FirstOrDefault();
                if (input == null)
                {
                    return false;
                }

                parameterTypes = input.parameters?.Select(parameter => parameter.GetParameterType()).ToArray() ??
                                 Type.EmptyTypes;
                var output = graph.nodes.OfType<CustomFunctionOutput>()
                    .FirstOrDefault(node => node.parameter?.hasReturn == true);
                if (output?.parameter?.hasReturn == true)
                {
                    returnType = output.parameter.GetParameterType();
                }

                return true;
            }

            private static bool DoesCustomFunctionNodeMatchSignature(FlowNode_ExecuteCustomFunction node,
                Type returnType, Type[] parameterTypes)
            {
                var genericArguments = GetGenericArguments(node);
                if (returnType == typeof(void))
                {
                    return node is not FlowNode_ExecuteCustomFunctionReturn &&
                           genericArguments.SequenceEqual(parameterTypes);
                }

                return node is FlowNode_ExecuteCustomFunctionReturn &&
                       genericArguments.Length == parameterTypes.Length + 1 &&
                       genericArguments.Take(parameterTypes.Length).SequenceEqual(parameterTypes) &&
                       genericArguments[^1] == returnType;
            }

            private bool TryRegisterFunctionDependency(FlowGraphFunctionAsset asset, out string assetGuid)
            {
                assetGuid = null;
                var assetPath = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(assetPath))
                {
                    return false;
                }

                assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(assetGuid))
                {
                    return false;
                }

                var graphData = ((IFlowGraphContainer)asset).GetFlowGraphData();
                if (graphData == null)
                {
                    return false;
                }

                var graphHash = FlowGeneratedRuntimeUtility.CalculateGraphHash(graphData);
                _functionAssetDependencies[assetGuid] =
                    new FlowGeneratedFunctionDependencyInfo(assetGuid, asset.name, graphHash);
                return true;
            }

            private bool ValidateCustomFunctionGraph(FlowGraph graph, Type returnType, Type[] parameterTypes)
            {
                var previousGraph = _graph;
                var previousNodes = _nodes;
                _graph = graph;
                _nodes = graph.nodes.ToDictionary(node => node.Guid);
                try
                {
                    var input = graph.Events.OfType<CustomFunctionInput>().FirstOrDefault();
                    if (input == null || input.GetPortArrayLength() != parameterTypes.Length)
                    {
                        return false;
                    }

                    foreach (var node in graph.nodes)
                    {
                        if (node is CustomFunctionInput) continue;
                        var context = new NodeGenerationContext(this, null, null, string.Empty);
                        if (!NodeGeneratorFactory.Get().TryGetGenerator(node.GetType(), out var generator) ||
                            !generator.CanGenerate(node, context))
                        {
                            return false;
                        }
                    }

                    return returnType == typeof(void) ||
                           graph.nodes.OfType<CustomFunctionOutput>().Any(output => output.parameter?.hasReturn == true);
                }
                finally
                {
                    _graph = previousGraph;
                    _nodes = previousNodes;
                }
            }

            private void FlushCustomFunctionMethods()
            {
                while (_pendingCustomFunctionMethods.Count > 0)
                {
                    var key = _pendingCustomFunctionMethods.Dequeue();
                    if (!_generatedCustomFunctionMethods.Add(key) ||
                        !_customFunctionBindings.TryGetValue(key, out var binding))
                    {
                        continue;
                    }

                    GenerateCustomFunctionMethod(binding);
                }
            }

            private void GenerateCustomFunctionMethod(CustomFunctionBinding binding)
            {
                var previousGraph = _graph;
                var previousNodes = _nodes;
                var previousReturnEmission = _customFunctionReturnEmission;
                _graph = binding.Graph;
                _nodes = binding.Graph.nodes.ToDictionary(node => node.Guid);
                _customFunctionReturnEmission = binding.ReturnType == typeof(void)
                    ? CustomFunctionReturnEmission.None
                    : CustomFunctionReturnEmission.ReturnMethodValue;
                try
                {
                    var input = binding.Graph.Events.OfType<CustomFunctionInput>().FirstOrDefault();
                    if (input == null)
                    {
                        throw new FlowCSharpRuntimeGenerationException(
                            $"Custom function graph for {binding.MethodName} has no input node.");
                    }

                    BeginFrame(binding.FrameName);
                    GenerateFunctionFrame(binding);
                    var body = CaptureMethodBody(() =>
                    {
                        GenerateForwardConnection(GetExecConnection(input, nameof(CustomFunctionInput.exec)),
                            binding.FrameName, "frame", "                ");
                        Emit(binding.ReturnType == typeof(void)
                            ? "                return;"
                            : "                return frame.ReturnValue;");
                    });
                    body = ResolveFrameArgumentMarkers(body, false);
                    AppendUniTaskMethod(binding.MethodName, binding.FrameName, binding.ReturnType, body,
                        "                frame.Release();");
                    CompleteFrame();
                }
                finally
                {
                    _customFunctionReturnEmission = previousReturnEmission;
                    _graph = previousGraph;
                    _nodes = previousNodes;
                }
            }

            private void GenerateFunctionFrame(CustomFunctionBinding binding)
            {
                _members.AppendLine($"        private sealed class {binding.FrameName}");
                _members.AppendLine("        {");
                _members.AppendLine($"            private static readonly ObjectPool<{binding.FrameName}> Pool = new(() => new {binding.FrameName}());");
                _members.AppendLine("            public UObject ContextObject;");
                _members.AppendLine("            public CancellationToken CancellationToken;");
                for (var i = 0; i < binding.ParameterTypes.Length; i++)
                {
                    _members.AppendLine($"            public {GetFriendlyTypeName(binding.ParameterTypes[i])} Arg{i + 1};");
                }
                if (binding.ReturnType != typeof(void))
                {
                    _members.AppendLine($"            public {GetFriendlyTypeName(binding.ReturnType)} ReturnValue;");
                }
                _members.AppendLine(_frameFieldMarker);
                _members.AppendLine();
                var parameters = new List<string>
                {
                    "UObject contextObject",
                    "CancellationToken cancellationToken"
                };
                for (var i = 0; i < binding.ParameterTypes.Length; i++)
                {
                    parameters.Add($"{GetFriendlyTypeName(binding.ParameterTypes[i])} arg{i + 1}");
                }
                _members.AppendLine($"            public static {binding.FrameName} Get({string.Join(", ", parameters)})");
                _members.AppendLine("            {");
                _members.AppendLine("                var frame = Pool.Get();");
                _members.AppendLine("                frame.ContextObject = contextObject;");
                _members.AppendLine("                frame.CancellationToken = cancellationToken;");
                for (var i = 0; i < binding.ParameterTypes.Length; i++)
                {
                    _members.AppendLine($"                frame.Arg{i + 1} = arg{i + 1};");
                }
                _members.AppendLine("                return frame;");
                _members.AppendLine("            }");
                _members.AppendLine();
                _members.AppendLine("            public void Release()");
                _members.AppendLine("            {");
                for (var i = 0; i < binding.ParameterTypes.Length; i++)
                {
                    _members.AppendLine($"                Arg{i + 1} = default;");
                }
                if (binding.ReturnType != typeof(void))
                {
                    _members.AppendLine("                ReturnValue = default;");
                }
                _members.AppendLine(_frameReleaseMarker);
                _members.AppendLine("                ContextObject = null;");
                _members.AppendLine("                CancellationToken = default;");
                _members.AppendLine("                Pool.Release(this);");
                _members.AppendLine("            }");
                _members.AppendLine("        }");
                _members.AppendLine();
            }

            private string BuildFunctionCallExpression(FlowNode_ExecuteFunction node, DirectCallInfo directCall,
                string frameTypeName, string frameVar, string indent)
            {
                var useDirectSerializedType =
                    TryGetDirectSerializedTypeArgument(node, directCall, out var directSerializedTypeArgument) &&
                    CanBuildDirectSerializedTypeFunctionExpression(directCall, directSerializedTypeArgument);
                var arguments = new List<string>();
                for (var i = 0; i < directCall.ParameterTypes.Length; i++)
                {
                    if (useDirectSerializedType && i == directSerializedTypeArgument.ParameterIndex)
                    {
                        arguments.Add(null);
                        continue;
                    }

                    var expression = GetValueExpression(node, $"input{i + 1}", directCall.ParameterTypes[i],
                        frameTypeName, frameVar, indent);
                    if (node.isStatic && node.isSelfTarget && i == 0)
                    {
                        expression = BuildSelfTargetArgumentExpression(node, $"input{i + 1}",
                            directCall.ParameterTypes[i], expression, frameVar);
                    }

                    arguments.Add($"({GetFriendlyTypeName(directCall.ParameterTypes[i])})({expression})");
                }

                if (useDirectSerializedType &&
                    TryBuildDirectSerializedTypeFunctionExpression(directCall, directSerializedTypeArgument,
                        arguments, out var directSerializedTypeExpression))
                {
                    return directSerializedTypeExpression;
                }

                if (!directCall.UseInvoker && node.isStatic &&
                    TryBuildIntrinsicFunctionExpression(directCall, arguments, out var intrinsicExpression))
                {
                    return intrinsicExpression;
                }

                if (!directCall.UseInvoker && node.isStatic)
                {
                    return $"{GetFriendlyTypeName(directCall.DeclaringType)}.{EscapeIdentifier(directCall.MethodName)}({string.Join(", ", arguments)})";
                }

                var targetExpression = node.isStatic
                    ? $"default({GetFriendlyTypeName(directCall.TargetType)})"
                    : GetValueExpression(node, "target", directCall.TargetType, frameTypeName, frameVar, indent);
                if (!node.isStatic)
                {
                    targetExpression = BuildTargetOrDefaultExpression(node, "target", directCall.TargetType,
                        node.isSelfTarget, false, targetExpression, frameVar);
                }

                if (!directCall.UseInvoker)
                {
                    return $"{targetExpression}.{EscapeIdentifier(directCall.MethodName)}({string.Join(", ", arguments)})";
                }

                var invokerField = GetFunctionInvokerField(node, directCall);
                var argumentList = arguments.Count == 0
                    ? targetExpression
                    : $"{targetExpression}, {string.Join(", ", arguments)}";
                if (directCall.ReturnType == typeof(void))
                {
                    return directCall.ParameterTypes.Length == 0
                        ? $"{invokerField}.Invoke({argumentList})"
                        : $"{invokerField}.Invoke<{string.Join(", ", directCall.ParameterTypes.Select(GetFriendlyTypeName))}>({argumentList})";
                }

                var genericTypes = directCall.ParameterTypes.Concat(new[] { directCall.ReturnType });
                return $"{invokerField}.Invoke<{string.Join(", ", genericTypes.Select(GetFriendlyTypeName))}>({argumentList})";
            }

            private bool TryGetDirectSerializedTypeArgument(FlowNode_ExecuteFunction node, DirectCallInfo directCall,
                out DirectSerializedTypeArgument argument)
            {
                argument = default;
                if (_serializedTypeMode != FlowGeneratedRuntimeSerializedTypeMode.DirectType ||
                    directCall.UseInvoker ||
                    !TryGetResolveReturnParameterIndex(directCall, out var parameterIndex) ||
                    parameterIndex < 0 ||
                    parameterIndex >= directCall.ParameterTypes.Length ||
                    !IsSerializedType(directCall.ParameterTypes[parameterIndex]) ||
                    !TryGetSerializedTypeConstraint(directCall.ParameterTypes[parameterIndex], out var constraintType))
                {
                    return false;
                }

                var portData = GetFunctionParameterPortData(node, parameterIndex);
                if (portData?.connections?.Any(connection => !connection.isFlattened) == true)
                {
                    return false;
                }

                if (portData?.GetPort(node)?.GetValue() is not SerializedTypeBase serializedType)
                {
                    return false;
                }

                var selectedType = serializedType.GetObjectType();
                if (selectedType == null ||
                    !selectedType.IsAssignableTo(constraintType) ||
                    !IsVisibleType(selectedType))
                {
                    return false;
                }

                argument = new DirectSerializedTypeArgument(parameterIndex, selectedType);
                return true;
            }

            private static bool TryGetSerializedTypeConstraint(Type type, out Type constraintType)
            {
                constraintType = null;
                while (type != null)
                {
                    if (type.IsGenericType &&
                        type.GetGenericTypeDefinition() == typeof(SerializedType<>))
                    {
                        constraintType = type.GetGenericArguments()[0];
                        return true;
                    }

                    type = type.BaseType;
                }

                return false;
            }

            private static bool CanBuildDirectSerializedTypeFunctionExpression(DirectCallInfo directCall,
                DirectSerializedTypeArgument typeArgument)
            {
                if (directCall.DeclaringType == typeof(UnityExecutableLibrary))
                {
                    return directCall.MethodName is
                        "Flow_FindObjectOfType" or
                        "Flow_GameObjectGetComponent" or
                        "Flow_GameObjectGetComponentInChildren" or
                        "Flow_GameObjectGetComponentInParent" or
                        "Flow_GameObjectGetComponents" or
                        "Flow_GameObjectGetComponentsInChildren" or
                        "Flow_GameObjectGetComponentsInParent" or
                        "Flow_GameObjectAddComponent" or
                        "Flow_GameObjectGetOrAddComponent" or
                        "Flow_ComponentGetComponent" or
                        "Flow_ComponentGetComponentInChildren" or
                        "Flow_ComponentGetComponentInParent" or
                        "Flow_ComponentGetComponents" or
                        "Flow_ComponentGetComponentsInChildren" or
                        "Flow_ComponentGetComponentsInParent";
                }

                if (directCall.DeclaringType == typeof(DataDrivenExecutableLibrary))
                {
                    return directCall.MethodName switch
                    {
                        "Flow_GetDataTableManager" => true,
                        "Flow_DataTableGetRow" or "Flow_DataTableGetRowByIndex" =>
                            !typeArgument.SelectedType.IsValueType,
                        _ => false
                    };
                }

                return false;
            }

            private static bool TryBuildDirectSerializedTypeFunctionExpression(DirectCallInfo directCall,
                DirectSerializedTypeArgument typeArgument, IReadOnlyList<string> arguments, out string expression)
            {
                expression = null;
                var typeName = GetFriendlyTypeName(typeArgument.SelectedType);

                if (directCall.DeclaringType == typeof(UnityExecutableLibrary))
                {
                    expression = directCall.MethodName switch
                    {
                        "Flow_FindObjectOfType" =>
                            $"FlowGeneratedRuntimeUtility.FindObjectOfType<{typeName}>()",
                        "Flow_GameObjectGetComponent" =>
                            $"{arguments[0]}.GetComponent<{typeName}>()",
                        "Flow_GameObjectGetComponentInChildren" =>
                            $"{arguments[0]}.GetComponentInChildren<{typeName}>()",
                        "Flow_GameObjectGetComponentInParent" =>
                            $"{arguments[0]}.GetComponentInParent<{typeName}>()",
                        "Flow_GameObjectGetComponents" =>
                            $"{arguments[0]}.GetComponents<{typeName}>()",
                        "Flow_GameObjectGetComponentsInChildren" =>
                            $"{arguments[0]}.GetComponentsInChildren<{typeName}>()",
                        "Flow_GameObjectGetComponentsInParent" =>
                            $"{arguments[0]}.GetComponentsInParent<{typeName}>()",
                        "Flow_GameObjectAddComponent" =>
                            $"{arguments[0]}.AddComponent<{typeName}>()",
                        "Flow_GameObjectGetOrAddComponent" =>
                            $"FlowGeneratedRuntimeUtility.GetOrAddComponent<{typeName}>({arguments[0]})",
                        "Flow_ComponentGetComponent" =>
                            $"{arguments[0]}.GetComponent<{typeName}>()",
                        "Flow_ComponentGetComponentInChildren" =>
                            $"{arguments[0]}.GetComponentInChildren<{typeName}>()",
                        "Flow_ComponentGetComponentInParent" =>
                            $"{arguments[0]}.GetComponentInParent<{typeName}>()",
                        "Flow_ComponentGetComponents" =>
                            $"{arguments[0]}.GetComponents<{typeName}>()",
                        "Flow_ComponentGetComponentsInChildren" =>
                            $"{arguments[0]}.GetComponentsInChildren<{typeName}>()",
                        "Flow_ComponentGetComponentsInParent" =>
                            $"{arguments[0]}.GetComponentsInParent<{typeName}>()",
                        _ => null
                    };
                    return expression != null;
                }

                if (directCall.DeclaringType == typeof(DataDrivenExecutableLibrary))
                {
                    expression = directCall.MethodName switch
                    {
                        "Flow_GetDataTableManager" =>
                            $"Chris.DataDriven.DataTableManager.GetOrCreateDataTableManager(typeof({typeName}))",
                        "Flow_DataTableGetRow" =>
                            $"{arguments[0]}.GetRow<{typeName}>({arguments[1]})",
                        "Flow_DataTableGetRowByIndex" =>
                            $"{arguments[0]}.GetRow<{typeName}>({arguments[1]})",
                        _ => null
                    };
                    return expression != null;
                }

                return false;
            }

            private static bool TryBuildIntrinsicFunctionExpression(DirectCallInfo directCall,
                IReadOnlyList<string> arguments, out string expression)
            {
                expression = null;
                if (directCall.DeclaringType != typeof(MathExecutableLibrary))
                {
                    return false;
                }

                string Unary(string op) => $"({op}({arguments[0]}))";
                string Binary(string op) => $"(({arguments[0]}) {op} ({arguments[1]}))";
                string Call(string owner, string method) => $"{owner}.{method}({string.Join(", ", arguments)})";

                expression = directCall.MethodName switch
                {
                    _ when TryBuildUnityTimeExpression(directCall, out var unityTimeExpression) => unityTimeExpression,
                    "Flow_FloatAdd" => Binary("+"),
                    "Flow_FloatSubtract" => Binary("-"),
                    "Flow_FloatMultiply" => Binary("*"),
                    "Flow_FloatDivide" => Binary("/"),
                    "Flow_FloatModulo" => Binary("%"),
                    "Flow_FloatLessThan" => Binary("<"),
                    "Flow_FloatLessThanOrEqualTo" => Binary("<="),
                    "Flow_FloatGreaterThan" => Binary(">"),
                    "Flow_FloatGreaterThanOrEqualTo" => Binary(">="),
                    "Flow_FloatToInt" => $"((int)({arguments[0]}))",
                    "Flow_FloatPow" => Call("Mathf", "Pow"),
                    "Flow_FloatSqrt" => Call("Mathf", "Sqrt"),
                    "Flow_FloatExp" => Call("Mathf", "Exp"),
                    "Flow_FloatAbs" => Call("Mathf", "Abs"),
                    "Flow_FloatClamp" => Call("Mathf", "Clamp"),
                    "Flow_FloatClamp01" => Call("Mathf", "Clamp01"),
                    "Flow_FloatLerp" => Call("Mathf", "Lerp"),
                    "Flow_FloatInverseLerp" => Call("Mathf", "InverseLerp"),
                    "Flow_FloatMin" => Call("Mathf", "Min"),
                    "Flow_FloatMax" => Call("Mathf", "Max"),
                    "Flow_FloatFloorToInt" => Call("Mathf", "FloorToInt"),
                    "Flow_FloatCeilToInt" => Call("Mathf", "CeilToInt"),
                    "Flow_FloatRoundToInt" => Call("Mathf", "RoundToInt"),
                    "Flow_FloatSin" => Call("Mathf", "Sin"),
                    "Flow_FloatCos" => Call("Mathf", "Cos"),
                    "Flow_FloatTan" => Call("Mathf", "Tan"),
                    "Flow_FloatAtan2" => Call("Mathf", "Atan2"),
                    "Flow_FloatApproximately" => Call("Mathf", "Approximately"),
                    "Flow_FloatSign" => Call("Mathf", "Sign"),
                    "Flow_FloatDeg2Rad" => $"(({arguments[0]}) * Mathf.Deg2Rad)",
                    "Flow_FloatRad2Deg" => $"(({arguments[0]}) * Mathf.Rad2Deg)",
                    "Flow_IntAdd" => Binary("+"),
                    "Flow_IntSubtract" => Binary("-"),
                    "Flow_IntMultiply" => Binary("*"),
                    "Flow_IntDivide" => Binary("/"),
                    "Flow_IntModulo" => Binary("%"),
                    "Flow_IntLessThan" => Binary("<"),
                    "Flow_IntLessThanOrEqualTo" => Binary("<="),
                    "Flow_IntGreaterThan" => Binary(">"),
                    "Flow_IntGreaterThanOrEqualTo" => Binary(">="),
                    "Flow_IntToFloat" => $"((float)({arguments[0]}))",
                    "Flow_IntAbs" => Call("Mathf", "Abs"),
                    "Flow_IntClamp" => Call("Mathf", "Clamp"),
                    "Flow_IntMin" => Call("Mathf", "Min"),
                    "Flow_IntMax" => Call("Mathf", "Max"),
                    "Flow_BoolInvert" => Unary("!"),
                    "Flow_BoolAnd" => Binary("&"),
                    "Flow_BoolOr" => Binary("|"),
                    "Flow_BoolXor" => Binary("^"),
                    "Flow_Vector2" => $"new Vector2({arguments[0]}, {arguments[1]})",
                    "Flow_Vector3" => $"new Vector3({arguments[0]}, {arguments[1]}, {arguments[2]})",
                    "Flow_Vector3Add" => Binary("+"),
                    "Flow_Vector3Subtract" => Binary("-"),
                    "Flow_Vector3MultiplyFloat" => Binary("*"),
                    "Flow_Vector3DivideFloat" => Binary("/"),
                    _ => null
                };

                return expression != null;
            }

            private static bool TryBuildUnityTimeExpression(DirectCallInfo directCall, out string expression)
            {
                expression = null;
                if (directCall.DeclaringType != typeof(UnityExecutableLibrary) ||
                    directCall.ParameterTypes.Length != 0)
                {
                    return false;
                }

                expression = directCall.MethodName switch
                {
                    "Flow_TimeGetTime" => "Time.time",
                    "Flow_TimeGetUnscaledTime" => "Time.unscaledTime",
                    "Flow_TimeGetDeltaTime" => "Time.deltaTime",
                    "Flow_TimeGetFixedDeltaTime" => "Time.fixedDeltaTime",
                    "Flow_TimeGetFrameCount" => "Time.frameCount",
                    "Flow_TimeGetRealtimeSinceStartup" => "Time.realtimeSinceStartup",
                    "Flow_TimeGetTimeScale" => "Time.timeScale",
                    _ => null
                };

                return expression != null;
            }

            internal void GenerateSetProperty(PropertyNode_PropertyValue node, string frameTypeName, string frameVar,
                string indent)
            {
                if (!CanDirectSetProperty(node, out var propertyCall))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Property setter node {node.Guid} can not be generated as a direct call.");
                }

                var value = GetValueExpression(node, "inputValue", propertyCall.PropertyType, frameTypeName, frameVar,
                    indent);
                var targetExpression = BuildPropertyTargetExpression(node, propertyCall, frameTypeName, frameVar,
                    indent);
                Emit($"{indent}{targetExpression}.{EscapeIdentifier(propertyCall.PropertyName)} = {value};");
                GenerateNext(node, frameTypeName, frameVar, indent);
            }

            internal void GenerateSetSharedVariable(PropertyNode_SharedVariableValue node, string frameTypeName,
                string frameVar, string indent)
            {
                if (!CanAccessSharedVariable(node, out var variableType, out var variableValueType,
                        out var portValueType))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Shared variable setter node {node.Guid} can not be generated.");
                }

                var binding = GetOrCreateGraphVariableBinding(node.propertyName, variableType, variableValueType);
                var value = GetValueExpression(node, "inputValue", portValueType, frameTypeName, frameVar, indent);
                if (binding.IsLocal)
                {
                    Emit($"{indent}{binding.FieldName} = {CastExpression(value, portValueType, binding.StorageType)};");
                }
                else
                {
                    Emit($"{indent}if ({binding.FieldName} != null)");
                    Emit($"{indent}{{");
                    Emit($"{indent}    {binding.FieldName}.Value = {value};");
                    Emit($"{indent}}}");
                }
                GenerateNext(node, frameTypeName, frameVar, indent);
            }

            private GraphVariableBinding GetOrCreateGraphVariableBinding(string variableName, Type variableType,
                Type variableValueType)
            {
                if (TryCreateLocalGraphVariableBinding(variableName, variableType, variableValueType,
                        out var localBinding))
                {
                    return GraphVariableBinding.FromLocal(localBinding);
                }

                return GraphVariableBinding.FromShared(GetOrCreateSharedVariableBinding(variableName, variableType));
            }

            private bool TryCreateLocalGraphVariableBinding(string variableName, Type variableType,
                Type variableValueType, out LocalVariableBinding binding)
            {
                binding = default;
                if (_variableStorageMode != FlowGeneratedRuntimeVariableStorageMode.LocalFieldsForUnshared ||
                    !TryFindGraphVariable(variableName, variableType, out var variable) ||
                    variable.IsShared ||
                    variable.IsGlobal ||
                    !TryGetLocalVariableStorageType(variable, variableValueType, out var storageType))
                {
                    return false;
                }

                var key = $"graph:{variableType.FullName}:{variableName}";
                if (_localVariableBindings.TryGetValue(key, out binding))
                {
                    return true;
                }

                binding = new LocalVariableBinding(
                    $"_local_{SanitizeIdentifier(variableName)}_{_localVariableBindings.Count}",
                    storageType,
                    $"FlowGeneratedRuntimeUtility.GetLocalVariableValue<{GetFriendlyTypeName(variableType)}, {GetFriendlyTypeName(storageType)}>(graphData, \"{Escape(variableName)}\")");
                _localVariableBindings.Add(key, binding);
                return true;
            }

            private bool TryFindGraphVariable(string variableName, Type variableType, out SharedVariable variable)
            {
                variable = null;
                if (string.IsNullOrEmpty(variableName) || _graph.variables == null)
                {
                    return false;
                }

                foreach (var candidate in _graph.variables)
                {
                    if (candidate == null ||
                        !string.Equals(candidate.Name, variableName, StringComparison.Ordinal) ||
                        !variableType.IsInstanceOfType(candidate))
                    {
                        continue;
                    }

                    variable = candidate;
                    return true;
                }

                return false;
            }

            private static bool TryGetLocalVariableStorageType(SharedVariable variable, Type variableValueType,
                out Type storageType)
            {
                storageType = null;
                if (variable == null || variableValueType == null || !IsVisibleType(variableValueType))
                {
                    return false;
                }

                var resolvedType = variable.GetValueType();
                if (resolvedType != null &&
                    resolvedType != typeof(object) &&
                    resolvedType.IsAssignableTo(variableValueType) &&
                    IsVisibleType(resolvedType))
                {
                    storageType = resolvedType;
                    return true;
                }

                storageType = variableValueType;
                return true;
            }

            private SharedVariableBinding GetOrCreateSharedVariableBinding(string variableName, Type variableType)
            {
                var key = $"{variableType.FullName}:{variableName}";
                if (_sharedVariableBindings.TryGetValue(key, out var binding))
                {
                    return binding;
                }

                binding = new SharedVariableBinding(variableName, variableType,
                    $"_shared_{SanitizeIdentifier(variableName)}_{_sharedVariableBindings.Count}");
                _sharedVariableBindings.Add(key, binding);
                return binding;
            }

            private string GetFunctionInvokerField(FlowNode_ExecuteFunction node, DirectCallInfo callInfo)
            {
                return GetOrCreateFunctionInvokerBinding(node, callInfo).FieldName;
            }

            private FunctionInvokerBinding GetOrCreateFunctionInvokerBinding(FlowNode_ExecuteFunction node,
                DirectCallInfo callInfo)
            {
                var key =
                    $"{callInfo.TargetType.FullName}:{node.isStatic}:{callInfo.MethodName}:{callInfo.ReturnType.FullName}:{string.Join(",", callInfo.ParameterTypes.Select(type => type.FullName))}";
                if (_functionInvokerBindings.TryGetValue(key, out var binding))
                {
                    return binding;
                }

                binding = new FunctionInvokerBinding(
                    $"_invoker_{SanitizeIdentifier(callInfo.MethodName)}_{_functionInvokerBindings.Count}",
                    callInfo.TargetType,
                    callInfo.MethodName,
                    callInfo.ReturnType,
                    callInfo.ParameterTypes,
                    node.isStatic);
                _functionInvokerBindings.Add(key, binding);
                return binding;
            }

            private static string BuildPrewarmCall(FunctionInvokerBinding binding)
            {
                if (binding.ReturnType == typeof(void))
                {
                    return binding.ParameterTypes.Length == 0
                        ? "Prewarm()"
                        : $"Prewarm<{string.Join(", ", binding.ParameterTypes.Select(GetFriendlyTypeName))}>()";
                }

                var genericTypes = binding.ParameterTypes.Concat(new[] { binding.ReturnType });
                return $"Prewarm<{string.Join(", ", genericTypes.Select(GetFriendlyTypeName))}>()";
            }

            private string BuildPropertyTargetExpression(PropertyNode_PropertyValue node, PropertyCallInfo propertyCall,
                string frameTypeName, string frameVar, string indent)
            {
                if (node.isStatic)
                {
                    return GetFriendlyTypeName(propertyCall.DeclaringType);
                }

                var targetExpression = GetValueExpression(node, "target", propertyCall.TargetType, frameTypeName,
                    frameVar, indent);
                return BuildTargetOrDefaultExpression(node, "target", propertyCall.TargetType,
                    node.isSelfTarget, false, targetExpression, frameVar);
            }

            private string BuildSelfTargetArgumentExpression(FlowNode_ExecuteFunction node, string propertyName,
                Type targetType, string inputExpression, string frameVar)
            {
                return BuildTargetOrDefaultExpression(node, propertyName, targetType, true, true,
                    inputExpression, frameVar);
            }

            private string BuildTargetOrDefaultExpression(CeresNode node, string propertyName, Type targetType,
                bool isSelfTarget, bool useSelfTargetUtility, string inputExpression, string frameVar)
            {
                if (!isSelfTarget)
                {
                    return inputExpression;
                }

                if (CanUseContextSelfTarget(node, propertyName, -1, targetType))
                {
                    return $"{frameVar}.ContextObject as {GetFriendlyTypeName(targetType)}";
                }

                var typeName = GetFriendlyTypeName(targetType);
                return useSelfTargetUtility
                    ? $"FlowGeneratedRuntimeUtility.GetSelfTargetOrDefault<{typeName}>(true, {inputExpression}, {frameVar}.ContextObject)"
                    : $"FlowGeneratedRuntimeUtility.GetTargetOrDefault<{typeName}>(false, true, {inputExpression}, {frameVar}.ContextObject)";
            }

            private bool CanUseContextSelfTarget(CeresNode node, string propertyName, int arrayIndex, Type targetType)
            {
                return CanCastContextAs(targetType) &&
                       IsUnconnectedNullInput(node, propertyName, arrayIndex);
            }

            private bool IsUnconnectedNullInput(CeresNode node, string propertyName, int arrayIndex)
            {
                if (TryFindValueConnection(node, propertyName, arrayIndex, out _, out _, out _))
                {
                    return false;
                }

                var portData = arrayIndex < 0
                    ? node.NodeData.FindPortData(propertyName)
                    : node.NodeData.FindPortData(propertyName, arrayIndex);
                var value = portData?.GetPort(node)?.GetValue();
                return IsNullLiteralValue(value);
            }

            private static bool CanCastContextAs(Type targetType)
            {
                return targetType != null &&
                       !targetType.IsValueType &&
                       (targetType.IsInterface ||
                        targetType.IsAssignableFrom(typeof(UObject)) ||
                        typeof(UObject).IsAssignableFrom(targetType));
            }

            internal void GenerateDefaultNext(FlowNode node, string frameTypeName, string frameVar, string indent)
            {
                GenerateNext(node, frameTypeName, frameVar, indent);
            }

            internal void GenerateNext(CeresNode node, string frameTypeName, string frameVar, string indent)
            {
                GenerateForwardConnection(GetExecConnection(node, "exec"), frameTypeName, frameVar, indent);
            }

            internal CeresNode GetExecTarget(CeresNode node, string propertyName)
            {
                return GetExecConnection(node, propertyName).Node;
            }

            internal ExecConnection GetExecConnection(CeresNode node, string propertyName)
            {
                var portData = node.NodeData.FindPortData(propertyName);
                var connection = portData?.connections?.FirstOrDefault(connection => !connection.isFlattened);
                if (connection == null) return default;
                return new ExecConnection(FindNode(connection.nodeId), connection.portId, connection.portIndex);
            }

            internal CeresNode GetExecTarget(CeresNode node, string propertyName, int arrayIndex)
            {
                return GetExecConnection(node, propertyName, arrayIndex).Node;
            }

            internal ExecConnection GetExecConnection(CeresNode node, string propertyName, int arrayIndex)
            {
                var portData = node.NodeData.FindPortData(propertyName, arrayIndex);
                var connection = portData?.connections?.FirstOrDefault(connection => !connection.isFlattened);
                if (connection == null) return default;
                return new ExecConnection(FindNode(connection.nodeId), connection.portId, connection.portIndex);
            }

            internal IEnumerable<CeresNode> GetExecTargets(CeresNode node, string propertyName)
            {
                return node.NodeData.portData
                    .Where(port => port.propertyName == propertyName)
                    .OrderBy(port => port.arrayIndex)
                    .SelectMany(port => port.connections ?? Array.Empty<PortConnectionData>())
                    .Where(connection => !connection.isFlattened)
                    .Select(connection => FindNode(connection.nodeId))
                    .Where(target => target != null);
            }

            internal IEnumerable<ExecConnection> GetExecConnections(CeresNode node, string propertyName)
            {
                return node.NodeData.portData
                    .Where(port => port.propertyName == propertyName)
                    .OrderBy(port => port.arrayIndex)
                    .SelectMany(port => port.connections ?? Array.Empty<PortConnectionData>())
                    .Where(connection => !connection.isFlattened)
                    .Select(connection => new ExecConnection(FindNode(connection.nodeId), connection.portId,
                        connection.portIndex))
                    .Where(target => target.Node != null);
            }

            internal string GetValueExpression(CeresNode node, string propertyName, Type expectedType,
                string frameTypeName, string frameVar, string indent)
            {
                return GetValueExpression(node, propertyName, -1, expectedType, frameTypeName, frameVar, indent);
            }

            internal string GetValueExpression(CeresNode node, string propertyName, int arrayIndex, Type expectedType,
                string frameTypeName, string frameVar, string indent)
            {
                var portData = arrayIndex < 0
                    ? node.NodeData.FindPortData(propertyName)
                    : node.NodeData.FindPortData(propertyName, arrayIndex);
                if (TryFindValueConnection(node, propertyName, arrayIndex, out var source, out var sourcePortId,
                        out var sourcePortIndex))
                {
                    if (source is ExecutableEvent)
                    {
                        return GetExecutableEventOutputExpression((ExecutableEvent)source, sourcePortId,
                            sourcePortIndex, expectedType, frameVar);
                    }

                    if (source != null &&
                        TryBuildInlineOutputExpression(source, sourcePortId, expectedType, frameTypeName,
                            frameVar, indent, out var inlineExpression))
                    {
                        return inlineExpression;
                    }

                    if (source == null || !TryGetOutputSlot(source, sourcePortId, out var outputType,
                            out var slotField))
                    {
                        throw new FlowCSharpRuntimeGenerationException(
                            $"Data connection {source?.GetType().Name}.{sourcePortId} -> {node.GetType().Name}.{propertyName} is not supported by generated runtime.");
                    }

                    if (!IsForwardOutputSlotAlreadyAssigned(source, sourcePortId))
                    {
                        GenerateDependencyCall(source, frameTypeName, frameVar, indent);
                    }

                    return CastExpression($"{frameVar}.{slotField}", outputType, expectedType);
                }

                var port = portData?.GetPort(node);
                var value = port?.GetValue();
                return ToLiteral(value, expectedType);
            }

            private bool TryFindValueConnection(CeresNode target, string propertyName, int arrayIndex,
                out CeresNode source, out string sourcePortId, out int sourcePortIndex)
            {
                source = null;
                sourcePortId = null;
                sourcePortIndex = -1;
                var portData = arrayIndex < 0
                    ? target.NodeData.FindPortData(propertyName)
                    : target.NodeData.FindPortData(propertyName, arrayIndex);
                var directConnection = portData?.connections?.FirstOrDefault(connection => !connection.isFlattened);
                if (directConnection != null)
                {
                    source = FindNode(directConnection.nodeId);
                    sourcePortId = directConnection.portId;
                    sourcePortIndex = directConnection.portIndex;
                    return source != null;
                }

                foreach (var candidate in _graph.nodes)
                {
                    foreach (var candidatePort in candidate.NodeData.portData)
                    {
                        foreach (var connection in candidatePort.connections ?? Array.Empty<PortConnectionData>())
                        {
                            if (connection.isFlattened ||
                                connection.nodeId != target.Guid ||
                                connection.portId != propertyName ||
                                (arrayIndex >= 0 && connection.portIndex != arrayIndex))
                            {
                                continue;
                            }

                            source = candidate;
                            sourcePortId = candidatePort.propertyName;
                            sourcePortIndex = candidatePort.arrayIndex;
                            return true;
                        }
                    }
                }

                return false;
            }

            private string GetExecutableEventOutputExpression(ExecutableEvent source, string portId, int portIndex,
                Type expectedType, string frameVar)
            {
                if (portId == "eventDelegate")
                {
                    return CastExpression(GetEventDelegateExpression(source), expectedType);
                }

                if (source is CustomFunctionInput)
                {
                    return CastExpression($"{frameVar}.Arg{portIndex + 1}", expectedType);
                }

                if (source is CustomExecutionEvent)
                {
                    return CastExpression(GetCustomEventOutputExpression(source, portId, frameVar), expectedType);
                }

                if (source is ExecutionEventUber && portId == "outputs")
                {
                    return CastExpression($"{frameVar}.Arg{portIndex + 1}", expectedType);
                }

                return CastExpression(GetEventOutputExpression(portId, frameVar), expectedType);
            }

            private bool TryBuildInlineOutputExpression(CeresNode source, string sourcePortId, Type expectedType,
                string frameTypeName, string frameVar, string indent, out string expression)
            {
                expression = null;
                if (!IsSingleValueOutputConnection(source, sourcePortId) ||
                    !NodeGeneratorFactory.Get().TryGetGenerator(source.GetType(), out var generator) ||
                    generator is not IInlineExpressionNodeGenerator inlineGenerator)
                {
                    return false;
                }

                var context = new NodeGenerationContext(this, frameTypeName, frameVar, indent);
                if (!inlineGenerator.TryGenerateOutputExpression(source, sourcePortId, context, out var outputType,
                        out var inlineExpression))
                {
                    return false;
                }

                expression = CastExpression(inlineExpression, outputType, expectedType);
                return true;
            }

            private bool IsSingleValueOutputConnection(CeresNode source, string sourcePortId)
            {
                var count = 0;
                foreach (var node in _graph.nodes)
                {
                    foreach (var portData in node.NodeData.portData)
                    {
                        foreach (var connection in portData.connections ?? Array.Empty<PortConnectionData>())
                        {
                            if (connection.isFlattened) continue;
                            if (node == source && portData.propertyName == sourcePortId)
                            {
                                count++;
                            }
                            else if (connection.nodeId == source.Guid && connection.portId == sourcePortId)
                            {
                                count++;
                            }

                            if (count > 1)
                            {
                                return false;
                            }
                        }
                    }
                }

                return count == 1;
            }

            private static bool IsForwardOutputSlotAlreadyAssigned(CeresNode source, string portId)
            {
                if (NodeGeneratorFactory.Get().TryGetGenerator(source.GetType(), out var generator) &&
                    generator is IForwardOutputSlotNodeGenerator forwardOutputSlotNodeGenerator)
                {
                    return forwardOutputSlotNodeGenerator.IsForwardOutputSlot(source, portId);
                }

                return false;
            }

            internal void GenerateDependencyCall(CeresNode node, string frameTypeName, string frameVar, string indent,
                DependencyCancellationCheck cancellationCheck = DependencyCancellationCheck.EmitIfNodeWillRun)
            {
                var methodName = GetDependencyMethodName(node);
                var executedField = EnsureFrameField($"executed:{node.Guid}", typeof(bool),
                    $"Executed_{SafeGuid(node.Guid)}");
                if (cancellationCheck == DependencyCancellationCheck.EmitIfNodeWillRun &&
                    ShouldEmitPerNodeCancellationChecks())
                {
                    Emit($"{indent}if (!{frameVar}.{executedField})");
                    Emit($"{indent}{{");
                    Emit($"{indent}    {GetCancellationCheckExpression(frameTypeName, frameVar)};");
                    Emit($"{indent}}}");
                }
                Emit($"{indent}{methodName}({GetFrameArgumentPrefix(frameTypeName)}{frameVar});");
                if (_generatedDependencyMethods.Add(node.Guid))
                {
                    _pendingDependencyMethods.Enqueue(node);
                }
            }

            private string GetFrameArgumentPrefix(string frameTypeName)
            {
                return frameTypeName == _currentFrameName ? FrameArgumentMarker : string.Empty;
            }

            private void FlushDependencyMethods()
            {
                while (_pendingDependencyMethods.Count > 0)
                {
                    GenerateDependencyMethod(_pendingDependencyMethods.Dequeue());
                }
            }

            private void GenerateDependencyMethod(CeresNode node)
            {
                var previousBody = _currentBody;
                _currentBody = _deferredMembers;
                var methodName = GetDependencyMethodName(node);
                var executedField = EnsureFrameField($"executed:{node.Guid}", typeof(bool),
                    $"Executed_{SafeGuid(node.Guid)}");

                _deferredMembers.AppendLine($"        private void {methodName}({(_currentFramePassByReference ? "ref " : string.Empty)}{_currentFrameName} frame)");
                _deferredMembers.AppendLine("        {");
                Emit($"            if (frame.{executedField})");
                Emit("            {");
                Emit("                return;");
                Emit("            }");
                Emit($"            frame.{executedField} = true;");

                if (ShouldExpandTransitiveDependencyPath())
                {
                    foreach (var dependency in GetDependencyNodes(node))
                    {
                        GenerateDependencyCall(dependency, _currentFrameName, "frame", "            ");
                    }
                }

                GenerateDependencyLocal(node, _currentFrameName, "frame", "            ");
                _deferredMembers.AppendLine("        }");
                _deferredMembers.AppendLine();
                _currentBody = previousBody;
            }

            private void GenerateDependencyLocal(CeresNode node, string frameTypeName, string frameVar, string indent)
            {
                var context = new NodeGenerationContext(this, frameTypeName, frameVar, indent);
                if (!NodeGeneratorFactory.Get().TryGetGenerator(node.GetType(), out var generator) ||
                    !generator.CanGenerate(node, context))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Dependency node {node.GetType().Name} ({node.Guid}) is not supported by generated runtime.");
                }

                generator.GenerateDependency(node, context);
            }

            internal bool CanGenerateExecuteFunctionVoid(FlowNode_ExecuteFunctionVoid node)
            {
                return CanGeneratedCall(node, out var directCall) && directCall.ReturnType == typeof(void);
            }

            internal bool CanGenerateExecuteFunctionReturn(FlowNode_ExecuteFunctionReturn node)
            {
                return CanGeneratedCall(node, out var directCall) && directCall.ReturnType != typeof(void);
            }

            internal bool CanGenerateGetProperty(PropertyNode_PropertyValue node)
            {
                return IsGenericInstance(node, typeof(PropertyNode_GetPropertyTValue<,>)) &&
                       CanDirectGetProperty(node, out _);
            }

            internal bool CanGenerateGetSelfReference(PropertyNode node)
            {
                return TryGetSelfReferenceTargetType(node, out _);
            }

            internal bool CanGenerateSetProperty(PropertyNode_PropertyValue node)
            {
                return IsGenericInstance(node, typeof(PropertyNode_SetPropertyTValue<,>)) &&
                       CanDirectSetProperty(node, out _);
            }

            internal bool CanGenerateGetSharedVariable(PropertyNode_SharedVariableValue node)
            {
                return IsGenericInstance(node, typeof(PropertyNode_GetSharedVariableTValue<,,>)) &&
                       CanAccessSharedVariable(node, out _, out _);
            }

            internal bool CanGenerateSetSharedVariable(PropertyNode_SharedVariableValue node)
            {
                return IsGenericInstance(node, typeof(PropertyNode_SetSharedVariableTValue<,,>)) &&
                       CanAccessSharedVariable(node, out _, out _);
            }

            private Type GetFunctionReturnOutputType(FlowNode_ExecuteFunctionReturn node,
                DirectCallInfo directCall)
            {
                return TryGetResolveReturnSelectedType(node, directCall, out var selectedType)
                    ? ResolveFunctionReturnType(directCall.ReturnType, selectedType)
                    : directCall.ReturnType;
            }

            private static Type ResolveFunctionReturnType(Type declaredReturnType, Type selectedType)
            {
                if (declaredReturnType == null || selectedType == null)
                {
                    return declaredReturnType;
                }

                if (declaredReturnType.IsArray && declaredReturnType.GetArrayRank() == 1)
                {
                    var declaredElementType = declaredReturnType.GetElementType();
                    return selectedType.IsAssignableTo(declaredElementType)
                        ? selectedType.MakeArrayType()
                        : declaredReturnType;
                }

                return selectedType.IsAssignableTo(declaredReturnType) ? selectedType : declaredReturnType;
            }

            private static bool TryGetResolveReturnSelectedType(FlowNode_ExecuteFunction node,
                DirectCallInfo directCall, out Type selectedType)
            {
                selectedType = null;
                if (!TryGetResolveReturnParameterIndex(directCall, out var parameterIndex))
                {
                    return false;
                }

                var portData = GetFunctionParameterPortData(node, parameterIndex);
                if (portData?.connections?.Any(connection => !connection.isFlattened) == true)
                {
                    return false;
                }

                var value = portData?.GetPort(node)?.GetValue();
                if (value is not SerializedTypeBase serializedType)
                {
                    return false;
                }

                selectedType = serializedType.GetObjectType();
                return selectedType != null;
            }

            private static bool TryGetResolveReturnParameterIndex(DirectCallInfo directCall, out int parameterIndex)
            {
                parameterIndex = -1;
                try
                {
                    var attribute = ExecutableReflection.GetFunction(directCall.MethodInfo).Attribute;
                    if (!attribute.IsNeedResolveReturnType || attribute.ResolveReturnTypeParameter == null)
                    {
                        return false;
                    }

                    var parameter = attribute.ResolveReturnTypeParameter;
                    var parameters = directCall.MethodInfo.GetParameters();
                    if (parameter.Position >= 0 && parameter.Position < parameters.Length)
                    {
                        parameterIndex = parameter.Position;
                        return true;
                    }

                    parameterIndex = Array.FindIndex(parameters, x => x.Name == parameter.Name);
                    return parameterIndex >= 0;
                }
                catch
                {
                    return false;
                }
            }

            private static CeresPortData GetFunctionParameterPortData(FlowNode_ExecuteFunction node,
                int parameterIndex)
            {
                return node is FlowNode_ExecuteFunctionUber
                    ? node.NodeData.FindPortData("inputs", parameterIndex)
                    : node.NodeData.FindPortData($"input{parameterIndex + 1}");
            }

            internal void GenerateExecuteFunctionVoidLocal(FlowNode_ExecuteFunctionVoid node, string frameTypeName,
                string frameVar, string indent)
            {
                if (!CanGeneratedCall(node, out var directCall) || directCall.ReturnType != typeof(void))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Function node {node.Guid} can not be generated as a direct call.");
                }

                Emit($"{indent}{BuildFunctionCallExpression(node, directCall, frameTypeName, frameVar, indent)};");
            }

            internal void GenerateFunctionReturnLocal(FlowNode_ExecuteFunctionReturn node, string frameTypeName,
                string frameVar, string indent)
            {
                if (!CanGeneratedCall(node, out var directCall) || directCall.ReturnType == typeof(void))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Function return node {node.Guid} can not be generated as a direct call.");
                }

                var outputType = GetFunctionReturnOutputType(node, directCall);
                var slot = EnsureOutputSlot(node, "output", outputType);
                var callExpression = BuildFunctionCallExpression(node, directCall, frameTypeName, frameVar, indent);
                Emit($"{indent}{frameVar}.{slot} = {CastExpression(callExpression, directCall.ReturnType, outputType)};");
            }

            internal void GenerateGetPropertyLocal(PropertyNode_PropertyValue node, string frameTypeName,
                string frameVar, string indent)
            {
                if (!CanDirectGetProperty(node, out var propertyCall))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Property getter node {node.Guid} can not be generated as a direct call.");
                }

                var slot = EnsureOutputSlot(node, "outputValue", propertyCall.PropertyType);
                var targetExpression = BuildPropertyTargetExpression(node, propertyCall, frameTypeName, frameVar,
                    indent);
                Emit($"{indent}{frameVar}.{slot} = {targetExpression}.{EscapeIdentifier(propertyCall.PropertyName)};");
            }

            internal void GenerateGetSelfReferenceLocal(PropertyNode node, string frameTypeName, string frameVar,
                string indent)
            {
                if (!TryGetSelfReferenceTargetType(node, out var targetType))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Self reference node {node.Guid} can not be generated.");
                }

                var slot = EnsureOutputSlot(node, "outputValue", targetType);
                Emit($"{indent}{frameVar}.{slot} = ({GetFriendlyTypeName(targetType)}){frameVar}.ContextObject;");
            }

            internal void GenerateGetSharedVariableLocal(PropertyNode_SharedVariableValue node, string frameTypeName,
                string frameVar, string indent)
            {
                if (!CanAccessSharedVariable(node, out var variableType, out var variableValueType,
                        out var portValueType))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Shared variable getter node {node.Guid} can not be generated.");
                }

                var binding = GetOrCreateGraphVariableBinding(node.propertyName, variableType, variableValueType);
                var slot = EnsureOutputSlot(node, "outputValue", portValueType);
                Emit($"{indent}{frameVar}.{slot} = default;");
                if (binding.IsLocal)
                {
                    if (portValueType.IsValueType || binding.StorageType.IsAssignableTo(portValueType))
                    {
                        Emit($"{indent}{frameVar}.{slot} = {CastExpression(binding.FieldName, binding.StorageType, portValueType)};");
                    }
                    else
                    {
                        Emit($"{indent}if ({binding.FieldName} is {GetFriendlyTypeName(portValueType)} value)");
                        Emit($"{indent}{{");
                        Emit($"{indent}    {frameVar}.{slot} = value;");
                        Emit($"{indent}}}");
                    }
                }
                else
                {
                    Emit($"{indent}if ({binding.FieldName} != null)");
                    Emit($"{indent}{{");
                    if (portValueType.IsValueType)
                    {
                        Emit($"{indent}    {frameVar}.{slot} = ({GetFriendlyTypeName(portValueType)}){binding.FieldName}.Value;");
                    }
                    else
                    {
                        Emit($"{indent}    if ({binding.FieldName}.Value is {GetFriendlyTypeName(portValueType)} value)");
                        Emit($"{indent}    {{");
                        Emit($"{indent}        {frameVar}.{slot} = value;");
                        Emit($"{indent}    }}");
                    }
                    Emit($"{indent}}}");
                }
            }

            private IEnumerable<CeresNode> GetDependencyNodes(CeresNode node)
            {
                var path = _graph.GetNodeDependencyPath(node.Guid);
                if (path == null)
                {
                    yield break;
                }

                foreach (var index in path)
                {
                    if (index < 0 || index >= _graph.nodes.Count)
                    {
                        continue;
                    }

                    var dependency = _graph.nodes[index];
                    if (dependency == node || dependency.NodeData.executionPath == ExecutionPath.Forward)
                    {
                        continue;
                    }

                    yield return dependency;
                }
            }

            private bool TryGetOutputSlot(CeresNode source, string portId, out Type outputType, out string slotField)
            {
                outputType = null;
                slotField = null;
                var context = new NodeGenerationContext(this, _currentFrameName, "frame", string.Empty);
                return NodeGeneratorFactory.Get().TryGetGenerator(source.GetType(), out var generator) &&
                       generator.CanGenerate(source, context) &&
                       generator.TryGetOutputSlot(source, portId, context, out outputType, out slotField);
            }

            internal bool TryGetFunctionReturnOutputSlot(FlowNode_ExecuteFunctionReturn node, string portId,
                out Type outputType, out string slotField)
            {
                outputType = null;
                slotField = null;
                if (portId != "output" ||
                    !CanGeneratedCall(node, out var directCall) ||
                    directCall.ReturnType == typeof(void))
                {
                    return false;
                }

                outputType = GetFunctionReturnOutputType(node, directCall);
                slotField = EnsureOutputSlot(node, portId, outputType);
                return true;
            }

            internal bool TryGenerateFunctionReturnOutputExpression(FlowNode_ExecuteFunctionReturn node,
                string portId, string frameTypeName, string frameVar, string indent, out Type outputType,
                out string expression)
            {
                outputType = null;
                expression = null;
                if (portId != "output" ||
                    !CanGeneratedCall(node, out var directCall) ||
                    directCall.ReturnType == typeof(void) ||
                    !CanInlineFunctionReturn(node, directCall))
                {
                    return false;
                }

                outputType = GetFunctionReturnOutputType(node, directCall);
                var callExpression = BuildFunctionCallExpression(node, directCall, frameTypeName, frameVar, indent);
                expression = CastExpression(callExpression, directCall.ReturnType, outputType);
                return true;
            }

            private bool CanInlineFunctionReturn(FlowNode_ExecuteFunctionReturn node, DirectCallInfo directCall)
            {
                if (_profile == FlowGeneratedRuntimeProfile.Debuggable ||
                    !node.isStatic ||
                    directCall.UseInvoker)
                {
                    return false;
                }

                if (directCall.DeclaringType == typeof(MathExecutableLibrary))
                {
                    return true;
                }

                return _profile == FlowGeneratedRuntimeProfile.OptimizedAggressive &&
                       TryBuildUnityTimeExpression(directCall, out _);
            }

            internal bool TryGetPropertyOutputSlot(PropertyNode_PropertyValue node, string portId,
                out Type outputType, out string slotField)
            {
                outputType = null;
                slotField = null;
                if (portId != "outputValue" ||
                    !CanGenerateGetProperty(node) ||
                    !CanDirectGetProperty(node, out var propertyCall))
                {
                    return false;
                }

                outputType = propertyCall.PropertyType;
                slotField = EnsureOutputSlot(node, portId, outputType);
                return true;
            }

            internal bool TryGetSelfReferenceOutputSlot(PropertyNode node, string portId,
                out Type outputType, out string slotField)
            {
                outputType = null;
                slotField = null;
                if (portId != "outputValue" ||
                    !TryGetSelfReferenceTargetType(node, out var targetType))
                {
                    return false;
                }

                outputType = targetType;
                slotField = EnsureOutputSlot(node, portId, outputType);
                return true;
            }

            internal bool TryGetSharedVariableOutputSlot(PropertyNode_SharedVariableValue node, string portId,
                out Type outputType, out string slotField)
            {
                outputType = null;
                slotField = null;
                if (portId != "outputValue" ||
                    !CanGenerateGetSharedVariable(node) ||
                    !CanAccessSharedVariable(node, out _, out var valueType))
                {
                    return false;
                }

                outputType = valueType;
                slotField = EnsureOutputSlot(node, portId, outputType);
                return true;
            }

            internal string EnsureOutputSlot(CeresNode node, string portId, Type type)
            {
                return EnsureFrameField($"slot:{node.Guid}:{portId}", type,
                    $"Slot_{SafeGuid(node.Guid)}_{SanitizeIdentifier(portId)}");
            }

            internal string EnsureProgramField(CeresNode node, string fieldId, Type type)
            {
                return EnsureProgramField($"{node.Guid}:{fieldId}", type,
                    $"_{SanitizeIdentifier(fieldId)}_{SafeGuid(node.Guid)}");
            }

            internal bool TryGetLocalSharedVariableValueExpression(CeresNode node, string fieldName,
                out Type valueType, out string expression)
            {
                return TryGetLocalSharedVariableValueExpression(node, fieldName, -1, out valueType, out expression);
            }

            internal bool TryGetLocalSharedVariableValueExpression(CeresNode node, string fieldName, int index,
                out Type valueType, out string expression)
            {
                valueType = null;
                expression = null;
                if (!TryGetOrCreateNodeLocalVariableBinding(node, fieldName, index, out var binding))
                {
                    return false;
                }

                valueType = binding.StorageType;
                expression = binding.FieldName;
                return true;
            }

            private bool TryGetOrCreateNodeLocalVariableBinding(CeresNode node, string fieldName, int index,
                out LocalVariableBinding binding)
            {
                binding = default;
                if (_variableStorageMode != FlowGeneratedRuntimeVariableStorageMode.LocalFieldsForUnshared ||
                    node == null ||
                    string.IsNullOrEmpty(fieldName))
                {
                    return false;
                }

                var field = GetFieldInHierarchy(node.GetType(), fieldName);
                if (field == null ||
                    field.GetCustomAttribute<Ceres.Annotations.ForceSharedAttribute>() != null ||
                    !TryGetSharedVariableFromFieldValue(field.GetValue(node), index, out var variable) ||
                    variable.IsShared ||
                    variable.IsGlobal ||
                    !TryGetLocalVariableStorageType(variable, variable.GetValueType(), out var storageType))
                {
                    return false;
                }

                var key = $"node:{node.Guid}:{fieldName}:{index}";
                if (_localVariableBindings.TryGetValue(key, out binding))
                {
                    return true;
                }

                var indexSuffix = index < 0 ? string.Empty : $"_{index}";
                binding = new LocalVariableBinding(
                    $"_local_{SanitizeIdentifier(fieldName)}{indexSuffix}_{SafeGuid(node.Guid)}",
                    storageType,
                    $"FlowGeneratedRuntimeUtility.GetNodeLocalVariableValue<{GetFriendlyTypeName(storageType)}>(graphData, \"{Escape(node.Guid)}\", \"{Escape(fieldName)}\", {index.ToString(CultureInfo.InvariantCulture)})");
                _localVariableBindings.Add(key, binding);
                return true;
            }

            private static FieldInfo GetFieldInHierarchy(Type type, string fieldName)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                while (type != null)
                {
                    var field = type.GetField(fieldName, flags);
                    if (field != null)
                    {
                        return field;
                    }

                    type = type.BaseType;
                }

                return null;
            }

            private static bool TryGetSharedVariableFromFieldValue(object fieldValue, int index,
                out SharedVariable variable)
            {
                variable = null;
                if (index < 0)
                {
                    variable = fieldValue as SharedVariable;
                    return variable != null;
                }

                if (fieldValue is not System.Collections.IList list ||
                    index >= list.Count)
                {
                    return false;
                }

                variable = list[index] as SharedVariable;
                return variable != null;
            }

            private string EnsureProgramField(string key, Type type, string fieldName)
            {
                if (!_programFields.TryGetValue(key, out var field))
                {
                    field = new ProgramFieldInfo(fieldName, type);
                    _programFields.Add(key, field);
                }

                return field.FieldName;
            }

            private string EnsureFrameField(string key, Type type, string fieldName)
            {
                if (!_frameSlots.TryGetValue(key, out var slot))
                {
                    slot = new FrameSlotInfo(fieldName, type);
                    _frameSlots.Add(key, slot);
                }

                return slot.FieldName;
            }

            private static string GetDependencyMethodName(CeresNode node)
            {
                return $"Eval_{SafeGuid(node.Guid)}";
            }

            private string GetEventDelegateExpression(ExecutableEvent source)
            {
                var delegateType = GetEventDelegateType(source);
                var key = $"{source.Guid}:{delegateType.FullName}";
                if (!_eventDelegateBindings.TryGetValue(key, out var binding))
                {
                    binding = new EventDelegateBinding(source.GetEventName(), delegateType,
                        $"_eventDelegate_{SanitizeIdentifier(source.GetEventName())}_{SafeGuid(source.Guid)}");
                    _eventDelegateBindings.Add(key, binding);
                }

                return binding.FieldName;
            }

            private static Type GetEventDelegateType(ExecutableEvent source)
            {
                var field = source.GetType().GetField("eventDelegate",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null || !field.FieldType.IsGenericType)
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Event {source.GetEventName()} ({source.Guid}) does not expose an event delegate port.");
                }

                return field.FieldType.GetGenericArguments()[0];
            }

            private string GetCustomEventOutputExpression(ExecutableEvent source, string portId,
                string frameVar)
            {
                _currentFrameNeedsEventBase = true;
                var eventType = GetCustomEventType(source.GetType());
                if (eventType == null)
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Event {source.GetType().Name} ({source.Guid}) is not a generated custom event.");
                }

                var property = eventType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(info => GetNormalizedPortName(info.Name) == portId);
                if (property == null)
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Custom event output {source.GetType().Name}.{portId} is not supported.");
                }

                return $"(({GetFriendlyTypeName(eventType)}){frameVar}.EventBase).{EscapeIdentifier(property.Name)}";
            }

            private static Type GetCustomEventType(Type nodeType)
            {
                while (nodeType != null)
                {
                    if (nodeType.IsGenericType &&
                        nodeType.GetGenericTypeDefinition() == typeof(CustomExecutionEvent<>))
                    {
                        return nodeType.GetGenericArguments()[0];
                    }

                    nodeType = nodeType.BaseType;
                }

                return null;
            }

            private static string GetNormalizedPortName(string propertyName)
            {
                if (string.IsNullOrEmpty(propertyName)) return propertyName;
                return char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
            }

            private static string CastExpression(string expression, Type expectedType)
            {
                return CastExpression(expression, null, expectedType);
            }

            private static string CastExpression(string expression, Type actualType, Type expectedType)
            {
                if (expectedType == null)
                {
                    return expression;
                }

                if (actualType == expectedType || actualType != null && actualType.IsAssignableTo(expectedType))
                {
                    return expression;
                }

                if (CanCastArrayElements(actualType, expectedType))
                {
                    return $"FlowGeneratedRuntimeUtility.CastArray<{GetFriendlyTypeName(expectedType.GetElementType())}>({expression})";
                }

                return $"({GetFriendlyTypeName(expectedType)})({expression})";
            }

            private static bool CanCastArrayElements(Type actualType, Type expectedType)
            {
                if (actualType == null ||
                    expectedType == null ||
                    !actualType.IsArray ||
                    !expectedType.IsArray ||
                    actualType.GetArrayRank() != 1 ||
                    expectedType.GetArrayRank() != 1)
                {
                    return false;
                }

                var actualElementType = actualType.GetElementType();
                var expectedElementType = expectedType.GetElementType();
                return actualElementType != null &&
                       expectedElementType != null &&
                       actualElementType != expectedElementType &&
                       expectedElementType.IsAssignableTo(actualElementType);
            }

            private static string GetEventOutputExpression(string portId, string frameVar)
            {
                if (portId.StartsWith("output", StringComparison.Ordinal) &&
                    int.TryParse(portId["output".Length..], out var index))
                {
                    return $"{frameVar}.Arg{index}";
                }

                throw new FlowCSharpRuntimeGenerationException($"Unsupported event output port {portId}.");
            }

            private CeresNode FindNode(string guid)
            {
                return _nodes.GetValueOrDefault(guid);
            }

            private string ToLiteral(object value, Type expectedType)
            {
                if (IsNullLiteralValue(value))
                {
                    return ToNullLiteral(expectedType);
                }

                if (TryGetSerializedTypeLiteral(value, expectedType, out var serializedTypeLiteral))
                {
                    return serializedTypeLiteral;
                }

                if (expectedType.IsEnum)
                {
                    return $"({GetFriendlyTypeName(expectedType)}){Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)}";
                }

                if (expectedType == typeof(string))
                {
                    return $"\"{Escape(value.ToString())}\"";
                }

                if (expectedType == typeof(bool))
                {
                    return value is bool b && b ? "true" : "false";
                }

                if (expectedType == typeof(int))
                {
                    return value is int i ? i.ToString(CultureInfo.InvariantCulture) : "0";
                }

                if (expectedType == typeof(float))
                {
                    return value is float f ? f.ToString(CultureInfo.InvariantCulture) + "f" : "0f";
                }

                if (expectedType == typeof(double))
                {
                    return value is double d ? d.ToString(CultureInfo.InvariantCulture) : "0d";
                }

                if (expectedType == typeof(long))
                {
                    return value is long l ? l.ToString(CultureInfo.InvariantCulture) + "L" : "0L";
                }

                if (expectedType == typeof(short))
                {
                    return value is short s ? $"(short){s.ToString(CultureInfo.InvariantCulture)}" : "default(short)";
                }

                if (expectedType == typeof(byte))
                {
                    return value is byte b ? $"(byte){b.ToString(CultureInfo.InvariantCulture)}" : "default(byte)";
                }

                if (expectedType == typeof(object))
                {
                    return value switch
                    {
                        string text => $"\"{Escape(text)}\"",
                        bool b => b ? "true" : "false",
                        int i => i.ToString(CultureInfo.InvariantCulture),
                        float f => f.ToString(CultureInfo.InvariantCulture) + "f",
                        double d => d.ToString(CultureInfo.InvariantCulture),
                        long l => l.ToString(CultureInfo.InvariantCulture) + "L",
                        _ => throw new FlowCSharpRuntimeGenerationException(
                            $"Default object literal of type {value.GetType().Name} is not supported.")
                    };
                }

                throw new FlowCSharpRuntimeGenerationException($"Default literal for {expectedType.Name} is not supported.");
            }

            private static bool IsNullLiteralValue(object value)
            {
                return value == null || value is UObject unityObject && !unityObject;
            }

            private static string ToNullLiteral(Type expectedType)
            {
                return expectedType.IsValueType && Nullable.GetUnderlyingType(expectedType) == null
                    ? $"default({GetFriendlyTypeName(expectedType)})"
                    : "null";
            }

            private bool TryGetSerializedTypeLiteral(object value, Type expectedType, out string expression)
            {
                expression = null;
                if (value is not SerializedTypeBase serializedType ||
                    !IsSerializedType(expectedType))
                {
                    return false;
                }

                var serializedTypeString = serializedType.serializedTypeString ?? string.Empty;
                var key = $"{expectedType.FullName}:{serializedTypeString}";
                if (!_serializedTypeBindings.TryGetValue(key, out var binding))
                {
                    binding = new SerializedTypeBinding(expectedType, serializedTypeString,
                        $"_serializedType_{_serializedTypeBindings.Count}");
                    _serializedTypeBindings.Add(key, binding);
                }

                expression = binding.FieldName;
                return true;
            }

            private static bool IsSerializedType(Type type)
            {
                while (type != null)
                {
                    if (type.IsGenericType &&
                        type.GetGenericTypeDefinition() == typeof(SerializedType<>))
                    {
                        return true;
                    }

                    type = type.BaseType;
                }

                return false;
            }

            private static string EscapeIdentifier(string value)
            {
                return CSharpKeywords.Contains(value) ? $"@{value}" : value;
            }

            private static string SafeGuid(string guid)
            {
                return guid.Replace("-", "_");
            }

            public static string GetFriendlyTypeName(Type type)
            {
                if (type == typeof(void)) return "void";
                if (type == typeof(int)) return "int";
                if (type == typeof(float)) return "float";
                if (type == typeof(double)) return "double";
                if (type == typeof(bool)) return "bool";
                if (type == typeof(string)) return "string";
                if (type == typeof(object)) return "object";
                if (type.IsArray)
                {
                    return $"{GetFriendlyTypeName(type.GetElementType())}[]";
                }

                if (type.IsGenericType)
                {
                    var name = type.GetGenericTypeDefinition().FullName;
                    name = name[..name.IndexOf('`')];
                    return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName))}>";
                }

                return type.FullName?.Replace("+", ".") ?? type.Name;
            }

            private static readonly HashSet<string> CSharpKeywords = new()
            {
                "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
                "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
                "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
                "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
                "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
                "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
                "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
                "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
                "using", "virtual", "void", "volatile", "while"
            };

            private readonly struct FrameSlotInfo
            {
                public readonly string FieldName;

                public readonly Type Type;

                public FrameSlotInfo(string fieldName, Type type)
                {
                    FieldName = fieldName;
                    Type = type;
                }
            }

            private readonly struct ProgramFieldInfo
            {
                public readonly string FieldName;

                public readonly Type Type;

                public ProgramFieldInfo(string fieldName, Type type)
                {
                    FieldName = fieldName;
                    Type = type;
                }
            }

            private readonly struct SharedVariableBinding
            {
                public readonly string VariableName;

                public readonly Type VariableType;

                public readonly string FieldName;

                public SharedVariableBinding(string variableName, Type variableType, string fieldName)
                {
                    VariableName = variableName;
                    VariableType = variableType;
                    FieldName = fieldName;
                }
            }

            private readonly struct LocalVariableBinding
            {
                public readonly string FieldName;

                public readonly Type StorageType;

                public readonly string InitializerExpression;

                public LocalVariableBinding(string fieldName, Type storageType, string initializerExpression)
                {
                    FieldName = fieldName;
                    StorageType = storageType;
                    InitializerExpression = initializerExpression;
                }
            }

            private readonly struct GraphVariableBinding
            {
                public readonly bool IsLocal;

                public readonly string FieldName;

                public readonly Type StorageType;

                private GraphVariableBinding(bool isLocal, string fieldName, Type storageType)
                {
                    IsLocal = isLocal;
                    FieldName = fieldName;
                    StorageType = storageType;
                }

                public static GraphVariableBinding FromLocal(LocalVariableBinding binding)
                {
                    return new GraphVariableBinding(true, binding.FieldName, binding.StorageType);
                }

                public static GraphVariableBinding FromShared(SharedVariableBinding binding)
                {
                    return new GraphVariableBinding(false, binding.FieldName, binding.VariableType);
                }
            }

            private readonly struct DirectSerializedTypeArgument
            {
                public readonly int ParameterIndex;

                public readonly Type SelectedType;

                public DirectSerializedTypeArgument(int parameterIndex, Type selectedType)
                {
                    ParameterIndex = parameterIndex;
                    SelectedType = selectedType;
                }
            }

            private readonly struct FunctionInvokerBinding
            {
                public readonly string FieldName;

                public readonly Type TargetType;

                public readonly string MethodName;

                public readonly Type ReturnType;

                public readonly Type[] ParameterTypes;

                public readonly bool IsStatic;

                public FunctionInvokerBinding(string fieldName, Type targetType, string methodName, Type returnType,
                    Type[] parameterTypes, bool isStatic)
                {
                    FieldName = fieldName;
                    TargetType = targetType;
                    MethodName = methodName;
                    ReturnType = returnType;
                    ParameterTypes = parameterTypes;
                    IsStatic = isStatic;
                }
            }

            private readonly struct EventDelegateBinding
            {
                public readonly string EventName;

                public readonly Type DelegateType;

                public readonly string FieldName;

                public EventDelegateBinding(string eventName, Type delegateType, string fieldName)
                {
                    EventName = eventName;
                    DelegateType = delegateType;
                    FieldName = fieldName;
                }
            }

            private readonly struct SerializedTypeBinding
            {
                public readonly Type SerializedType;

                public readonly string SerializedTypeString;

                public readonly string FieldName;

                public SerializedTypeBinding(Type serializedType, string serializedTypeString, string fieldName)
                {
                    SerializedType = serializedType;
                    SerializedTypeString = serializedTypeString;
                    FieldName = fieldName;
                }
            }

            private readonly struct CustomFunctionBinding
            {
                public readonly FlowGraph Graph;

                public readonly string MethodName;

                public readonly string FrameName;

                public readonly Type ReturnType;

                public readonly Type[] ParameterTypes;

                public CustomFunctionBinding(FlowGraph graph, string methodName, string frameName, Type returnType,
                    Type[] parameterTypes)
                {
                    Graph = graph;
                    MethodName = methodName;
                    FrameName = frameName;
                    ReturnType = returnType;
                    ParameterTypes = parameterTypes;
                }
            }
        }

        private readonly struct EventInfo
        {
            private enum ArgumentSource
            {
                ExecuteFlowEvent,
                Uber,
                SubFlow
            }

            public readonly Type[] ArgumentTypes;

            public readonly Type ReturnType;

            private readonly ArgumentSource _argumentSource;

            private EventInfo(Type[] argumentTypes, Type returnType, ArgumentSource argumentSource)
            {
                ArgumentTypes = argumentTypes;
                ReturnType = returnType;
                _argumentSource = argumentSource;
            }

            public static EventInfo FromCustomFunction(Type[] argumentTypes, Type returnType)
            {
                return new EventInfo(argumentTypes, returnType, ArgumentSource.SubFlow);
            }

            public static EventInfo From(ExecutableEvent evt)
            {
                if (evt is ExecutionEventUber uber)
                {
                    return new EventInfo(Enumerable.Repeat(typeof(object), uber.argumentCount).ToArray(),
                        typeof(void), ArgumentSource.Uber);
                }

                if (evt is CustomExecutionEvent)
                {
                    return new EventInfo(Type.EmptyTypes, typeof(void), ArgumentSource.ExecuteFlowEvent);
                }

                var type = evt.GetType();
                return evt is ExecutionEventGeneric && type.IsGenericType
                    ? new EventInfo(type.GetGenericArguments(), typeof(void), ArgumentSource.ExecuteFlowEvent)
                    : new EventInfo(Type.EmptyTypes, typeof(void), ArgumentSource.ExecuteFlowEvent);
            }

            public void GenerateAssignments(StringBuilder body, string frameVar, string eventVar, string indent)
            {
                if (ArgumentTypes.Length == 0) return;
                if (_argumentSource == ArgumentSource.Uber)
                {
                    body.AppendLine($"{indent}var flowEvent = ({typeof(ExecuteFlowEvent).FullName}){eventVar};");
                    for (var i = 0; i < ArgumentTypes.Length; i++)
                    {
                        body.AppendLine($"{indent}{frameVar}.Arg{i + 1} = flowEvent.Args != null && flowEvent.Args.Length > {i} ? flowEvent.Args[{i}] : null;");
                    }
                    return;
                }

                if (_argumentSource == ArgumentSource.SubFlow)
                {
                    for (var i = 0; i < ArgumentTypes.Length; i++)
                    {
                        body.AppendLine($"{indent}{frameVar}.Arg{i + 1} = FlowGeneratedRuntimeUtility.GetSubFlowArgument<{SourceContext.GetFriendlyTypeName(ArgumentTypes[i])}>({eventVar}, {i});");
                    }
                    return;
                }

                var eventType = $"ExecuteFlowEvent<{string.Join(", ", ArgumentTypes.Select(SourceContext.GetFriendlyTypeName))}>";
                body.AppendLine($"{indent}var flowEvent = ({eventType}){eventVar};");
                for (var i = 0; i < ArgumentTypes.Length; i++)
                {
                    body.AppendLine($"{indent}{frameVar}.Arg{i + 1} = flowEvent.Arg{i + 1};");
                }
            }
        }
    }
}
