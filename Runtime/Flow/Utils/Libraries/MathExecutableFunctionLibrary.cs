using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using UnityEngine.Scripting;
using UnityEngine;
namespace Ceres.Graph.Flow.Utilities
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

        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("-", FontSize = 30)]
        public static float Flow_FloatSubtract(float value1,  float value2)
        {
            return value1 - value2;
        }

        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("*", FontSize = 30)]
        public static float Flow_FloatMultiply(float value1,  float value2)
        {
            return value1 * value2;
        }

        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("/", FontSize = 30)]
        public static float Flow_FloatDivide(float value1,  float value2)
        {
            return value1 / value2;
        }

        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("%", FontSize = 30)]
        public static float Flow_FloatModulo(float value1,  float value2)
        {
            return value1 % value2;
        }

        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("Pow", FontSize = 30)]
        public static float Flow_FloatPow(float value1,  float value2)
        {
            return Mathf.Pow(value1, value2);
        }

        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("Sqrt", FontSize = 30)]
        public static float Flow_FloatSqrt(float floatValue)
        {
            return Mathf.Sqrt(floatValue);
        }

        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("Exp", FontSize = 30)]
        public static float Flow_FloatExp(float floatValue)
        {
            return Mathf.Exp(floatValue);
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
        
        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("-", FontSize = 30)]
        public static int Flow_IntSubtract(int value1,  int value2)
        {
            return value1 - value2;
        }

        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("*", FontSize = 30)]
        public static int Flow_IntMultiply(int value1,  int value2)
        {
            return value1 * value2;
        }

        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("/", FontSize = 30)]
        public static int Flow_IntDivide(int value1,  int value2)
        {
            return value1 / value2;
        }

        [ExecutableFunction(IsScriptMethod = true, DisplayTarget = false), CeresLabel("%", FontSize = 30)]
        public static int Flow_IntModulo(int value1,  int value2)
        {
            return value1 % value2;
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
