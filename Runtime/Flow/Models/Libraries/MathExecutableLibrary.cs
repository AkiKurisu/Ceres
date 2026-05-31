using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using UnityEngine;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Provides scalar, boolean, vector, and quaternion math helpers for Flow graphs.
    /// </summary>
    [CeresGroup("Math")]
    public partial class MathExecutableLibrary : ExecutableFunctionLibrary
    {
        #region Float
        
        /// <summary>
        /// Provides the Float Add operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Float + Operator", SearchAliases = "add, plus, sum"),
         CeresLabel("+", FontSize = 30)]
        public static float Flow_FloatAdd(float value1,  float value2)
        {
            return value1 + value2;
        }

        /// <summary>
        /// Provides the Float Subtract operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Float - Operator", SearchAliases = "subtract, minus"),
         CeresLabel("-", FontSize = 30)]
        public static float Flow_FloatSubtract(float value1,  float value2)
        {
            return value1 - value2;
        }

        /// <summary>
        /// Provides the Float Multiply operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Float * Operator", SearchAliases = "multiply, times"),
         CeresLabel("*", FontSize = 30)]
        public static float Flow_FloatMultiply(float value1,  float value2)
        {
            return value1 * value2;
        }

        /// <summary>
        /// Provides the Float Divide operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Float / Operator", SearchAliases = "divide"),
         CeresLabel("/", FontSize = 30)]
        public static float Flow_FloatDivide(float value1,  float value2)
        {
            return value1 / value2;
        }

        /// <summary>
        /// Provides the Float Modulo operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Float % Operator", SearchAliases = "mod, modulo, remainder"),
         CeresLabel("%", FontSize = 30)]
        public static float Flow_FloatModulo(float value1,  float value2)
        {
            return value1 % value2;
        }

        /// <summary>
        /// Provides the Float Pow operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Float Pow", SearchAliases = "power"),
         CeresLabel("Pow", FontSize = 30)]
        public static float Flow_FloatPow(float value1,  float value2)
        {
            return Mathf.Pow(value1, value2);
        }

        /// <summary>
        /// Provides the Float Sqrt operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Float Sqrt", SearchAliases = "square root"),
         CeresLabel("Sqrt", FontSize = 30)]
        public static float Flow_FloatSqrt(float floatValue)
        {
            return Mathf.Sqrt(floatValue);
        }

        /// <summary>
        /// Provides the Float Exp operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Float Exp", SearchAliases = "exponential"),
         CeresLabel("Exp", FontSize = 30)]
        public static float Flow_FloatExp(float floatValue)
        {
            return Mathf.Exp(floatValue);
        }
        
        /// <summary>
        /// Provides the Float Less Than operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Float < Operator", SearchAliases = "less, less than"),
         CeresLabel("<", FontSize = 30)]
        public static bool Flow_FloatLessThan(float value1,  float value2)
        {
            return value1 < value2;
        }
        
        /// <summary>
        /// Provides the Float Less Than Or Equal To operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Float <= Operator", SearchAliases = "less equal, less than or equal"),
         CeresLabel("<=", FontSize = 30)]
        public static bool Flow_FloatLessThanOrEqualTo(float value1,  float value2)
        {
            return value1 <= value2;
        }
        
        /// <summary>
        /// Provides the Float Greater Than operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Float > Operator", SearchAliases = "greater, greater than"),
         CeresLabel(">", FontSize = 30)]
        public static bool Flow_FloatGreaterThan(float value1,  float value2)
        {
            return value1 > value2;
        }
        
        /// <summary>
        /// Provides the Float Greater Than Or Equal To operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Float >= Operator", SearchAliases = "greater equal, greater than or equal"),
         CeresLabel(">=", FontSize = 30)]
        public static bool Flow_FloatGreaterThanOrEqualTo(float value1,  float value2)
        {
            return value1 >= value2;
        }
        
        /// <summary>
        /// Provides the Float To Int operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false, SearchName = "Float To Int"),
         CeresLabel("To Int")]
        public static int Flow_FloatToInt(float floatValue)
        {
            return (int)floatValue;
        }

        /// <summary>
        /// Provides the Float Abs operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Abs")]
        public static float Flow_FloatAbs(float value)
        {
            return Mathf.Abs(value);
        }

        /// <summary>
        /// Provides the Float Clamp operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Clamp")]
        public static float Flow_FloatClamp(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// Provides the Float Clamp01 operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Clamp01")]
        public static float Flow_FloatClamp01(float value)
        {
            return Mathf.Clamp01(value);
        }

        /// <summary>
        /// Provides the Float Lerp operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Lerp")]
        public static float Flow_FloatLerp(float from, float to, float t)
        {
            return Mathf.Lerp(from, to, t);
        }

        /// <summary>
        /// Provides the Float Inverse Lerp operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Inverse Lerp")]
        public static float Flow_FloatInverseLerp(float from, float to, float value)
        {
            return Mathf.InverseLerp(from, to, value);
        }

        /// <summary>
        /// Provides the Float Min operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Min")]
        public static float Flow_FloatMin(float value1, float value2)
        {
            return Mathf.Min(value1, value2);
        }

        /// <summary>
        /// Provides the Float Max operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Max")]
        public static float Flow_FloatMax(float value1, float value2)
        {
            return Mathf.Max(value1, value2);
        }

        /// <summary>
        /// Provides the Float Floor To Int operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Floor To Int")]
        public static int Flow_FloatFloorToInt(float value)
        {
            return Mathf.FloorToInt(value);
        }

        /// <summary>
        /// Provides the Float Ceil To Int operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Ceil To Int")]
        public static int Flow_FloatCeilToInt(float value)
        {
            return Mathf.CeilToInt(value);
        }

        /// <summary>
        /// Provides the Float Round To Int operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Round To Int")]
        public static int Flow_FloatRoundToInt(float value)
        {
            return Mathf.RoundToInt(value);
        }

        /// <summary>
        /// Provides the Float Sin operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Sin")]
        public static float Flow_FloatSin(float radians)
        {
            return Mathf.Sin(radians);
        }

        /// <summary>
        /// Provides the Float Cos operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Cos")]
        public static float Flow_FloatCos(float radians)
        {
            return Mathf.Cos(radians);
        }

        /// <summary>
        /// Provides the Float Tan operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Tan")]
        public static float Flow_FloatTan(float radians)
        {
            return Mathf.Tan(radians);
        }

        /// <summary>
        /// Provides the Float Atan2 operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Atan2")]
        public static float Flow_FloatAtan2(float y, float x)
        {
            return Mathf.Atan2(y, x);
        }

        /// <summary>
        /// Provides the Float Approximately operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Approximately")]
        public static bool Flow_FloatApproximately(float value1, float value2)
        {
            return Mathf.Approximately(value1, value2);
        }

        /// <summary>
        /// Provides the Float Sign operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Sign")]
        public static float Flow_FloatSign(float value)
        {
            return Mathf.Sign(value);
        }

        /// <summary>
        /// Provides the Float Deg2 Rad operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Deg2Rad")]
        public static float Flow_FloatDeg2Rad(float degrees)
        {
            return degrees * Mathf.Deg2Rad;
        }

        /// <summary>
        /// Provides the Float Rad2 Deg operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Rad2Deg")]
        public static float Flow_FloatRad2Deg(float radians)
        {
            return radians * Mathf.Rad2Deg;
        }
        
        #endregion Float
        
        #region Integer
        
        /// <summary>
        /// Provides the Int Add operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Int + Operator", SearchAliases = "add, plus, sum"),
         CeresLabel("+", FontSize = 30)]
        public static int Flow_IntAdd(int value1,  int value2)
        {
            return value1 + value2;
        }
        
        /// <summary>
        /// Provides the Int Subtract operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Int - Operator", SearchAliases = "subtract, minus"),
         CeresLabel("-", FontSize = 30)]
        public static int Flow_IntSubtract(int value1,  int value2)
        {
            return value1 - value2;
        }

        /// <summary>
        /// Provides the Int Multiply operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Int * Operator", SearchAliases = "multiply, times"),
         CeresLabel("*", FontSize = 30)]
        public static int Flow_IntMultiply(int value1,  int value2)
        {
            return value1 * value2;
        }

        /// <summary>
        /// Provides the Int Divide operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Int / Operator", SearchAliases = "divide"),
         CeresLabel("/", FontSize = 30)]
        public static int Flow_IntDivide(int value1,  int value2)
        {
            return value1 / value2;
        }

        /// <summary>
        /// Provides the Int Modulo operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Int % Operator", SearchAliases = "mod, modulo, remainder"),
         CeresLabel("%", FontSize = 30)]
        public static int Flow_IntModulo(int value1,  int value2)
        {
            return value1 % value2;
        }
        
                
        /// <summary>
        /// Provides the Int Less Than operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Int < Operator", SearchAliases = "less, less than"),
         CeresLabel("<", FontSize = 30)]
        public static bool Flow_IntLessThan(int value1,  int value2)
        {
            return value1 < value2;
        }
        
        /// <summary>
        /// Provides the Int Less Than Or Equal To operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Int <= Operator", SearchAliases = "less equal, less than or equal"),
         CeresLabel("<=", FontSize = 30)]
        public static bool Flow_IntLessThanOrEqualTo(int value1,  int value2)
        {
            return value1 <= value2;
        }
        
        /// <summary>
        /// Provides the Int Greater Than operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Int > Operator", SearchAliases = "greater, greater than"),
         CeresLabel(">", FontSize = 30)]
        public static bool Flow_IntGreaterThan(int value1,  int value2)
        {
            return value1 > value2;
        }
        
        /// <summary>
        /// Provides the Int Greater Than Or Equal To operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Int >= Operator", SearchAliases = "greater equal, greater than or equal"),
         CeresLabel(">=", FontSize = 30)]
        public static bool Flow_IntGreaterThanOrEqualTo(int value1,  int value2)
        {
            return value1 >= value2;
        }

        /// <summary>
        /// Provides the Int To Float operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false, SearchName = "Int To Float"),
         CeresLabel("To Float")]
        public static float Flow_IntToFloat(int intValue)
        {
            return intValue;
        }

        /// <summary>
        /// Provides the Int Abs operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Abs")]
        public static int Flow_IntAbs(int value)
        {
            return Mathf.Abs(value);
        }

        /// <summary>
        /// Provides the Int Clamp operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Clamp")]
        public static int Flow_IntClamp(int value, int min, int max)
        {
            return Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// Provides the Int Min operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Min")]
        public static int Flow_IntMin(int value1, int value2)
        {
            return Mathf.Min(value1, value2);
        }

        /// <summary>
        /// Provides the Int Max operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false), CeresLabel("Max")]
        public static int Flow_IntMax(int value1, int value2)
        {
            return Mathf.Max(value1, value2);
        }
        
        #endregion Integer

        #region Boolean
        
        /// <summary>
        /// Provides the Bool Invert operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Bool ! Operator", SearchAliases = "not, invert"),
         CeresLabel("!", FontSize = 30)]
        public static bool Flow_BoolInvert(bool boolValue)
        {
            return !boolValue;
        }
        
        /// <summary>
        /// Provides the Bool And operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Bool && Operator", SearchAliases = "and"),
         CeresLabel("&&", FontSize = 30)]
        public static bool Flow_BoolAnd(bool value1, bool value2)
        {
            return value1 && value2;
        }
        
        /// <summary>
        /// Provides the Bool Or operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true,  DisplayTarget = false,
            SearchName = "Bool || Operator", SearchAliases = "or"),
         CeresLabel("||", FontSize = 30)]
        public static bool Flow_BoolOr(bool value1, bool value2)
        {
            return value1 || value2;
        }

        /// <summary>
        /// Provides the Bool Xor operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false,
            SearchName = "Bool ^ Operator", SearchAliases = "xor, exclusive or"),
         CeresLabel("^", FontSize = 30)]
        public static bool Flow_BoolXor(bool value1, bool value2)
        {
            return value1 ^ value2;
        }
        
        #endregion Boolean

        #region Vector2

        /// <summary>
        /// Provides the Vector2 operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false, SearchName = "Make Vector2"),
         CeresGroup("Math/Vector"), CeresLabel("Vector2")]
        public static Vector2 Flow_Vector2(float x, float y)
        {
            return new Vector2(x, y);
        }

        #endregion Vector2

        #region Vector3

        /// <summary>
        /// Provides the Vector3 operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false, SearchName = "Make Vector3"),
         CeresGroup("Math/Vector"), CeresLabel("Vector3")]
        public static Vector3 Flow_Vector3(float x, float y, float z)
        {
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Provides the Vector3 Add operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false,
            SearchName = "Vector3 + Operator", SearchAliases = "add, plus, sum"),
         CeresGroup("Math/Vector"), CeresLabel("+", FontSize = 30)]
        public static Vector3 Flow_Vector3Add(Vector3 value1, Vector3 value2)
        {
            return value1 + value2;
        }

        /// <summary>
        /// Provides the Vector3 Subtract operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false,
            SearchName = "Vector3 - Operator", SearchAliases = "subtract, minus"),
         CeresGroup("Math/Vector"), CeresLabel("-", FontSize = 30)]
        public static Vector3 Flow_Vector3Subtract(Vector3 value1, Vector3 value2)
        {
            return value1 - value2;
        }

        /// <summary>
        /// Provides the Vector3 Multiply Float operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false,
            SearchName = "Vector3 * Float Operator", SearchAliases = "multiply, times, scale"),
         CeresGroup("Math/Vector"), CeresLabel("*", FontSize = 30)]
        public static Vector3 Flow_Vector3MultiplyFloat(Vector3 value, float multiplier)
        {
            return value * multiplier;
        }

        /// <summary>
        /// Provides the Vector3 Divide Float operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false,
            SearchName = "Vector3 / Float Operator", SearchAliases = "divide"),
         CeresGroup("Math/Vector"), CeresLabel("/", FontSize = 30)]
        public static Vector3 Flow_Vector3DivideFloat(Vector3 value, float divisor)
        {
            return value / divisor;
        }

        /// <summary>
        /// Provides the Vector3 Dot operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Dot")]
        public static float Flow_Vector3Dot(Vector3 value1, Vector3 value2)
        {
            return Vector3.Dot(value1, value2);
        }

        /// <summary>
        /// Provides the Vector3 Cross operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Cross")]
        public static Vector3 Flow_Vector3Cross(Vector3 value1, Vector3 value2)
        {
            return Vector3.Cross(value1, value2);
        }

        /// <summary>
        /// Provides the Vector3 Magnitude operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Magnitude")]
        public static float Flow_Vector3Magnitude(Vector3 value)
        {
            return value.magnitude;
        }

        /// <summary>
        /// Provides the Vector3 Sqr Magnitude operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Sqr Magnitude")]
        public static float Flow_Vector3SqrMagnitude(Vector3 value)
        {
            return value.sqrMagnitude;
        }

        /// <summary>
        /// Provides the Vector3 Normalize operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Normalize")]
        public static Vector3 Flow_Vector3Normalize(Vector3 value)
        {
            return value.normalized;
        }

        /// <summary>
        /// Provides the Vector3 Distance operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Distance")]
        public static float Flow_Vector3Distance(Vector3 value1, Vector3 value2)
        {
            return Vector3.Distance(value1, value2);
        }

        /// <summary>
        /// Provides the Vector3 Lerp operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Lerp")]
        public static Vector3 Flow_Vector3Lerp(Vector3 from, Vector3 to, float t)
        {
            return Vector3.Lerp(from, to, t);
        }

        /// <summary>
        /// Provides the Vector3 Move Towards operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Move Towards")]
        public static Vector3 Flow_Vector3MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
        {
            return Vector3.MoveTowards(current, target, maxDistanceDelta);
        }

        /// <summary>
        /// Provides the Vector3 Angle operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Angle")]
        public static float Flow_Vector3Angle(Vector3 from, Vector3 to)
        {
            return Vector3.Angle(from, to);
        }

        /// <summary>
        /// Provides the Vector3 Project operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Project")]
        public static Vector3 Flow_Vector3Project(Vector3 vector, Vector3 onNormal)
        {
            return Vector3.Project(vector, onNormal);
        }

        /// <summary>
        /// Provides the Vector3 Reflect operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Reflect")]
        public static Vector3 Flow_Vector3Reflect(Vector3 inDirection, Vector3 inNormal)
        {
            return Vector3.Reflect(inDirection, inNormal);
        }

        /// <summary>
        /// Provides the Vector3 Get X operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Get X")]
        public static float Flow_Vector3GetX(Vector3 value)
        {
            return value.x;
        }

        /// <summary>
        /// Provides the Vector3 Get Y operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Get Y")]
        public static float Flow_Vector3GetY(Vector3 value)
        {
            return value.y;
        }

        /// <summary>
        /// Provides the Vector3 Get Z operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Get Z")]
        public static float Flow_Vector3GetZ(Vector3 value)
        {
            return value.z;
        }

        /// <summary>
        /// Provides the Vector3 With X operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("With X")]
        public static Vector3 Flow_Vector3WithX(Vector3 value, float x)
        {
            value.x = x;
            return value;
        }

        /// <summary>
        /// Provides the Vector3 With Y operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("With Y")]
        public static Vector3 Flow_Vector3WithY(Vector3 value, float y)
        {
            value.y = y;
            return value;
        }

        /// <summary>
        /// Provides the Vector3 With Z operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("With Z")]
        public static Vector3 Flow_Vector3WithZ(Vector3 value, float z)
        {
            value.z = z;
            return value;
        }

        #endregion Vector3

        #region Quaternion

        /// <summary>
        /// Provides the Quaternion Euler operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false, SearchName = "Quaternion Euler"),
         CeresGroup("Math/Vector"), CeresLabel("Euler")]
        public static Quaternion Flow_QuaternionEuler(Vector3 eulerAngles)
        {
            return Quaternion.Euler(eulerAngles);
        }

        /// <summary>
        /// Provides the Quaternion Look Rotation operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Look Rotation")]
        public static Quaternion Flow_QuaternionLookRotation(Vector3 forward, Vector3 upwards)
        {
            return Quaternion.LookRotation(forward, upwards);
        }

        /// <summary>
        /// Provides the Quaternion Slerp operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Slerp")]
        public static Quaternion Flow_QuaternionSlerp(Quaternion from, Quaternion to, float t)
        {
            return Quaternion.Slerp(from, to, t);
        }

        /// <summary>
        /// Provides the Quaternion Inverse operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Inverse")]
        public static Quaternion Flow_QuaternionInverse(Quaternion rotation)
        {
            return Quaternion.Inverse(rotation);
        }

        /// <summary>
        /// Provides the Quaternion Angle operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false),
         CeresGroup("Math/Vector"), CeresLabel("Angle")]
        public static float Flow_QuaternionAngle(Quaternion from, Quaternion to)
        {
            return Quaternion.Angle(from, to);
        }

        /// <summary>
        /// Provides the Quaternion Multiply operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false,
            SearchName = "Quaternion * Quaternion Operator", SearchAliases = "multiply, times"),
         CeresGroup("Math/Vector"), CeresLabel("*", FontSize = 30)]
        public static Quaternion Flow_QuaternionMultiply(Quaternion value1, Quaternion value2)
        {
            return value1 * value2;
        }

        /// <summary>
        /// Provides the Quaternion Multiply Vector operation for Flow graphs.
        /// </summary>
        [ExecutableFunction(IsScriptMethod = true, ExecuteInDependency = true, DisplayTarget = false,
            SearchName = "Quaternion * Vector3 Operator", SearchAliases = "multiply, times, rotate"),
         CeresGroup("Math/Vector"), CeresLabel("*", FontSize = 30)]
        public static Vector3 Flow_QuaternionMultiplyVector(Quaternion rotation, Vector3 point)
        {
            return rotation * point;
        }

        #endregion Quaternion
    }
}
