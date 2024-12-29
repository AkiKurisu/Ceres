using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Ceres.Graph.Flow.Utilities;
using UnityEngine.Scripting;
namespace Ceres
{
    /// <summary>
    /// Executable function library for basic math operations
    /// </summary>
    [Preserve]
    public class MathExecutableFunctionLibrary : ExecutableFunctionLibrary
    {
        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("+", FontSize = 30)]
        public static float Flow_FloatAdd(float value1,  float value2)
        {
            return value1 + value2;
        }
        
        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("To Int")]
        public static int Flow_FloatToInt(float floatValue)
        {
            return (int)floatValue;
        }
        
        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("+", FontSize = 30)]
        public static int Flow_IntAdd(int value1,  int value2)
        {
            return value1 + value2;
        }
        
        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("To Float")]
        public static float Flow_IntToFloat(int intValue)
        {
            return intValue;
        }
        
        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("!", FontSize = 30)]
        public static bool Flow_BoolInvert(bool boolValue)
        {
            return !boolValue;
        }
        
        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("&&", FontSize = 30)]
        public static bool Flow_BoolAnd(bool value1, bool value2)
        {
            return value1 && value2;
        }
        
        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("||", FontSize = 30)]
        public static bool Flow_BoolOr(bool value1, bool value2)
        {
            return value1 || value2;
        }
    }
}
