using Ceres.Annotations;
using Ceres.Graph.Flow.Annotations;

namespace Ceres.Graph.Flow.Utilities
{
    /// <summary>
    /// Executable function library for texts
    /// </summary>
    [CeresGroup("Text")]
    public partial class TextExecutableLibrary : ExecutableFunctionLibrary
    {
        [ExecutableFunction, CeresLabel("String Concat")]
        public static string Flow_StringConcat(string[] values)
        {
            return string.Concat(values);
        }
        
        [ExecutableFunction, CeresLabel("String Join")]
        public static string Flow_StringJoin(string separator, string[] values)
        {
            return string.Join(separator, values);
        }
    }
}
