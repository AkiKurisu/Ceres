using System;
using Ceres.Graph.Flow;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal sealed class FlowCSharpCompiler
    {
        private readonly FlowCompilationContext _context;

        public FlowCSharpCompiler(FlowCompilationContext context)
        {
            _context = context;
        }

        public FlowCSharpCompilationResult Compile()
        {
            ValidateSupport();

            if (TryCompileOptimizedIr(out var result))
            {
                return result;
            }

            return CompileLegacy();
        }

        public FlowGeneratedFunctionDependencyInfo[] ValidateSupport()
        {
            var legacy = new FlowCSharpRuntimeGenerator.SourceContext(_context.Graph, "Validation");
            legacy.ValidateSupport(_context.DisplayName);
            return legacy.GetFunctionDependencies();
        }

        private bool TryCompileOptimizedIr(out FlowCSharpCompilationResult result)
        {
            result = null;
            if (!_context.Options.UsesGeneratedRuntimeProgram ||
                _context.Options.Profile == FlowGeneratedRuntimeProfile.Debuggable)
            {
                return false;
            }

            var unit = FlowSyncIrCompiler.TryCompile(_context);
            if (unit == null)
            {
                return false;
            }

            result = new FlowCSharpCompilationResult(
                CSharpIrEmitter.Emit(unit),
                Array.Empty<FlowGeneratedFunctionDependencyInfo>(),
                false);
            return true;
        }

        private FlowCSharpCompilationResult CompileLegacy()
        {
            var legacy = new FlowCSharpRuntimeGenerator.SourceContext(_context.Graph, _context.ClassName);
            var source = legacy.Generate();
            return new FlowCSharpCompilationResult(source, legacy.GetFunctionDependencies(), true);
        }
    }
}
