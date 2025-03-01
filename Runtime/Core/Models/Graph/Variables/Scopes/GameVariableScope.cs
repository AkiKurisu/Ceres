using System.Collections.Generic;
using UnityEngine;
namespace Ceres.Graph
{
    /// <summary>
    /// Component contains <see cref="SharedVariable"/> in game lifetime scope
    /// </summary>
    [CreateAssetMenu(fileName = "GameVariableScope", menuName = "Ceres/GameVariableScope")]
    public class GameVariableScope : ScriptableObject, IVariableScope, IVariableSource
    {
        private static readonly Stack<GameVariableScope> InitializationStack = new();
        
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

        private void Initialize()
        {
            if (InitializationStack.Contains(this))
            {
                CeresLogger.LogError("Circulating initialization occurs!");
                return;
            }
            _initialized = true;
            InitializationStack.Push(this);
            if (parentScope && parentScope.IsCurrentScope())
            {
                GlobalVariables = new GlobalVariables(sharedVariables, parentScope);
            }
            else
            {
                GlobalVariables = new GlobalVariables(sharedVariables);
            }
            InitializationStack.TryPop(out _);
        }
        
        public bool IsCurrentScope()
        {
            if (!_initialized) Initialize();
            return GlobalVariables.Instance == GlobalVariables;
        }
        
        private void OnDestroy()
        {
            GlobalVariables.Dispose();
        }
    }
}