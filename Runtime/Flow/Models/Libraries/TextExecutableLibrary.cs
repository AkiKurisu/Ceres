using System.Globalization;
using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;
using Ceres.Utilities;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Provides string construction, formatting, replacement, casing, and hashing helpers for Flow graphs.
    /// </summary>
    [CeresGroup("Text")]
    public partial class TextExecutableLibrary : ExecutableFunctionLibrary
    {
        /// <summary>
        /// Concatenates all input strings into a single string.
        /// </summary>
        /// <param name="values">Strings to concatenate.</param>
        /// <returns>The concatenated string.</returns>
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Concat String")]
        public static string Flow_StringConcat(string[] values)
        {
            return string.Concat(values);
        }
        
        /// <summary>
        /// Joins all input strings using the separator.
        /// </summary>
        /// <param name="separator">Separator inserted between each value.</param>
        /// <param name="values">Strings to join.</param>
        /// <returns>The joined string.</returns>
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Join String")]
        public static string Flow_StringJoin(string separator, string[] values)
        {
            return string.Join(separator, values);
        }
        
        /// <summary>
        /// Formats a string with one argument at placeholder {0}.
        /// </summary>
        /// <param name="separator"></param>
        /// <param name="argument"></param>
        /// <returns></returns>
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Format String With One Argument")]
        public static string Flow_StringFormatOneArgument(string separator, object argument)
        {
            return string.Format(separator, argument);
        }
        
        /// <summary>
        /// Formats a string with two arguments at placeholders {0} and {1}.
        /// </summary>
        /// <param name="separator"></param>
        /// <param name="argument0"></param>
        /// <param name="argument1"></param>
        /// <returns></returns>
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Format String With Two Arguments")]
        public static string Flow_StringFormatTwoArguments(string separator, object argument0, object argument1)
        {
            return string.Format(separator, argument0, argument1);
        }
        
        /// <summary>
        /// Formats a string with three arguments at placeholders {0}, {1}, and {2}.
        /// </summary>
        /// <param name="separator"></param>
        /// <param name="argument0"></param>
        /// <param name="argument1"></param>
        /// <param name="argument2"></param>
        /// <returns></returns>
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Format String With Three Arguments")]
        public static string Flow_StringFormatThreeArguments(string separator, object argument0, object argument1, object argument2)
        {
            return string.Format(separator, argument0, argument1, argument2);
        }
        
        /// <summary>
        /// Formats a string with an object array of arguments.
        /// </summary>
        /// <param name="separator">The composite format string.</param>
        /// <param name="arguments">Arguments used by the format string.</param>
        /// <returns>The formatted string.</returns>
        [ExecutableFunction(ExecuteInDependency = true), CeresLabel("Format String")]
        public static string Flow_StringFormat(string separator, object[] arguments)
        {
            return string.Format(separator, arguments);
        }
        
        /// <summary>
        /// Replaces every occurrence of a substring with a new value.
        /// </summary>
        /// <param name="stringValue">The source string.</param>
        /// <param name="oldValue">The substring to replace.</param>
        /// <param name="newValue">The replacement string.</param>
        /// <returns>The updated string.</returns>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true, SearchAliases = "Replace String"),
         CeresLabel("Replace")]
        public static string Flow_StringReplace(string stringValue, string oldValue, string newValue)
        {
            return stringValue.Replace(oldValue, newValue);
        }
        
        /// <summary>
        /// Converts a string to lower case using the current culture.
        /// </summary>
        /// <param name="stringValue">The source string.</param>
        /// <returns>The lower-case string.</returns>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true), CeresLabel("To Lower")]
        public static string Flow_StringToLower(string stringValue)
        {
            return stringValue.ToLower(CultureInfo.CurrentCulture);
        }
        
        /// <summary>
        /// Converts a string to upper case using the current culture.
        /// </summary>
        /// <param name="stringValue">The source string.</param>
        /// <returns>The upper-case string.</returns>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true), CeresLabel("To Upper")]
        public static string Flow_StringToUpper(string stringValue)
        {
            return stringValue.ToUpper(CultureInfo.CurrentCulture);
        }
        
        /// <summary>
        /// Computes a 64-bit hash for the string.
        /// </summary>
        /// <param name="stringValue">The source string.</param>
        /// <returns>The 64-bit hash value.</returns>
        [ExecutableFunction(IsScriptMethod = true, IsSelfTarget = true, ExecuteInDependency = true), CeresLabel("Hash64")]
        public static ulong Flow_StringHash64(string stringValue)
        {
            return stringValue.Hash64();
        }
    }
}
