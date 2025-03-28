using UnityEngine;
namespace Ceres.Graph
{
    public static class SharedVariableExtension
    {
        /// <summary>
        /// Get <see cref="SharedVariable"/> by name
        /// </summary>
        /// <param name="variableScope"></param>
        /// <param name="variableName"></param>
        /// <returns></returns>
        public static SharedVariable GetSharedVariable(this IVariableSource variableScope, string variableName)
        {
            if (string.IsNullOrEmpty(variableName))
            {
                return null;
            }
            foreach (var variable in variableScope.SharedVariables)
            {
                if (variable.Name.Equals(variableName))
                {
                    return variable;
                }
            }
            return null;
        }
        
        /// <summary>
        /// Get <see cref="SharedVariable{T}"/> by name
        /// </summary>
        /// <param name="variableScope"></param>
        /// <param name="variableName"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static SharedVariable<T> GetSharedVariable<T>(this IVariableSource variableScope, string variableName)
        {
            return GetSharedVariable(variableScope, variableName) as SharedVariable<T>;
        }
        
        /// <summary>
        /// Try get shared variable by its name
        /// </summary>
        /// <param name="variableScope"></param>
        /// <param name="variableName"></param>
        /// <param name="sharedVariable"></param>
        /// <returns></returns>
        public static bool TryGetSharedVariable(this IVariableSource variableScope, string variableName, out SharedVariable sharedVariable)
        {
            if (string.IsNullOrEmpty(variableName))
            {
                sharedVariable = null;
                return false;
            }
            foreach (var variable in variableScope.SharedVariables)
            {
                if (variable.Name.Equals(variableName))
                {
                    sharedVariable = variable;
                    return true;
                }
            }
            sharedVariable = null;
            return false;
        }
        
        public static bool TryGetSharedVariable<T>(this IVariableSource variableScope, string variableName, out SharedVariable<T> sharedTVariable) where T : unmanaged
        {
            if (variableScope.TryGetSharedVariable(variableName, out var sharedVariable))
            {
                sharedTVariable = sharedVariable as SharedVariable<T>;
                return sharedTVariable != null;
            }
            sharedTVariable = null;
            return false;
        }
        
        public static bool TryGetSharedString(this IVariableSource variableScope, string variableName, out SharedVariable<string> sharedTVariable)
        {
            if (variableScope.TryGetSharedVariable(variableName, out var sharedVariable))
            {
                sharedTVariable = sharedVariable as SharedVariable<string>;
                return sharedTVariable != null;
            }
            sharedTVariable = null;
            return false;
        }
        
        public static bool TryGetSharedUObject(this IVariableSource variableScope, string variableName, out SharedVariable<Object> sharedObject)
        {
            if (variableScope.TryGetSharedVariable(variableName, out SharedVariable sharedVariable))
            {
                sharedObject = sharedVariable as SharedVariable<Object>;
                return sharedObject != null;
            }
            sharedObject = null;
            return false;
        }
        
        public static bool TryGetSharedUObject<T>(this IVariableSource variableScope, string variableName, out SharedVariable<T> sharedTObject) where T : Object
        {
            if (variableScope.TryGetSharedVariable(variableName, out var sharedVariable))
            {
                sharedTObject = sharedVariable as SharedUObject<T>;
                return sharedTObject != null;
            }
            sharedTObject = null;
            return false;
        }
        
        public static bool TryGetSharedObject(this IVariableSource variableScope, string variableName, out SharedVariable<object> sharedObject)
        {
            if (variableScope.TryGetSharedVariable(variableName, out var sharedVariable))
            {
                sharedObject = sharedVariable as SharedObject;
                return sharedObject != null;
            }
            sharedObject = null;
            return false;
        }
        
        /// <summary>
        /// Link variable to global variables
        /// </summary>
        /// <param name="variableSource"></param>
        public static void LinkToGlobal(this IVariableSource variableSource)
        {
            var globalVariables = GlobalVariables.Instance;
            foreach (var variable in variableSource.SharedVariables)
            {
                if (!variable.IsGlobal) continue;
                variable.LinkToSource(globalVariables);
            }
        }
        
        /// <summary>
        /// Link variable to target variable source
        /// </summary>
        /// <param name="variable"></param>
        /// <param name="variableSource"></param>
        public static void LinkToSource(this SharedVariable variable, IVariableSource variableSource)
        {
            if (variable == null) return;
            if (!variable.IsShared && !variable.IsGlobal) return;
            if (!variableSource.TryGetSharedVariable(variable.Name, out var sharedVariable))
            {
                CeresLogger.LogWarning($"Can not map variable {variable.Name} to {variableSource} !");
                return;
            }
            variable.Bind(sharedVariable);
        }
    }
}