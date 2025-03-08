using System;
using UnityEngine.Assertions;
using UnityEngine.Pool;

namespace Ceres.Graph
{
    /// <summary>
    /// Ceres graph compilation context
    /// </summary>
    public interface ICeresGraphCompilationContext
    {
        void PreCompileGraph(CeresGraph source);
        
        void PostCompileGraph(CeresGraph source);
    }
    
    /// <summary>
    /// Ceres graph compiler
    /// </summary>
    public sealed class CeresGraphCompiler: IDisposable
    {
        private static readonly ObjectPool<CeresGraphCompiler> Pool = new(() => new CeresGraphCompiler());
        
        /// <summary>
        /// Compilation source graph, will not change during compilation
        /// </summary>
        public CeresGraph Source { get; private set; }
        
        /// <summary>
        /// Compilation target graph
        /// </summary>
        public CeresGraph Target { get; internal set; }
        
        public ICeresGraphCompilationContext Context { get; set; }
        
        private bool _isPooled;
        
        private CeresGraphCompiler() { }
        
        public static CeresGraphCompiler GetPooled(CeresGraph graph, ICeresGraphCompilationContext context = null)
        {
            Assert.IsNotNull(graph);
            var compileContext = Pool.Get();
            compileContext.Context = context;
            compileContext.Source = graph;
            compileContext.Target = graph;
            compileContext._isPooled = true;
            context?.PreCompileGraph(graph);
            return compileContext;
        }

        public void Dispose()
        {
            Context?.PostCompileGraph(Source);
            Target = null;
            Source = null;
            Context = null;
            if (!_isPooled) return;
            
            _isPooled = false;
            Pool.Release(this);
        }
    }
}