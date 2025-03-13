using System.Reflection;
using Ceres.Editor.Utilities;
using Ceres.Graph.Flow;

namespace Ceres.Editor.Graph.Flow
{
    public static class ExecutableReflectionEditorUtils
    {
        public static (string filePath, int lineNumber) GetExecutableFunctionFileInfo(MethodInfo methodInfo)
        {
            var function = ExecutableReflection.GetFunction(methodInfo);
            CeresLogger.Assert(function != null, $"Method {methodInfo} is not an executable function");
            if (string.IsNullOrEmpty(function!.FilePath))
            {
                var declareType = methodInfo.DeclaringType;
                (function.FilePath, function.LineNumber) =
                    MonoCecilHelper.TryGetCecilFileOpenInfo(declareType, methodInfo);
            }
            
            return (function.FilePath, function.LineNumber);
        }

        public static string GetExecutableFunctionXmlDocumentation(MethodInfo methodInfo)
        {
            var function = ExecutableReflection.GetFunction(methodInfo);
            CeresLogger.Assert(function != null, $"Method {methodInfo} is not an executable function");
            if (function!.Documentation == null)
            {
                var (filePath, lineNumber) = GetExecutableFunctionFileInfo(methodInfo);
                function.Documentation = new ExecutableFunction.ExecutableDocumentation(filePath, lineNumber);
            }

            return function.Documentation.Summary;
        }
    }
}