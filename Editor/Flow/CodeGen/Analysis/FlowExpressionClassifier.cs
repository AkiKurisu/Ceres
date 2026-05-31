using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Reflection;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Utilities;
using UnityEngine;

namespace Ceres.Editor.Graph.Flow.CodeGen
{
    internal static class FlowExpressionClassifier
    {
        public static CsExpressionPurity ClassifyFunction(FlowCSharpRuntimeGenerator.DirectCallInfo directCall)
        {
            if (directCall.DeclaringType == typeof(UnityExecutableLibrary))
            {
                if (TryBuildUnityTimeExpression(directCall, out _))
                {
                    return CsExpressionPurity.PerEventStable;
                }

                if (directCall.MethodName is "Flow_RandomRange" or "Flow_RandomRangeInt")
                {
                    return CsExpressionPurity.Impure;
                }

                if (directCall.MethodName.Contains("GetComponent", StringComparison.Ordinal) ||
                    directCall.MethodName == "Flow_FindObjectOfType")
                {
                    return CsExpressionPurity.CachePerEvent;
                }
            }

            if (directCall.DeclaringType == typeof(MathExecutableLibrary))
            {
                return CsExpressionPurity.Pure;
            }

            return directCall.MethodInfo.GetCustomAttribute<PureAttribute>() != null
                ? CsExpressionPurity.Pure
                : CsExpressionPurity.Impure;
        }

        public static bool CanInlineFunction(FlowCSharpRuntimeGenerator.DirectCallInfo directCall)
        {
            return ClassifyFunction(directCall) is CsExpressionPurity.Pure or CsExpressionPurity.PerEventStable;
        }

        public static bool TryBuildIntrinsicFunctionExpression(
            FlowCSharpRuntimeGenerator.DirectCallInfo directCall,
            IReadOnlyList<string> arguments, out string expression)
        {
            expression = null;
            if (directCall.DeclaringType == typeof(UnityExecutableLibrary) &&
                TryBuildUnityTimeExpression(directCall, out expression))
            {
                return true;
            }

            if (directCall.DeclaringType != typeof(MathExecutableLibrary))
            {
                return false;
            }

            string Unary(string op) => $"({op}({arguments[0]}))";
            string Binary(string op) => $"(({arguments[0]}) {op} ({arguments[1]}))";
            string Call(string owner, string method) => $"{owner}.{method}({string.Join(", ", arguments)})";

            expression = directCall.MethodName switch
            {
                "Flow_FloatAdd" => Binary("+"),
                "Flow_FloatSubtract" => Binary("-"),
                "Flow_FloatMultiply" => Binary("*"),
                "Flow_FloatDivide" => Binary("/"),
                "Flow_FloatModulo" => Binary("%"),
                "Flow_FloatLessThan" => Binary("<"),
                "Flow_FloatLessThanOrEqualTo" => Binary("<="),
                "Flow_FloatGreaterThan" => Binary(">"),
                "Flow_FloatGreaterThanOrEqualTo" => Binary(">="),
                "Flow_FloatToInt" => $"((int)({arguments[0]}))",
                "Flow_FloatPow" => Call("Mathf", "Pow"),
                "Flow_FloatSqrt" => Call("Mathf", "Sqrt"),
                "Flow_FloatExp" => Call("Mathf", "Exp"),
                "Flow_FloatAbs" => Call("Mathf", "Abs"),
                "Flow_FloatClamp" => Call("Mathf", "Clamp"),
                "Flow_FloatClamp01" => Call("Mathf", "Clamp01"),
                "Flow_FloatLerp" => Call("Mathf", "Lerp"),
                "Flow_FloatInverseLerp" => Call("Mathf", "InverseLerp"),
                "Flow_FloatMin" => Call("Mathf", "Min"),
                "Flow_FloatMax" => Call("Mathf", "Max"),
                "Flow_FloatFloorToInt" => Call("Mathf", "FloorToInt"),
                "Flow_FloatCeilToInt" => Call("Mathf", "CeilToInt"),
                "Flow_FloatRoundToInt" => Call("Mathf", "RoundToInt"),
                "Flow_FloatSin" => Call("Mathf", "Sin"),
                "Flow_FloatCos" => Call("Mathf", "Cos"),
                "Flow_FloatTan" => Call("Mathf", "Tan"),
                "Flow_FloatAtan2" => Call("Mathf", "Atan2"),
                "Flow_FloatApproximately" => Call("Mathf", "Approximately"),
                "Flow_FloatSign" => Call("Mathf", "Sign"),
                "Flow_FloatDeg2Rad" => $"(({arguments[0]}) * Mathf.Deg2Rad)",
                "Flow_FloatRad2Deg" => $"(({arguments[0]}) * Mathf.Rad2Deg)",
                "Flow_IntAdd" => Binary("+"),
                "Flow_IntSubtract" => Binary("-"),
                "Flow_IntMultiply" => Binary("*"),
                "Flow_IntDivide" => Binary("/"),
                "Flow_IntModulo" => Binary("%"),
                "Flow_IntLessThan" => Binary("<"),
                "Flow_IntLessThanOrEqualTo" => Binary("<="),
                "Flow_IntGreaterThan" => Binary(">"),
                "Flow_IntGreaterThanOrEqualTo" => Binary(">="),
                "Flow_IntToFloat" => $"((float)({arguments[0]}))",
                "Flow_IntAbs" => Call("Mathf", "Abs"),
                "Flow_IntClamp" => Call("Mathf", "Clamp"),
                "Flow_IntMin" => Call("Mathf", "Min"),
                "Flow_IntMax" => Call("Mathf", "Max"),
                "Flow_BoolInvert" => Unary("!"),
                "Flow_BoolAnd" => Binary("&"),
                "Flow_BoolOr" => Binary("|"),
                "Flow_BoolXor" => Binary("^"),
                "Flow_Vector2" => $"new Vector2({arguments[0]}, {arguments[1]})",
                "Flow_Vector3" => $"new Vector3({arguments[0]}, {arguments[1]}, {arguments[2]})",
                "Flow_Vector3Add" => Binary("+"),
                "Flow_Vector3Subtract" => Binary("-"),
                "Flow_Vector3MultiplyFloat" => Binary("*"),
                "Flow_Vector3DivideFloat" => Binary("/"),
                _ => null
            };

            return expression != null;
        }

        public static bool TryBuildUnityTimeExpression(FlowCSharpRuntimeGenerator.DirectCallInfo directCall,
            out string expression)
        {
            expression = null;
            if (directCall.DeclaringType != typeof(UnityExecutableLibrary) ||
                directCall.ParameterTypes.Length != 0)
            {
                return false;
            }

            expression = directCall.MethodName switch
            {
                "Flow_TimeGetTime" => "Time.time",
                "Flow_TimeGetUnscaledTime" => "Time.unscaledTime",
                "Flow_TimeGetDeltaTime" => "Time.deltaTime",
                "Flow_TimeGetFixedDeltaTime" => "Time.fixedDeltaTime",
                "Flow_TimeGetFrameCount" => "Time.frameCount",
                "Flow_TimeGetRealtimeSinceStartup" => "Time.realtimeSinceStartup",
                "Flow_TimeGetTimeScale" => "Time.timeScale",
                _ => null
            };

            return expression != null;
        }
    }
}
