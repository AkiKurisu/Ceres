using System.Globalization;
using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Chris;

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
        
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Format String")]
        public static string Flow_StringFormat(string separator, object[] values)
        {
            return string.Format(separator, values);
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
