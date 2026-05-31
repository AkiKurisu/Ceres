using System;
using System.Collections.Generic;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Properties;
using Ceres.Graph.Flow.Utilities;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal sealed class FlowLoweringContext
    {
        internal delegate CsExpression BuildFunctionCallExpressionHandler(FlowNode_ExecuteFunction node,
            out FlowCSharpRuntimeGenerator.DirectCallInfo directCall);

        private readonly CsBlock _block;

        private readonly Func<CsBlock> _getBlock;

        private readonly Func<CeresNode, string, Type, CsExpression> _lowerInput;

        private readonly Action<FlowConnection> _lowerForwardConnection;

        private readonly Action<CeresNode> _lowerDefaultNext;

        private readonly Action<FlowNode_Branch> _lowerBranch;

        private readonly BuildFunctionCallExpressionHandler _buildFunctionCallExpression;

        private readonly Func<FlowNode_ExecuteFunctionReturn, CsExpression> _buildFunctionReturnExpression;

        private readonly Action<FlowNode_ExecuteFunctionReturn> _materializeFunctionReturnIfNeeded;

        private readonly Action<PropertyNode_PropertyValue> _lowerSetProperty;

        private readonly Action<PropertyNode_SharedVariableValue> _lowerSetSharedVariable;

        private readonly Func<PropertyNode_PropertyValue, CsExpression> _buildGetPropertyExpression;

        private readonly Func<PropertyNode, Type, CsExpression> _buildSelfReferenceExpression;

        private readonly Func<PropertyNode_SharedVariableValue, CsExpression> _buildGetSharedVariableExpression;

        public FlowCompilationContext Compilation { get; }

        public CsBlock Block => _getBlock?.Invoke() ?? _block;

        public string ContextObjectExpression { get; }

        public string EventBaseExpression { get; }

        public FlowLoweringContext(FlowCompilationContext compilation, CsBlock block,
            string contextObjectExpression = "contextObject", string eventBaseExpression = "evtBase")
        {
            Compilation = compilation;
            _block = block;
            ContextObjectExpression = contextObjectExpression;
            EventBaseExpression = eventBaseExpression;
        }

        private FlowLoweringContext(FlowCompilationContext compilation, Func<CsBlock> getBlock,
            Func<CeresNode, string, Type, CsExpression> lowerInput,
            Action<FlowConnection> lowerForwardConnection, Action<CeresNode> lowerDefaultNext,
            Action<FlowNode_Branch> lowerBranch,
            BuildFunctionCallExpressionHandler buildFunctionCallExpression,
            Func<FlowNode_ExecuteFunctionReturn, CsExpression> buildFunctionReturnExpression,
            Action<FlowNode_ExecuteFunctionReturn> materializeFunctionReturnIfNeeded,
            Action<PropertyNode_PropertyValue> lowerSetProperty,
            Action<PropertyNode_SharedVariableValue> lowerSetSharedVariable,
            Func<PropertyNode_PropertyValue, CsExpression> buildGetPropertyExpression,
            Func<PropertyNode, Type, CsExpression> buildSelfReferenceExpression,
            Func<PropertyNode_SharedVariableValue, CsExpression> buildGetSharedVariableExpression,
            string contextObjectExpression = "contextObject", string eventBaseExpression = "evtBase")
        {
            Compilation = compilation;
            _getBlock = getBlock;
            _lowerInput = lowerInput;
            _lowerForwardConnection = lowerForwardConnection;
            _lowerDefaultNext = lowerDefaultNext;
            _lowerBranch = lowerBranch;
            _buildFunctionCallExpression = buildFunctionCallExpression;
            _buildFunctionReturnExpression = buildFunctionReturnExpression;
            _materializeFunctionReturnIfNeeded = materializeFunctionReturnIfNeeded;
            _lowerSetProperty = lowerSetProperty;
            _lowerSetSharedVariable = lowerSetSharedVariable;
            _buildGetPropertyExpression = buildGetPropertyExpression;
            _buildSelfReferenceExpression = buildSelfReferenceExpression;
            _buildGetSharedVariableExpression = buildGetSharedVariableExpression;
            ContextObjectExpression = contextObjectExpression;
            EventBaseExpression = eventBaseExpression;
        }

        internal static FlowLoweringContext CreateSync(FlowCompilationContext compilation, Func<CsBlock> getBlock,
            Func<CeresNode, string, Type, CsExpression> lowerInput,
            Action<FlowConnection> lowerForwardConnection, Action<CeresNode> lowerDefaultNext,
            Action<FlowNode_Branch> lowerBranch,
            BuildFunctionCallExpressionHandler buildFunctionCallExpression,
            Func<FlowNode_ExecuteFunctionReturn, CsExpression> buildFunctionReturnExpression,
            Action<FlowNode_ExecuteFunctionReturn> materializeFunctionReturnIfNeeded,
            Action<PropertyNode_PropertyValue> lowerSetProperty,
            Action<PropertyNode_SharedVariableValue> lowerSetSharedVariable,
            Func<PropertyNode_PropertyValue, CsExpression> buildGetPropertyExpression,
            Func<PropertyNode, Type, CsExpression> buildSelfReferenceExpression,
            Func<PropertyNode_SharedVariableValue, CsExpression> buildGetSharedVariableExpression)
        {
            return new FlowLoweringContext(compilation, getBlock, lowerInput, lowerForwardConnection,
                lowerDefaultNext, lowerBranch, buildFunctionCallExpression, buildFunctionReturnExpression,
                materializeFunctionReturnIfNeeded, lowerSetProperty, lowerSetSharedVariable,
                buildGetPropertyExpression, buildSelfReferenceExpression, buildGetSharedVariableExpression);
        }

        public FlowConnection GetExecConnection(CeresNode node, string propertyName)
        {
            return Compilation.CompilationGraph.GetExecConnection(node, propertyName);
        }

        public FlowConnection GetExecConnection(CeresNode node, string propertyName, int arrayIndex)
        {
            return Compilation.CompilationGraph.GetExecConnection(node, propertyName, arrayIndex);
        }

        public IEnumerable<FlowConnection> GetExecConnections(CeresNode node, string propertyName)
        {
            return Compilation.CompilationGraph.GetExecConnections(node, propertyName);
        }

        public bool TryGetInputConnection(CeresNode node, string propertyName, out FlowConnection connection)
        {
            return Compilation.CompilationGraph.TryGetInputConnection(node, propertyName, out connection);
        }

        public CsExpression LowerInput(CeresNode node, string propertyName, Type expectedType)
        {
            if (_lowerInput == null)
            {
                throw new FlowCSharpRuntimeGenerationException("This lowering context can not lower input values.");
            }

            return _lowerInput(node, propertyName, expectedType);
        }

        public void LowerForwardConnection(FlowConnection connection)
        {
            if (_lowerForwardConnection == null)
            {
                throw new FlowCSharpRuntimeGenerationException("This lowering context can not lower forward edges.");
            }

            _lowerForwardConnection(connection);
        }

        public void LowerDefaultNext(CeresNode node)
        {
            if (_lowerDefaultNext == null)
            {
                throw new FlowCSharpRuntimeGenerationException("This lowering context can not lower forward edges.");
            }

            _lowerDefaultNext(node);
        }

        public void Add(CsStatement statement)
        {
            Block.Add(statement);
        }

        public void AddRaw(string code)
        {
            Block.AddRaw(code);
        }

        public void LowerBranch(FlowNode_Branch node)
        {
            if (_lowerBranch == null)
            {
                throw new FlowCSharpRuntimeGenerationException("This lowering context can not lower branches.");
            }

            _lowerBranch(node);
        }

        public CsExpression BuildFunctionCallExpression(FlowNode_ExecuteFunction node,
            out FlowCSharpRuntimeGenerator.DirectCallInfo directCall)
        {
            directCall = default;
            if (_buildFunctionCallExpression == null)
            {
                throw new FlowCSharpRuntimeGenerationException("This lowering context can not lower function calls.");
            }

            return _buildFunctionCallExpression(node, out directCall);
        }

        public CsExpression BuildFunctionReturnExpression(FlowNode_ExecuteFunctionReturn node)
        {
            if (_buildFunctionReturnExpression == null)
            {
                throw new FlowCSharpRuntimeGenerationException("This lowering context can not lower function returns.");
            }

            return _buildFunctionReturnExpression(node);
        }

        public void MaterializeFunctionReturnIfNeeded(FlowNode_ExecuteFunctionReturn node)
        {
            if (_materializeFunctionReturnIfNeeded == null)
            {
                throw new FlowCSharpRuntimeGenerationException("This lowering context can not materialize function returns.");
            }

            _materializeFunctionReturnIfNeeded(node);
        }

        public void LowerSetProperty(PropertyNode_PropertyValue node)
        {
            if (_lowerSetProperty == null)
            {
                throw new FlowCSharpRuntimeGenerationException("This lowering context can not lower property setters.");
            }

            _lowerSetProperty(node);
        }

        public void LowerSetSharedVariable(PropertyNode_SharedVariableValue node)
        {
            if (_lowerSetSharedVariable == null)
            {
                throw new FlowCSharpRuntimeGenerationException("This lowering context can not lower shared variable setters.");
            }

            _lowerSetSharedVariable(node);
        }

        public CsExpression BuildGetPropertyExpression(PropertyNode_PropertyValue node)
        {
            if (_buildGetPropertyExpression == null)
            {
                throw new FlowCSharpRuntimeGenerationException("This lowering context can not lower property getters.");
            }

            return _buildGetPropertyExpression(node);
        }

        public CsExpression BuildSelfReferenceExpression(PropertyNode node, Type targetType)
        {
            if (_buildSelfReferenceExpression == null)
            {
                throw new FlowCSharpRuntimeGenerationException("This lowering context can not lower self references.");
            }

            return _buildSelfReferenceExpression(node, targetType);
        }

        public CsExpression BuildGetSharedVariableExpression(PropertyNode_SharedVariableValue node)
        {
            if (_buildGetSharedVariableExpression == null)
            {
                throw new FlowCSharpRuntimeGenerationException("This lowering context can not lower shared variable getters.");
            }

            return _buildGetSharedVariableExpression(node);
        }

        public void AddUnsupported(CeresNode node, string reason)
        {
            Compilation.AddDiagnostic(new FlowCompilationDiagnostic(FlowCompilationDiagnosticSeverity.Warning,
                reason, node?.Guid));
        }

        public static string GetFriendlyTypeName(Type type)
        {
            return FlowCSharpRuntimeGenerator.GetFriendlyTypeName(type);
        }
    }
}
