using System.Collections.Generic;
using UnityEngine;
namespace Ceres
{
    public class SceneVariableScope : MonoBehaviour, IVariableScope, IVariableSource
    {
        [SerializeReference]
        private List<SharedVariable> sharedVariables = new();
        
        public List<SharedVariable> SharedVariables => sharedVariables;
        
        [SerializeField]
        private GameVariableScope parentScope;
        
        public GlobalVariables GlobalVariables { get; private set; }
        
        private bool _initialized;
        
        private void Awake()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }
        
        public void Initialize()
        {
            _initialized = true;
            if (parentScope && parentScope.IsCurrentScope())
            {
                GlobalVariables = new GlobalVariables(sharedVariables, parentScope);
            }
            else
            {
                GlobalVariables = new GlobalVariables(sharedVariables);
            }
        }
        
        private void OnDestroy()
        {
            GlobalVariables.Dispose();
        }
    }
}