using System.Globalization;
using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Ceres.Utilities;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Executable function library for texts
    /// </summary>
    [CeresGroup("Text")]
    public partial class TextExecutableLibrary : ExecutableFunctionLibrary
    {
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Concat String")]
        public static string Flow_StringConcat(string[] values)
        {
            return string.Concat(values);
        }
        
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Join String")]
        public static string Flow_StringJoin(string separator, string[] values)
        {
            return string.Join(separator, values);
        }
        
        /// <summary>
        /// Format string with one argument {0}.
        /// </summary>
        /// <param name="separator"></param>
        /// <param name="argument"></param>
        /// <returns></returns>
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Format String with One Argument")]
        public static string Flow_StringFormatOneArgument(string separator, object argument)
        {
            return string.Format(separator, argument);
        }
        
        /// <summary>
        /// Format string with two arguments {0}, {1}.
        /// </summary>
        /// <param name="separator"></param>
        /// <param name="argument0"></param>
        /// <param name="argument1"></param>
        /// <returns></returns>
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Format String with Two Arguments")]
        public static string Flow_StringFormatTwoArguments(string separator, object argument0, object argument1)
        {
            return string.Format(separator, argument0, argument1);
        }
        
        /// <summary>
        /// Format string with three arguments {0}, {1}, {2}.
        /// </summary>
        /// <param name="separator"></param>
        /// <param name="argument0"></param>
        /// <param name="argument1"></param>
        /// <param name="argument2"></param>
        /// <returns></returns>
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Format String with Three Arguments")]
        public static string Flow_StringFormatThreeArguments(string separator, object argument0, object argument1, object argument2)
        {
            return string.Format(separator, argument0, argument1, argument2);
        }
        
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Format String")]
        public static string Flow_StringFormat(string separator, object[] arguments)
        {
            return string.Format(separator, arguments);
        }
        
        [ExecutableFunction(IsSelfTarget = true, ExecuteInDependency = true), CeresLabel("Replace String")]
        public static string Flow_StringReplace(string stringValue, string oldValue, string newValue)
        {
            return stringValue.Replace(oldValue, newValue);
        }
        
        [ExecutableFunction(IsSelfTarget = true, ExecuteInDependency = true), CeresLabel("ToLower")]
        public static string Flow_StringToLower(string stringValue)
        {
            return stringValue.ToLower(CultureInfo.CurrentCulture);
        }
        
        [ExecutableFunction(IsSelfTarget = true, ExecuteInDependency = true), CeresLabel("ToUpper")]
        public static string Flow_StringToUpper(string stringValue)
        {
            return stringValue.ToUpper(CultureInfo.CurrentCulture);
        }
        
        [ExecutableFunction(IsSelfTarget = true, ExecuteInDependency = true), CeresLabel("Hash64")]
        public static ulong Flow_StringHash64(string stringValue)
        {
            return stringValue.Hash64();
        }
    }
}
