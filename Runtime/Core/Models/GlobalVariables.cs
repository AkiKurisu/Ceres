using System;
using System.Collections.Generic;
using UObject = UnityEngine.Object;
namespace Ceres
{
    /// <summary>
    /// Global variables are variables managed by a variable scope and any graph initialized in this scope
    /// will map global variable <see cref="SharedVariable.IsGlobal"/> to it
    /// </summary>
    public class GlobalVariables : IVariableSource, IDisposable
    {
        public List<SharedVariable> SharedVariables { get; }
        
        private static GlobalVariables _instance;
        
        public static GlobalVariables Instance => _instance ?? FindOrCreateDefault();
        
        private readonly IVariableScope _parentScope;
        
        public GlobalVariables(List<SharedVariable> sharedVariables)
        {
            _instance = this;
            SharedVariables = new List<SharedVariable>(sharedVariables);
        }
        
        public GlobalVariables(List<SharedVariable> sharedVariables, IVariableScope parentScope)
        {
            _instance = this;
            _parentScope = parentScope;
            SharedVariables = new List<SharedVariable>(sharedVariables);
            if (parentScope != null)
            {
                sharedVariables.AddRange(parentScope.GlobalVariables.SharedVariables);
            }
        }
        
        private static GlobalVariables FindOrCreateDefault()
        {
            var scope = UObject.FindObjectOfType<SceneVariableScope>();
            if (scope != null)
            {
                scope.Initialize();
                return scope.GlobalVariables;
            }
            _instance = new GlobalVariables(new List<SharedVariable>());
            return _instance;
        }
        
        public void Dispose()
        {
            if (_instance != this)
            {
                CeresAPI.LogWarning("Global variables can only be disposed in top level scope");
                return;
            }
            _instance = null;
            if (_parentScope != null)
            {
                _instance = _parentScope.GlobalVariables;
            }
        }
    }
}