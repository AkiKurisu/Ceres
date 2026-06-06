using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.CustomFunctions;
using Ceres.Graph.Flow.Properties;
using Ceres.Graph.Flow.Utilities;
using Ceres.Utilities;
using Chris.Events;
using Chris.Serialization;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal static class FlowSyncIrCompiler
    {
        public static CsCompilationUnit TryCompile(FlowCompilationContext context)
        {
            var builder = new Builder(context);
            return builder.TryCompile();
        }

        private sealed class Builder
        {
            private readonly FlowCompilationContext _context;

            private readonly FlowNodeLowererFactory _nodeLowerers;

            private readonly FlowLoweringContext _loweringContext;

            private readonly Dictionary<string, GraphVariableBinding> _graphVariableBindings = new();

            private readonly Dictionary<string, SharedVariableBinding> _sharedVariableBindings = new();

            private readonly Dictionary<string, LocalVariableBinding> _localVariableBindings = new();

            private readonly Dictionary<FlowOutputKey, MaterializedValue> _materializedValues = new();

            private readonly Dictionary<FlowOutputKey, PersistentOutputBinding> _persistentOutputs = new();

            private readonly Dictionary<string, GraphValueBinding> _graphValueBindings = new();

            private readonly HashSet<string> _loweringStack = new();

            private CsMethod _currentMethod;

            private CsBlock _currentBlock;

            private ExecutableEvent _currentEvent;

            private Dictionary<string, string> _eventArgumentExpressions;

            private int _tempIndex;

            public Builder(FlowCompilationContext context)
            {
                _context = context;
                _nodeLowerers = FlowNodeLowererFactory.Get();
                _loweringContext = FlowLoweringContext.CreateSync(
                    _context,
                    () => _currentBlock,
                    LowerInput,
                    LowerForwardConnection,
                    LowerDefaultNext,
                    LowerBranch,
                    BuildFunctionCallExpression,
                    BuildFunctionReturnExpression,
                    MaterializeFunctionReturnIfNeeded,
                    LowerSetProperty,
                    LowerSetSharedVariable,
                    BuildGetPropertyExpression,
                    BuildSelfReferenceExpression,
                    BuildGetSharedVariableExpression,
                    BuildNodeFieldValueExpression);
            }

            public CsCompilationUnit TryCompile()
            {
                if (!CanCompileGraph())
                {
                    return null;
                }

                try
                {
                    var unit = CreateUnit();
                    var model = unit.Class;
                    foreach (var evt in _context.CompilationGraph.Events)
                    {
                        if (!TryCompileEvent(evt, model, out var methodName))
                        {
                            return null;
                        }

                        RegisterEventCase(model, evt.GetEventName(), methodName);
                    }

                    EmitFieldsAndConstructor(model);
                    return unit;
                }
                catch (FlowCSharpRuntimeGenerationException ex)
                {
                    _context.AddDiagnostic(new FlowCompilationDiagnostic(FlowCompilationDiagnosticSeverity.Info,
                        $"IR lowering fallback: {ex.Message}"));
                    return null;
                }
            }

            private bool CanCompileGraph()
            {
                if (_context.Graph.nodes.OfType<CustomFunctionInput>().Any() ||
                    _context.Graph.nodes.OfType<CustomFunctionOutput>().Any())
                {
                    return false;
                }

                return _context.Graph.nodes.All(CanCompileNode);
            }

            private bool CanCompileNode(CeresNode node)
            {
                return node is ExecutableEvent || _nodeLowerers.CanLower(node, _loweringContext);
            }

            private CsCompilationUnit CreateUnit()
            {
                var unit = new CsCompilationUnit
                {
                    Namespace = FlowCSharpRuntimeGenerator.GeneratedNamespace,
                    Class = new CsClassModel
                    {
                        Name = _context.ClassName,
                        BaseType = "FlowGeneratedProgram",
                        BaseConstructorArguments = "graphData, false"
                    }
                };
                unit.HeaderLines.AddRange(FlowCSharpRuntimeGenerator.GeneratedHeaderLines);
                unit.UsingLines.AddRange(FlowCSharpRuntimeGenerator.ProgramUsingLines);

                unit.Class.Methods.Add(new CsMethod
                {
                    Modifiers = "public override",
                    ReturnType = typeof(bool),
                    Name = "TryExecuteEvent"
                });
                var tryExecute = unit.Class.Methods[0];
                tryExecute.Parameters.Add(new CsParameter(typeof(UObject), "contextObject"));
                tryExecute.Parameters.Add(new CsParameter(typeof(string), "eventName"));
                unit.Class.RawMembers.Add("__CERES_EVENT_SWITCH_CASES__");
                return unit;
            }

            private void RegisterEventCase(CsClassModel model, string eventName, string methodName)
            {
                var @case =
                    $"            case \"{FlowCSharpRuntimeGenerator.Escape(eventName)}\":\n" +
                    "            {\n" +
                    $"                {methodName}(contextObject, evtBase);\n" +
                    "                return true;\n" +
                    "            }";
                var cases = model.RawMembers[0];
                model.RawMembers[0] = cases == "__CERES_EVENT_SWITCH_CASES__" ? @case : cases + "\n" + @case;
            }

            private void CompleteTryExecuteMethod(CsClassModel model)
            {
                var cases = model.RawMembers.Count == 0 ? string.Empty : model.RawMembers[0];
                model.RawMembers.Clear();
                var method = model.Methods.First(x => x.Name == "TryExecuteEvent");
                method.Parameters.Clear();
                method.Parameters.Add(new CsParameter(typeof(UObject), "contextObject"));
                method.Parameters.Add(new CsParameter(typeof(string), "eventName"));
                method.Parameters.Add(new CsParameter(typeof(EventBase), "evtBase = null"));
                method.Body.AddRaw("switch (eventName)\n{");
                method.Body.AddRaw(cases);
                method.Body.AddRaw("            default:\n                return false;\n        }");
            }

            private bool TryCompileEvent(ExecutableEvent evt, CsClassModel model, out string methodName)
            {
                methodName =
                    $"Execute_{FlowCSharpRuntimeGenerator.SanitizeIdentifier(evt.GetEventName())}_{SafeGuid(evt.Guid)}";
                _currentEvent = evt;
                _eventArgumentExpressions = BuildEventArgumentExpressions(evt);
                if (_eventArgumentExpressions == null)
                {
                    return false;
                }

                _tempIndex = 0;
                _materializedValues.Clear();
                _loweringStack.Clear();

                _currentMethod = new CsMethod
                {
                    Name = methodName,
                    ReturnType = typeof(void)
                };
                _currentMethod.Parameters.Add(new CsParameter(typeof(UObject), "contextObject"));
                _currentMethod.Parameters.Add(new CsParameter(typeof(EventBase), "evtBase"));
                _currentBlock = new CsBlock();

                foreach (var statement in BuildEventArgumentStatements(evt))
                {
                    _currentBlock.Add(statement);
                }

                if (ShouldEmitSyncCancellationChecks())
                {
                    _currentBlock.AddRaw(
                        $"using var cancellation = GetCancellation(contextObject, \"{FlowCSharpRuntimeGenerator.Escape(evt.GetEventName())}\");");
                }

                _currentBlock.Add(new CsRawStatement("PushExecutionContext(contextObject);"));
                LowerForwardConnection(_context.CompilationGraph.GetExecConnection(evt, "exec"));

                var tryFinally = new CsTryFinallyStatement();
                foreach (var statement in _currentBlock.Statements)
                {
                    tryFinally.Try.Add(statement);
                }

                tryFinally.Finally.Add(new CsRawStatement("PopExecutionContext(contextObject);"));
                _currentMethod.Body.Add(tryFinally);
                model.Methods.Add(_currentMethod);
                _currentEvent = null;
                _eventArgumentExpressions = null;
                _currentMethod = null;
                _currentBlock = null;
                return true;
            }

            private Dictionary<string, string> BuildEventArgumentExpressions(ExecutableEvent evt)
            {
                if (evt is ExecutionEventUber)
                {
                    return null;
                }

                if (evt is CustomExecutionEvent)
                {
                    return new Dictionary<string, string>();
                }

                var result = new Dictionary<string, string>();
                if (evt is not ExecutionEventGeneric || !evt.GetType().IsGenericType)
                {
                    return result;
                }

                var arguments = evt.GetType().GetGenericArguments();
                for (var i = 0; i < arguments.Length; i++)
                {
                    result[$"output{i + 1}"] = $"flowEvent.Arg{i + 1}";
                }

                return result;
            }

            private IEnumerable<CsStatement> BuildEventArgumentStatements(ExecutableEvent evt)
            {
                if (evt is not ExecutionEventGeneric || !evt.GetType().IsGenericType)
                {
                    yield break;
                }

                var arguments = evt.GetType().GetGenericArguments();
                var eventType = $"ExecuteFlowEvent<{string.Join(", ", arguments.Select(FlowCSharpRuntimeGenerator.GetFriendlyTypeName))}>";
                yield return new CsRawStatement($"var flowEvent = ({eventType})evtBase;");
            }

            private void EmitFieldsAndConstructor(CsClassModel model)
            {
                foreach (var binding in _sharedVariableBindings.Values)
                {
                    model.Fields.Add(new CsField("private readonly", binding.VariableType, binding.FieldName));
                    model.ConstructorBody.AddRaw(
                        $"{binding.FieldName} = FlowGeneratedRuntimeUtility.GetRequiredSharedVariable<{FlowCSharpRuntimeGenerator.GetFriendlyTypeName(binding.VariableType)}>(Blackboard, \"{FlowCSharpRuntimeGenerator.Escape(binding.VariableName)}\");");
                }

                foreach (var binding in _localVariableBindings.Values)
                {
                    model.Fields.Add(new CsField("private", binding.StorageType, binding.FieldName));
                    model.ConstructorBody.AddRaw($"{binding.FieldName} = {binding.InitializerExpression};");
                }

                foreach (var binding in _graphValueBindings.Values)
                {
                    model.Fields.Add(new CsField("private readonly", binding.Type, binding.FieldName));
                    model.ConstructorBody.AddRaw($"{binding.FieldName} = {binding.InitializerExpression};");
                }

                foreach (var binding in _persistentOutputs.Values)
                {
                    model.Fields.Add(new CsField("private", binding.Type, binding.FieldName));
                    model.ConstructorBody.AddRaw($"{binding.FieldName} = {binding.InitializerExpression};");
                }

                model.BaseConstructorArguments =
                    _sharedVariableBindings.Count > 0 ? "graphData, true" : "graphData, false";
                CompleteTryExecuteMethod(model);
                EmitCustomEventResolver(model);
            }

            private void EmitCustomEventResolver(CsClassModel model)
            {
                var customEvents = _context.CompilationGraph.Events
                    .Select(evt =>
                    {
                        var hasEventBaseType =
                            FlowCSharpRuntimeGenerator.TryGetCustomExecutionEventBaseType(evt, out var eventBaseType);
                        return new
                        {
                            Event = evt,
                            HasEventBaseType = hasEventBaseType,
                            EventBaseType = eventBaseType
                        };
                    })
                    .Where(item => item.HasEventBaseType)
                    .ToArray();
                if (customEvents.Length == 0) return;

                var body = new StringBuilder();
                body.AppendLine("        protected override bool TryGetCustomEventName(long eventTypeId, out string eventName)");
                body.AppendLine("        {");
                foreach (var item in customEvents)
                {
                    body.AppendLine(
                        $"            if (eventTypeId == EventBase<{FlowCSharpRuntimeGenerator.GetFriendlyTypeName(item.EventBaseType)}>.TypeId())");
                    body.AppendLine("            {");
                    body.AppendLine(
                        $"                eventName = \"{FlowCSharpRuntimeGenerator.Escape(item.Event.GetEventName())}\";");
                    body.AppendLine("                return true;");
                    body.AppendLine("            }");
                    body.AppendLine();
                }

                body.AppendLine("            return base.TryGetCustomEventName(eventTypeId, out eventName);");
                body.AppendLine("        }");
                model.RawMembers.Add(body.ToString());
            }

            private void LowerForwardConnection(FlowConnection connection)
            {
                if (connection.IsValid)
                {
                    LowerForwardNode(connection.Node);
                }
            }

            private void LowerForwardNode(CeresNode node)
            {
                if (!_loweringStack.Add(node.Guid))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Execution cycle is not supported by optimized sync lowering ({node.Guid}).");
                }

                try
                {
                    EmitSyncCancellationCheck();

                    if (!_nodeLowerers.TryGetLowerer(node.GetType(), out var lowerer) ||
                        !lowerer.CanLower(node, _loweringContext))
                    {
                        throw new FlowCSharpRuntimeGenerationException(
                            $"Node {node.GetType().Name} ({node.Guid}) is not supported by optimized sync lowering.");
                    }

                    lowerer.LowerForward(node, _loweringContext);
                }
                finally
                {
                    _loweringStack.Remove(node.Guid);
                }
            }

            private void LowerBranch(FlowNode_Branch node)
            {
                var condition = LowerInput(node, "condition", typeof(bool));
                var statement = new CsIfStatement(condition);
                var previous = _currentBlock;
                var materializedBeforeBranch = new HashSet<FlowOutputKey>(_materializedValues.Keys);
                _currentBlock = statement.Then;
                LowerForwardConnection(_context.CompilationGraph.GetExecConnection(node, "trueOutput"));
                RemoveMaterializationsAddedAfter(materializedBeforeBranch);
                _currentBlock = statement.Else;
                LowerForwardConnection(_context.CompilationGraph.GetExecConnection(node, "falseOutput"));
                RemoveMaterializationsAddedAfter(materializedBeforeBranch);
                _currentBlock = previous;
                _currentBlock.Add(statement);
            }

            private void RemoveMaterializationsAddedAfter(HashSet<FlowOutputKey> snapshot)
            {
                foreach (var key in _materializedValues.Keys.ToArray())
                {
                    if (!snapshot.Contains(key))
                    {
                        _materializedValues.Remove(key);
                    }
                }
            }

            private void MaterializeFunctionReturnIfNeeded(FlowNode_ExecuteFunctionReturn node)
            {
                if (_context.CompilationGraph.GetConsumerCount(node, "output") > 0 ||
                    IsImpureFunction(node))
                {
                    MaterializeOutput(node, "output", BuildFunctionReturnExpression(node));
                }
            }

            private void LowerSetProperty(PropertyNode_PropertyValue node)
            {
                if (!FlowCSharpRuntimeGenerator.CanDirectSetProperty(node, out var propertyCall))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Property setter node {node.Guid} can not be generated as a direct call.");
                }

                var target = BuildPropertyTargetExpression(node, propertyCall);
                var value = LowerInput(node, "inputValue", propertyCall.PropertyType);
                _currentBlock.AddRaw($"{target}.{EscapeIdentifier(propertyCall.PropertyName)} = {value.Code};");
                LowerDefaultNext(node);
            }

            private void LowerSetSharedVariable(PropertyNode_SharedVariableValue node)
            {
                if (!FlowCSharpRuntimeGenerator.CanAccessSharedVariable(node, out var variableType,
                        out var variableValueType, out var portValueType))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Shared variable setter node {node.Guid} can not be generated.");
                }

                var binding = GetGraphVariableBinding(node.propertyName, variableType, variableValueType);
                var value = LowerInput(node, "inputValue", portValueType);
                if (binding.IsLocal)
                {
                    _currentBlock.AddRaw($"{binding.FieldName} = {CastExpression(value.Code, portValueType, binding.StorageType)};");
                }
                else
                {
                    _currentBlock.AddRaw(
                        $"if ({binding.FieldName} != null)\n{{\n    {binding.FieldName}.Value = {value.Code};\n}}");
                }

                LowerDefaultNext(node);
            }

            private void LowerDefaultNext(CeresNode node)
            {
                LowerForwardConnection(_context.CompilationGraph.GetExecConnection(node, "exec"));
            }

            private CsExpression LowerInput(CeresNode node, string propertyName, Type expectedType)
            {
                return LowerInput(node, propertyName, -1, expectedType);
            }

            private CsExpression LowerInput(CeresNode node, string propertyName, int arrayIndex, Type expectedType)
            {
                if (_context.CompilationGraph.TryGetInputConnection(node, propertyName, arrayIndex,
                        out var connection))
                {
                    return LowerValueConnection(connection, expectedType);
                }

                var portData = arrayIndex < 0
                    ? node.NodeData.FindPortData(propertyName)
                    : node.NodeData.FindPortData(propertyName, arrayIndex);
                var value = portData?.GetPort(node)?.GetValue();
                return BuildGraphPortDefaultExpression(node, propertyName, arrayIndex, expectedType, portData, value);
            }

            private CsExpression LowerValueConnection(FlowConnection connection, Type expectedType)
            {
                if (connection.Node is ExecutableEvent evt)
                {
                    return LowerEventOutput(evt, connection.PortId, expectedType);
                }

                var expression = BuildValueExpression(connection.Node, connection.PortId);
                return CastExpression(expression, expectedType);
            }

            private CsExpression BuildGraphPortDefaultExpression(CeresNode node, string portName, int portIndex,
                Type expectedType, CeresPortData portData, object value)
            {
                if (FlowCSharpRuntimeGenerator.TryBuildLiteralExpression(value, expectedType, out var literal))
                {
                    return new CsExpression(literal, expectedType);
                }

                var binding = GetOrCreateGraphPortValueBinding(node, portName, portIndex, expectedType, portData);
                return new CsExpression(binding.FieldName, binding.Type, CsExpressionPurity.CachePerEvent, true,
                    false);
            }

            private CsExpression BuildNodeFieldValueExpression(CeresNode node, string fieldName, int fieldIndex,
                Type expectedType)
            {
                var field = GetRequiredNodeField(node, fieldName, expectedType);
                var fieldValue = field.GetValue(node);
                var value = fieldIndex < 0 ? fieldValue : GetIndexedGraphValue(node, fieldName, fieldIndex,
                    fieldValue, expectedType);
                if (FlowCSharpRuntimeGenerator.TryBuildLiteralExpression(value, expectedType, out var literal))
                {
                    return new CsExpression(literal, expectedType);
                }

                var binding = GetOrCreateGraphNodeFieldValueBinding(node, fieldName, fieldIndex, expectedType,
                    field);
                return new CsExpression(binding.FieldName, binding.Type, CsExpressionPurity.CachePerEvent, true,
                    false);
            }

            private CsExpression LowerEventOutput(ExecutableEvent evt, string portId, Type expectedType)
            {
                if (evt != _currentEvent)
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Cross-event value reads are not supported by optimized sync lowering ({evt.Guid}).");
                }

                if (!_eventArgumentExpressions.TryGetValue(portId, out var expression))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Event output {evt.GetType().Name}.{portId} is not supported by optimized sync lowering.");
                }

                return new CsExpression(CastExpression(expression, null, expectedType), expectedType);
            }

            private CsExpression BuildValueExpression(CeresNode node, string portId)
            {
                var key = new FlowOutputKey(node.Guid, portId, -1);
                if (_materializedValues.TryGetValue(key, out var materialized))
                {
                    return new CsExpression(materialized.LocalName, materialized.Type,
                        materialized.Purity, true, false);
                }

                if (TryGetPersistentOutputExpression(node, portId, out var persistentExpression))
                {
                    return persistentExpression;
                }

                if (!_nodeLowerers.TryGetLowerer(node.GetType(), out var lowerer) ||
                    !lowerer.TryLowerValue(node, portId, _loweringContext, out var expression))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Value node {node.GetType().Name}.{portId} ({node.Guid}) is not supported by optimized sync lowering.");
                }

                return ShouldMaterialize(node, portId, expression)
                    ? MaterializeOutput(node, portId, expression)
                    : expression;
            }

            private CsExpression BuildFunctionReturnExpression(FlowNode_ExecuteFunctionReturn node)
            {
                var expression = BuildFunctionCallExpression(node, out var directCall);
                var outputType = GetFunctionReturnOutputType(node, directCall);
                return CastExpression(expression, outputType);
            }

            private CsExpression BuildFunctionCallExpression(FlowNode_ExecuteFunction node,
                out FlowCSharpRuntimeGenerator.DirectCallInfo directCall)
            {
                if (!FlowCSharpRuntimeGenerator.CanGeneratedCall(node, out directCall) ||
                    directCall.UseInvoker)
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Function node {node.Guid} can not be generated as a direct optimized call.");
                }

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

                    var expression = LowerInput(node, $"input{i + 1}", directCall.ParameterTypes[i]);
                    var argumentExpression = expression.Code;
                    if (node.isStatic && node.isSelfTarget && i == 0)
                    {
                        argumentExpression = BuildSelfTargetArgumentExpression(
                            node,
                            $"input{i + 1}",
                            directCall.ParameterTypes[i],
                            expression).Code;
                    }

                    arguments.Add($"({FlowCSharpRuntimeGenerator.GetFriendlyTypeName(directCall.ParameterTypes[i])})({argumentExpression})");
                }

                if (useDirectSerializedType &&
                    TryBuildDirectSerializedTypeFunctionExpression(directCall, directSerializedTypeArgument,
                        arguments, out var directSerializedTypeExpression))
                {
                    return CreateCallExpression(directSerializedTypeExpression, directCall);
                }

                if (node.isStatic &&
                    FlowExpressionClassifier.TryBuildIntrinsicFunctionExpression(directCall, arguments, out var intrinsic))
                {
                    return CreateCallExpression(intrinsic, directCall);
                }

                if (node.isStatic)
                {
                    return CreateCallExpression(
                        $"{FlowCSharpRuntimeGenerator.GetFriendlyTypeName(directCall.DeclaringType)}.{EscapeIdentifier(directCall.MethodName)}({string.Join(", ", arguments)})",
                        directCall);
                }

                var targetExpression = BuildTargetOrDefaultExpression(
                    node,
                    "target",
                    directCall.TargetType,
                    node.isSelfTarget,
                    false).Code;
                return CreateCallExpression(
                    $"({targetExpression}).{EscapeIdentifier(directCall.MethodName)}({string.Join(", ", arguments)})",
                    directCall);
            }

            private static CsExpression CreateCallExpression(string code,
                FlowCSharpRuntimeGenerator.DirectCallInfo directCall)
            {
                var canInline = FlowExpressionClassifier.CanInlineFunction(directCall);
                return new CsExpression(code, directCall.ReturnType,
                    FlowExpressionClassifier.ClassifyFunction(directCall), canInline, !canInline);
            }

            private bool ShouldMaterialize(CeresNode node, string portId, CsExpression expression)
            {
                return expression.RequiresMaterialization ||
                       expression.Purity is CsExpressionPurity.Impure or CsExpressionPurity.CachePerEvent ||
                       _context.CompilationGraph.GetConsumerCount(node, portId) > 1;
            }

            private CsExpression MaterializeOutput(CeresNode node, string portId, CsExpression expression)
            {
                var key = new FlowOutputKey(node.Guid, portId, -1);
                if (_materializedValues.TryGetValue(key, out var existing))
                {
                    return new CsExpression(existing.LocalName, existing.Type, existing.Purity);
                }

                if (TryGetPersistentOutputBinding(node, portId, expression.Type, out var persistent))
                {
                    EmitSyncCancellationCheck();
                    var assignment = new CsAssignmentStatement(persistent.FieldName, CastExpression(expression, persistent.Type));
                    _currentBlock.Add(assignment);
                    var persistentValue = new MaterializedValue(persistent.FieldName, persistent.Type, expression.Purity);
                    _materializedValues.Add(key, persistentValue);
                    return new CsExpression(persistent.FieldName, persistent.Type, expression.Purity);
                }

                var localName =
                    $"value_{FlowCSharpRuntimeGenerator.SanitizeIdentifier(portId)}_{SafeGuid(node.Guid)}_{_tempIndex++}";
                EmitSyncCancellationCheck();
                _currentBlock.Add(new CsDeclarationStatement(expression.Type, localName, expression));
                var materialized = new MaterializedValue(localName, expression.Type, expression.Purity);
                _materializedValues.Add(key, materialized);
                return new CsExpression(localName, expression.Type, expression.Purity);
            }

            private bool TryGetPersistentOutputExpression(CeresNode node, string portId, out CsExpression expression)
            {
                expression = null;
                if (!ShouldPersistOutput(node, portId))
                {
                    return false;
                }

                if (!TryGetOutputType(node, portId, out var outputType) ||
                    !TryGetPersistentOutputBinding(node, portId, outputType, out var binding))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Persistent output {node.GetType().Name}.{portId} ({node.Guid}) can not be typed for optimized lowering.");
                }

                expression = new CsExpression(binding.FieldName, binding.Type, CsExpressionPurity.CachePerEvent);
                return true;
            }

            private bool TryGetPersistentOutputBinding(CeresNode node, string portId, Type type,
                out PersistentOutputBinding binding)
            {
                binding = default;
                if (!ShouldPersistOutput(node, portId))
                {
                    return false;
                }

                if (type == null || !FlowCSharpRuntimeGenerator.IsVisibleType(type))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Persistent output {node.GetType().Name}.{portId} ({node.Guid}) has inaccessible type {type?.FullName ?? "<null>"}.");
                }

                var key = new FlowOutputKey(node.Guid, portId, -1);
                if (_persistentOutputs.TryGetValue(key, out binding))
                {
                    return true;
                }

                binding = new PersistentOutputBinding(
                    $"_port_{SafeGuid(node.Guid)}_{FlowCSharpRuntimeGenerator.SanitizeIdentifier(portId)}",
                    type,
                    $"FlowGeneratedRuntimeUtility.GetNodePortDefaultValue<{FlowCSharpRuntimeGenerator.GetFriendlyTypeName(type)}>(graphData, \"{FlowCSharpRuntimeGenerator.Escape(node.Guid)}\", \"{FlowCSharpRuntimeGenerator.Escape(portId)}\", -1)");
                _persistentOutputs.Add(key, binding);
                return true;
            }

            private GraphValueBinding GetOrCreateGraphPortValueBinding(CeresNode node, string portName,
                int portIndex, Type type, CeresPortData portData)
            {
                if (node == null || string.IsNullOrEmpty(portName))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Graph port value can not be generated because the node or port name is missing.");
                }

                if (portData == null)
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Graph port value {node.GetType().Name}.{portName} ({node.Guid}) can not be located.");
                }

                EnsureGraphValueTypeIsVisible(node, portName, type);
                var key = $"port:{node.Guid}:{portName}:{portIndex}:{type.FullName}";
                if (_graphValueBindings.TryGetValue(key, out var binding))
                {
                    return binding;
                }

                var indexSuffix = portIndex < 0 ? string.Empty : $"_{portIndex.ToString(CultureInfo.InvariantCulture)}";
                binding = new GraphValueBinding(
                    $"_graph_{FlowCSharpRuntimeGenerator.SanitizeIdentifier(portName)}{indexSuffix}_{SafeGuid(node.Guid)}_{_graphValueBindings.Count}",
                    type,
                    $"FlowGeneratedRuntimeUtility.GetNodePortDefaultValue<{FlowCSharpRuntimeGenerator.GetFriendlyTypeName(type)}>(graphData, \"{FlowCSharpRuntimeGenerator.Escape(node.Guid)}\", \"{FlowCSharpRuntimeGenerator.Escape(portName)}\", {portIndex.ToString(CultureInfo.InvariantCulture)})");
                _graphValueBindings.Add(key, binding);
                return binding;
            }

            private GraphValueBinding GetOrCreateGraphNodeFieldValueBinding(CeresNode node, string fieldName,
                int fieldIndex, Type type, FieldInfo field)
            {
                if (node == null || string.IsNullOrEmpty(fieldName))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        "Graph node field value can not be generated because the node or field name is missing.");
                }

                if (field == null)
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Graph node field value {node.GetType().Name}.{fieldName} ({node.Guid}) can not be located.");
                }

                EnsureGraphValueTypeIsVisible(node, fieldName, type);
                var key = $"field:{node.Guid}:{fieldName}:{fieldIndex}:{type.FullName}";
                if (_graphValueBindings.TryGetValue(key, out var binding))
                {
                    return binding;
                }

                var indexSuffix = fieldIndex < 0 ? string.Empty : $"_{fieldIndex.ToString(CultureInfo.InvariantCulture)}";
                binding = new GraphValueBinding(
                    $"_graph_{FlowCSharpRuntimeGenerator.SanitizeIdentifier(fieldName)}{indexSuffix}_{SafeGuid(node.Guid)}_{_graphValueBindings.Count}",
                    type,
                    $"FlowGeneratedRuntimeUtility.GetNodeFieldValue<{FlowCSharpRuntimeGenerator.GetFriendlyTypeName(type)}>(graphData, \"{FlowCSharpRuntimeGenerator.Escape(node.Guid)}\", \"{FlowCSharpRuntimeGenerator.Escape(fieldName)}\", {fieldIndex.ToString(CultureInfo.InvariantCulture)})");
                _graphValueBindings.Add(key, binding);
                return binding;
            }

            private static void EnsureGraphValueTypeIsVisible(CeresNode node, string valueName, Type type)
            {
                if (type != null && FlowCSharpRuntimeGenerator.IsVisibleType(type))
                {
                    return;
                }

                throw new FlowCSharpRuntimeGenerationException(
                    $"Graph value {node.GetType().Name}.{valueName} ({node.Guid}) has inaccessible type {type?.FullName ?? "<null>"}.");
            }

            private static FieldInfo GetRequiredNodeField(CeresNode node, string fieldName, Type expectedType)
            {
                if (node == null || string.IsNullOrEmpty(fieldName))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        "Graph node field value can not be generated because the node or field name is missing.");
                }

                var field = GetFieldInHierarchy(node.GetType(), fieldName);
                if (field != null)
                {
                    return field;
                }

                throw new FlowCSharpRuntimeGenerationException(
                    $"Graph node field value {node.GetType().Name}.{fieldName} ({node.Guid}) can not be located for {FlowCSharpRuntimeGenerator.GetFriendlyTypeName(expectedType)}.");
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

            private static object GetIndexedGraphValue(CeresNode node, string fieldName, int fieldIndex,
                object fieldValue, Type expectedType)
            {
                if (fieldValue is System.Collections.IList list &&
                    fieldIndex >= 0 &&
                    fieldIndex < list.Count)
                {
                    return list[fieldIndex];
                }

                throw new FlowCSharpRuntimeGenerationException(
                    $"Graph node field value {node.GetType().Name}.{fieldName}[{fieldIndex.ToString(CultureInfo.InvariantCulture)}] ({node.Guid}) can not be located for {FlowCSharpRuntimeGenerator.GetFriendlyTypeName(expectedType)}.");
            }

            private bool ShouldPersistOutput(CeresNode node, string portId)
            {
                return _context.CompilationGraph.ShouldPersistOutput(node, portId);
            }

            private bool ShouldEmitSyncCancellationChecks()
            {
                return _context.Options.CancellationMode == FlowGeneratedRuntimeCancellationMode.Always;
            }

            private void EmitSyncCancellationCheck()
            {
                if (ShouldEmitSyncCancellationChecks())
                {
                    _currentBlock.AddRaw("cancellation.ThrowIfCancellationRequested();");
                }
            }

            private CsExpression BuildGetPropertyExpression(PropertyNode_PropertyValue node)
            {
                if (!FlowCSharpRuntimeGenerator.CanDirectGetProperty(node, out var propertyCall))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Property getter node {node.Guid} can not be generated as a direct call.");
                }

                var target = BuildPropertyTargetExpression(node, propertyCall);
                var accessTarget = node.isStatic ? target : $"({target})";
                return new CsExpression($"{accessTarget}.{EscapeIdentifier(propertyCall.PropertyName)}",
                    propertyCall.PropertyType, CsExpressionPurity.CachePerEvent, false, true);
            }

            private string BuildPropertyTargetExpression(PropertyNode_PropertyValue node,
                FlowCSharpRuntimeGenerator.PropertyCallInfo propertyCall)
            {
                if (node.isStatic)
                {
                    return FlowCSharpRuntimeGenerator.GetFriendlyTypeName(propertyCall.DeclaringType);
                }

                var targetExpression = LowerInput(node, "target", propertyCall.TargetType).Code;
                return BuildTargetOrDefaultExpression(
                    node,
                    "target",
                    propertyCall.TargetType,
                    node.isSelfTarget,
                    false,
                    targetExpression).Code;
            }

            private CsExpression BuildSelfTargetArgumentExpression(FlowNode_ExecuteFunction node,
                string propertyName, Type targetType, CsExpression inputExpression)
            {
                return BuildTargetOrDefaultExpression(node, propertyName, targetType, true, true,
                    inputExpression.Code, inputExpression);
            }

            private CsExpression BuildTargetOrDefaultExpression(CeresNode node, string propertyName,
                Type targetType, bool isSelfTarget, bool useSelfTargetUtility,
                string inputCode = null, CsExpression inputExpression = null)
            {
                inputExpression ??= LowerInput(node, propertyName, targetType);
                inputCode ??= inputExpression.Code;
                if (!isSelfTarget)
                {
                    return inputExpression;
                }

                if (CanUseContextSelfTarget(node, propertyName, -1, targetType))
                {
                    return new CsExpression(
                        $"contextObject as {FlowCSharpRuntimeGenerator.GetFriendlyTypeName(targetType)}",
                        targetType,
                        CsExpressionPurity.CachePerEvent,
                        true,
                        false);
                }

                var typeName = FlowCSharpRuntimeGenerator.GetFriendlyTypeName(targetType);
                var code = useSelfTargetUtility
                    ? $"FlowGeneratedRuntimeUtility.GetSelfTargetOrDefault<{typeName}>(true, {inputCode}, contextObject)"
                    : $"FlowGeneratedRuntimeUtility.GetTargetOrDefault<{typeName}>(false, true, {inputCode}, contextObject)";
                return new CsExpression(code, targetType, inputExpression.Purity, false, true);
            }

            private bool CanUseContextSelfTarget(CeresNode node, string propertyName, int arrayIndex, Type targetType)
            {
                return CanCastContextAs(targetType) &&
                       IsUnconnectedNullInput(node, propertyName, arrayIndex);
            }

            private bool IsUnconnectedNullInput(CeresNode node, string propertyName, int arrayIndex)
            {
                if (_context.CompilationGraph.TryGetInputConnection(node, propertyName, arrayIndex, out _))
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

            private static CsExpression BuildSelfReferenceExpression(PropertyNode node, Type targetType)
            {
                return new CsExpression(
                    $"({FlowCSharpRuntimeGenerator.GetFriendlyTypeName(targetType)})contextObject", targetType);
            }

            private CsExpression BuildGetSharedVariableExpression(PropertyNode_SharedVariableValue node)
            {
                if (!FlowCSharpRuntimeGenerator.CanAccessSharedVariable(node, out var variableType,
                        out var variableValueType, out var portValueType))
                {
                    throw new FlowCSharpRuntimeGenerationException(
                        $"Shared variable getter node {node.Guid} can not be generated.");
                }

                var binding = GetGraphVariableBinding(node.propertyName, variableType, variableValueType);
                if (binding.IsLocal)
                {
                    return new CsExpression(CastExpression(binding.FieldName, binding.StorageType, portValueType),
                        portValueType, CsExpressionPurity.CachePerEvent, true, false);
                }

                var sharedValue = $"{binding.FieldName}.Value";
                return new CsExpression(
                    $"({binding.FieldName} != null ? {CastExpression(sharedValue, binding.StorageType, portValueType)} : default({FlowCSharpRuntimeGenerator.GetFriendlyTypeName(portValueType)}))",
                    portValueType, CsExpressionPurity.CachePerEvent, false, true);
            }

            private GraphVariableBinding GetGraphVariableBinding(string variableName, Type variableType,
                Type variableValueType)
            {
                var key = $"{variableType.FullName}:{variableName}";
                if (_graphVariableBindings.TryGetValue(key, out var binding))
                {
                    return binding;
                }

                if (TryCreateLocalGraphVariableBinding(variableName, variableType, variableValueType,
                        out var localBinding))
                {
                    binding = GraphVariableBinding.FromLocal(localBinding);
                }
                else
                {
                    binding = GraphVariableBinding.FromShared(GetOrCreateSharedVariableBinding(variableName,
                        variableType));
                }

                _graphVariableBindings.Add(key, binding);
                return binding;
            }

            private bool TryCreateLocalGraphVariableBinding(string variableName, Type variableType,
                Type variableValueType, out LocalVariableBinding binding)
            {
                binding = default;
                if (_context.Options.VariableStorageMode != FlowGeneratedRuntimeVariableStorageMode.LocalFieldsForUnshared ||
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
                    $"_local_{FlowCSharpRuntimeGenerator.SanitizeIdentifier(variableName)}_{_localVariableBindings.Count}",
                    storageType,
                    $"FlowGeneratedRuntimeUtility.GetLocalVariableValue<{FlowCSharpRuntimeGenerator.GetFriendlyTypeName(variableType)}, {FlowCSharpRuntimeGenerator.GetFriendlyTypeName(storageType)}>(graphData, \"{FlowCSharpRuntimeGenerator.Escape(variableName)}\")");
                _localVariableBindings.Add(key, binding);
                return true;
            }

            private bool TryFindGraphVariable(string variableName, Type variableType, out SharedVariable variable)
            {
                variable = null;
                if (string.IsNullOrEmpty(variableName) || _context.Graph.variables == null)
                {
                    return false;
                }

                foreach (var candidate in _context.Graph.variables)
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
                if (variable == null || variableValueType == null ||
                    !FlowCSharpRuntimeGenerator.IsVisibleType(variableValueType))
                {
                    return false;
                }

                var resolvedType = variable.GetValueType();
                if (resolvedType != null &&
                    resolvedType != typeof(object) &&
                    resolvedType.IsAssignableTo(variableValueType) &&
                    FlowCSharpRuntimeGenerator.IsVisibleType(resolvedType))
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
                    $"_shared_{FlowCSharpRuntimeGenerator.SanitizeIdentifier(variableName)}_{_sharedVariableBindings.Count}");
                _sharedVariableBindings.Add(key, binding);
                return binding;
            }

            private bool TryGetOutputType(CeresNode node, string portId, out Type outputType)
            {
                outputType = null;
                if (node is FlowNode_ExecuteFunctionReturn functionNode &&
                    portId == "output" &&
                    FlowCSharpRuntimeGenerator.CanGeneratedCall(functionNode, out var directCall) &&
                    directCall.ReturnType != typeof(void))
                {
                    outputType = GetFunctionReturnOutputType(functionNode, directCall);
                    return true;
                }

                if (node is PropertyNode_PropertyValue propertyNode &&
                    portId == "outputValue" &&
                    FlowCSharpRuntimeGenerator.CanDirectGetProperty(propertyNode, out var propertyCall))
                {
                    outputType = propertyCall.PropertyType;
                    return true;
                }

                if (node is PropertyNode_SharedVariableValue sharedVariableNode &&
                    portId == "outputValue" &&
                    FlowCSharpRuntimeGenerator.CanAccessSharedVariable(sharedVariableNode, out _, out var valueType))
                {
                    outputType = valueType;
                    return true;
                }

                if (node is PropertyNode selfReferenceNode &&
                    portId == "outputValue" &&
                    FlowCSharpRuntimeGenerator.TryGetSelfReferenceTargetType(selfReferenceNode, out var targetType))
                {
                    outputType = targetType;
                    return true;
                }

                outputType = node.NodeData.FindPortData(portId)?.GetValueType();
                return outputType != null && outputType != typeof(NodeReference);
            }

            private Type GetFunctionReturnOutputType(FlowNode_ExecuteFunctionReturn node,
                FlowCSharpRuntimeGenerator.DirectCallInfo directCall)
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

            private bool TryGetResolveReturnSelectedType(FlowNode_ExecuteFunction node,
                FlowCSharpRuntimeGenerator.DirectCallInfo directCall, out Type selectedType)
            {
                selectedType = null;
                if (!TryGetResolveReturnParameterIndex(directCall, out var parameterIndex))
                {
                    return false;
                }

                if (IsFunctionParameterConnected(node, parameterIndex))
                {
                    return false;
                }

                var portData = GetFunctionParameterPortData(node, parameterIndex);
                if (portData?.GetPort(node)?.GetValue() is not SerializedTypeBase serializedType)
                {
                    return false;
                }

                selectedType = serializedType.GetObjectType();
                return selectedType != null;
            }

            private static bool TryGetResolveReturnParameterIndex(
                FlowCSharpRuntimeGenerator.DirectCallInfo directCall, out int parameterIndex)
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

            private bool TryGetDirectSerializedTypeArgument(FlowNode_ExecuteFunction node,
                FlowCSharpRuntimeGenerator.DirectCallInfo directCall, out DirectSerializedTypeArgument argument)
            {
                argument = default;
                if (_context.Options.SerializedTypeMode != FlowGeneratedRuntimeSerializedTypeMode.DirectType ||
                    directCall.UseInvoker ||
                    !TryGetResolveReturnParameterIndex(directCall, out var parameterIndex) ||
                    parameterIndex < 0 ||
                    parameterIndex >= directCall.ParameterTypes.Length ||
                    !IsSerializedType(directCall.ParameterTypes[parameterIndex]) ||
                    !TryGetSerializedTypeConstraint(directCall.ParameterTypes[parameterIndex], out var constraintType))
                {
                    return false;
                }

                if (IsFunctionParameterConnected(node, parameterIndex))
                {
                    return false;
                }

                var portData = GetFunctionParameterPortData(node, parameterIndex);
                if (portData?.GetPort(node)?.GetValue() is not SerializedTypeBase serializedType)
                {
                    return false;
                }

                var selectedType = serializedType.GetObjectType();
                if (selectedType == null ||
                    !selectedType.IsAssignableTo(constraintType) ||
                    !FlowCSharpRuntimeGenerator.IsVisibleType(selectedType))
                {
                    return false;
                }

                argument = new DirectSerializedTypeArgument(parameterIndex, selectedType);
                return true;
            }

            private static CeresPortData GetFunctionParameterPortData(FlowNode_ExecuteFunction node,
                int parameterIndex)
            {
                return node is FlowNode_ExecuteFunctionUber
                    ? node.NodeData.FindPortData("inputs", parameterIndex)
                    : node.NodeData.FindPortData($"input{parameterIndex + 1}");
            }

            private bool IsFunctionParameterConnected(FlowNode_ExecuteFunction node, int parameterIndex)
            {
                var portData = GetFunctionParameterPortData(node, parameterIndex);
                if (portData == null)
                {
                    return false;
                }

                var arrayIndex = node is FlowNode_ExecuteFunctionUber ? portData.arrayIndex : -1;
                return _context.CompilationGraph.TryGetInputConnection(node, portData.propertyName, arrayIndex,
                    out _);
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

            private static bool CanBuildDirectSerializedTypeFunctionExpression(
                FlowCSharpRuntimeGenerator.DirectCallInfo directCall,
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

            private static bool TryBuildDirectSerializedTypeFunctionExpression(
                FlowCSharpRuntimeGenerator.DirectCallInfo directCall,
                DirectSerializedTypeArgument typeArgument, IReadOnlyList<string> arguments, out string expression)
            {
                expression = null;
                var typeName = FlowCSharpRuntimeGenerator.GetFriendlyTypeName(typeArgument.SelectedType);

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

            private bool IsImpureFunction(FlowNode_ExecuteFunctionReturn node)
            {
                if (!FlowCSharpRuntimeGenerator.CanGeneratedCall(node, out var directCall))
                {
                    return true;
                }

                return FlowExpressionClassifier.ClassifyFunction(directCall) == CsExpressionPurity.Impure;
            }

            private static CsExpression CastExpression(CsExpression expression, Type expectedType)
            {
                return new CsExpression(CastExpression(expression.Code, expression.Type, expectedType),
                    expectedType, expression.Purity, expression.CanInline, expression.RequiresMaterialization);
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
                    return $"FlowGeneratedRuntimeUtility.CastArray<{FlowCSharpRuntimeGenerator.GetFriendlyTypeName(expectedType.GetElementType())}>({expression})";
                }

                return $"({FlowCSharpRuntimeGenerator.GetFriendlyTypeName(expectedType)})({expression})";
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

            private static bool IsNullLiteralValue(object value)
            {
                return FlowCSharpRuntimeGenerator.IsNullLiteralValue(value);
            }

            private static string EscapeIdentifier(string value)
            {
                return CSharpKeywords.Contains(value) ? $"@{value}" : value;
            }

            private static string SafeGuid(string guid)
            {
                return guid.Replace("-", "_");
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
        }

        private readonly struct MaterializedValue
        {
            public readonly string LocalName;

            public readonly Type Type;

            public readonly CsExpressionPurity Purity;

            public MaterializedValue(string localName, Type type, CsExpressionPurity purity)
            {
                LocalName = localName;
                Type = type;
                Purity = purity;
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

        private readonly struct PersistentOutputBinding
        {
            public readonly string FieldName;

            public readonly Type Type;

            public readonly string InitializerExpression;

            public PersistentOutputBinding(string fieldName, Type type, string initializerExpression)
            {
                FieldName = fieldName;
                Type = type;
                InitializerExpression = initializerExpression;
            }
        }

        private readonly struct GraphValueBinding
        {
            public readonly string FieldName;

            public readonly Type Type;

            public readonly string InitializerExpression;

            public GraphValueBinding(string fieldName, Type type, string initializerExpression)
            {
                FieldName = fieldName;
                Type = type;
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
    }
}
