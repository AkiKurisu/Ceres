using System.Collections.Generic;
using Chris.Serialization;
using UnityEngine;
using UObject = UnityEngine.Object;
namespace Ceres.Graph
{
    /// <summary>
    /// A medium for centralized storage and exchange of graph data
    /// </summary>
    public class BlackBoard : IVariableSource
    {
        public List<SharedVariable> SharedVariables { get; } = new();
        
        /// <summary>
        /// Create a BlackBoard 
        /// </summary>
        /// <param name="variables">Variables to add</param>
        /// <param name="clone">Whether clone source variable</param>
        /// <returns></returns>
        public static BlackBoard Create(List<SharedVariable> variables = null, bool clone = true)
        {
            var blackBoard = new BlackBoard();
            if (variables != null)
            {
                foreach (var variable in variables)
                {
                    blackBoard.SharedVariables.Add(clone ? variable.Clone() : variable);
                }
            }
            return blackBoard;
        }
        
        public SharedVariable<float> SetFloat(string key, float value)
        {
            if (!this.TryGetSharedVariable(key, out SharedVariable<float> variable))
            {
                variable = new SharedFloat() { Name = key };
                SharedVariables.Add(variable);
            }
            variable.Value = value;
            return variable;
        }

        public SharedVariable<int> SetInt(string key, int value)
        {
            if (!this.TryGetSharedVariable(key, out SharedVariable<int> variable))
            {
                variable = new SharedInt() { Name = key };
                SharedVariables.Add(variable);
            }
            variable.Value = value;
            return variable;
        }

        public SharedVariable<Vector3> SetVector3(string key, Vector3 value)
        {
            if (!this.TryGetSharedVariable(key, out SharedVariable<Vector3> variable))
            {
                variable = new SharedVector3() { Name = key };
                SharedVariables.Add(variable);
            }
            variable.Value = value;
            return variable;
        }
        
        public SharedVariable<Vector3Int> SetVector3Int(string key, Vector3Int value)
        {
            if (!this.TryGetSharedVariable(key, out SharedVariable<Vector3Int> variable))
            {
                variable = new SharedVector3Int() { Name = key };
                SharedVariables.Add(variable);
            }
            variable.Value = value;
            return variable;
        }
        
        public SharedVariable<Vector2> SetVector2(string key, Vector2 value)
        {
            if (!this.TryGetSharedVariable(key, out SharedVariable<Vector2> variable))
            {
                variable = new SharedVector2 { Name = key };
                SharedVariables.Add(variable);
            }
            variable.Value = value;
            return variable;
        }
        
        public SharedVariable<Vector2Int> SetVector2Int(string key, Vector2Int value)
        {
            if (!this.TryGetSharedVariable(key, out SharedVariable<Vector2Int> variable))
            {
                variable = new SharedVector2Int { Name = key };
                SharedVariables.Add(variable);
            }
            variable.Value = value;
            return variable;
        }
        
        public SharedVariable<bool> SetBool(string key, bool value)
        {
            if (!this.TryGetSharedVariable(key, out SharedVariable<bool> variable))
            {
                variable = new SharedBool { Name = key };
                SharedVariables.Add(variable);
            }
            variable.Value = value;
            return variable;
        }
        
        public SharedVariable<string> SetString(string key, string value)
        {
            if (!this.TryGetSharedString(key, out var variable))
            {
                variable = new SharedString() { Name = key };
                SharedVariables.Add(variable);
            }
            variable.Value = value;
            return variable;
        }
        
        public SharedVariable<UObject> SetUObject(string key, UObject value)
        {
            if (!this.TryGetSharedUObject(key, out var variable))
            {
                variable = new SharedUObject() { Name = key };
                SharedVariables.Add(variable);
            }
            variable.Value = value;
            return variable;
        }
        
        public SharedVariable<UObject> SetUObject<T>(string key, T value) where T : UObject
        {
            if (!this.TryGetSharedUObject(key, out var variable))
            {
                variable = new SharedUObject() { Name = key, serializedType = SerializedType<UObject>.FromType(typeof(T)) };
                SharedVariables.Add(variable);
            }
            variable.Value = value;
            return variable;
        }
        
        public SharedVariable<object> SetObject(string key, object value)
        {
            if (!this.TryGetSharedObject(key, out var variable))
            {
                variable = new SharedObject { Name = key, serializedObject = SerializedObjectBase.FromObject(value)};
                SharedVariables.Add(variable);
            }
            variable.Value = value;
            return variable;
        }
    }
}