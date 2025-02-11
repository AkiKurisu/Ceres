using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
namespace Ceres.SourceGenerator;

/// <summary>
/// Some helpers for debugging purpose. Notable entries:
/// - Logging and reporting (in file and compiler diagnostics)
/// </summary>
internal static class Helpers
{
    private static ThreadLocal<string> s_OutputFolder;

    private static ThreadLocal<string> s_ProjectPath;

    private static ThreadLocal<bool> s_IsUnity2021_OrNewer;

    private static ThreadLocal<bool> s_SupportTemplatesFromAdditionalFiles;

    private static ThreadLocal<bool> s_WriteLogToDisk;

    private static ThreadLocal<bool> s_CanWriteFiles;

    private static ThreadLocal<LoggingLevel> s_LogLevel;

    public enum LoggingLevel : int
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    public static string ProjectPath
    {
        get => s_ProjectPath.Value;
        private set => s_ProjectPath.Value = value;
    }
    public static string OutputFolder
    {
        get => s_OutputFolder.Value;
        private set => s_OutputFolder.Value = value;
    }

    public static bool IsUnity2021_OrNewer
    {
        get => s_IsUnity2021_OrNewer.Value;
        private set => s_IsUnity2021_OrNewer.Value = value;
    }

    public static bool SupportTemplateFromAdditionalFiles
    {
        get => s_SupportTemplatesFromAdditionalFiles.Value;
        private set => s_SupportTemplatesFromAdditionalFiles.Value = value;
    }

    public static bool WriteLogToDisk
    {
        get => s_WriteLogToDisk.Value;
        private set => s_WriteLogToDisk.Value = value;
    }

    public static bool CanWriteFiles
    {
        get => s_CanWriteFiles.Value;
        private set => s_CanWriteFiles.Value = value;
    }

    static public LoggingLevel CurrentLogLevel => s_LogLevel.Value;

    static Helpers()
    {
        s_OutputFolder = new ThreadLocal<string>(()=> Path.Combine("Temp", "CeresGenerated"));
        s_ProjectPath = new ThreadLocal<string>();
        s_IsUnity2021_OrNewer = new ThreadLocal<bool>();
        s_SupportTemplatesFromAdditionalFiles = new ThreadLocal<bool>();
        s_WriteLogToDisk = new ThreadLocal<bool>();
        s_CanWriteFiles = new ThreadLocal<bool>();
        s_LogLevel = new ThreadLocal<LoggingLevel>();
    }

    public static void SetupContext(GeneratorExecutionContext executionContext)
    {
        ProjectPath = null;
        //by default we allow both writing files and logs to disk. It is possible to change the behavior via
        //globalconfig
        CanWriteFiles = true;
        WriteLogToDisk = true;
        IsUnity2021_OrNewer = executionContext.ParseOptions.PreprocessorSymbolNames.Any(d => d == "UNITY_2022_1_OR_NEWER" || d == "UNITY_2021_3_OR_NEWER");
        //Setup the current project folder directory by inspecting the context for global options or additional files, depending on the current Unity version
        if (!IsUnity2021_OrNewer)
        {
            SupportTemplateFromAdditionalFiles = false;
            if (executionContext.AdditionalFiles.Any() && !string.IsNullOrEmpty(executionContext.AdditionalFiles[0].Path))
                ProjectPath = Helpers.FindProjectFolderFromAdditionalFile(executionContext.AdditionalFiles[0].Path);
        }
        else
        {
            SupportTemplateFromAdditionalFiles = true;
            if (executionContext.AdditionalFiles.Any() && !string.IsNullOrEmpty(executionContext.AdditionalFiles[0].Path))
                ProjectPath = executionContext.AdditionalFiles[0].GetText()?.ToString();
        }
        //Parse global options and overrides default behaviour. They are used by both tests, and Editor (2021_OR_NEWER)
        ProjectPath = executionContext.GetOptionsString(GlobalOptions.ProjectPath, ProjectPath);
        OutputFolder = executionContext.GetOptionsString(GlobalOptions.OutputPath, OutputFolder);
        SupportTemplateFromAdditionalFiles = GlobalOptions.GetOptionsFlag(executionContext, GlobalOptions.TemplateFromAdditionalFiles, SupportTemplateFromAdditionalFiles);

        //If the project path is not valid, for any reason, we can't write files and/or log to disk
        if (string.IsNullOrEmpty(ProjectPath))
        {
            WriteLogToDisk = false;
            CanWriteFiles = false;
            Debug.LogWarning("Unable to setup/find the project path. Forcibly disable writing logs and files to disk");
        }
        else
        {
            Directory.CreateDirectory(GetOutputPath());
            CanWriteFiles = executionContext.GetOptionsFlag(GlobalOptions.WriteFilesToDisk, CanWriteFiles);
            WriteLogToDisk = executionContext.GetOptionsFlag(GlobalOptions.WriteLogsToDisk, WriteLogToDisk);
        }

        //The default log level is info. User can customise that via debug config. Info level is very light right now.
        s_LogLevel.Value = LoggingLevel.Info;
        var loggingLevel = executionContext.GetOptionsString(GlobalOptions.LoggingLevel);
        if (!string.IsNullOrEmpty(loggingLevel) && Enum.TryParse<LoggingLevel>(loggingLevel.ToLower(), out var logLevel))
            s_LogLevel.Value = logLevel;
    }

    public static string GetOutputPath()
    {
        return Path.Combine(ProjectPath, OutputFolder);
    }

    // This path resolution is necessary for 2020.x where we need to resolve templates from packages
    // and other folders.
    private static string FindProjectFolderFromAdditionalFile(string folder)
    {
        var index = folder.LastIndexOf("/Library/", StringComparison.Ordinal);
        if(index < 0)
            index = folder.LastIndexOf("\\Library\\", StringComparison.Ordinal);
        return index > 0 ? folder.Substring(0, index) : null;
    }

    public static SourceText WithInitialLineDirective(this SourceText sourceText, string generatedSourceFilePath)
    {
        var firstLine = sourceText.Lines.FirstOrDefault();
        return sourceText.WithChanges(new TextChange(firstLine.Span, $"#line 1 \"{generatedSourceFilePath}\"" + Environment.NewLine + firstLine));
    }
}

internal static class Debug
{
    public static string LastErrorLog { get; set; } // used for tests. TODO have something fancier that tracks all logs

    public static void LaunchDebugger()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Debugger.Launch();
        }
        else
        {
            string text = $"Attach to {Process.GetCurrentProcess().Id} ceres generator";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                StarProcess("/usr/bin/osascript", $"-e \"display dialog \\\"{text}\\\" with icon note buttons {{\\\"OK\\\"}}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                StarProcess("/usr/bin/zenity", $@"--info --title=""Attach Debugger"" --text=""{text}"" --no-wrap");
            }
        }
    }

    public static void LaunchDebugger(GeneratorExecutionContext context, string assembly)
    {
        if(string.IsNullOrEmpty(assembly)
           || string.IsNullOrEmpty(context.Compilation.AssemblyName)
           || context.Compilation.AssemblyName.Equals(assembly, StringComparison.InvariantCultureIgnoreCase))
        {
            LaunchDebugger();
        }
    }

    public static void LaunchDebugger(Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax node, string[] names)
    {
        if(names.Contains(node.Identifier.ValueText))
        {
            LaunchDebugger();
        }
    }

    private static void StarProcess(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = true,
            CreateNoWindow = false
        };

        var processTemp = new Process {StartInfo = startInfo, EnableRaisingEvents = true};
        processTemp.Start();
        processTemp.WaitForExit();
    }

    private const string LogFile = "SourceGenerator.log";
        
    private static string GetLogFilePath()
    {
        return Path.Combine(Helpers.GetOutputPath(), LogFile);
    }
        
    private static TextWriter GetOutputStream()
    {
        return Helpers.WriteLogToDisk ? File.AppendText(GetLogFilePath()) : Console.Out;
    }
        
    static void LogToDebugStream(string level, string message)
    {
        try
        {
            using var writer = GetOutputStream();
            writer.WriteLine($"[{level}]{message}");
        }
        catch (Exception flushEx)
        {
            Console.WriteLine($"Exception while writing to log: {flushEx.Message}");
        }
    }
    public static void LogException(Exception exception)
    {
        LastErrorLog = exception.ToString();
        try
        {
            using var writer = GetOutputStream();
            writer.Write("[Exception]");
            writer.WriteLine(exception.ToString());
            writer.WriteLine("Callstack:");
            writer.Write(exception.StackTrace);
            writer.Write('\n');
        }
        catch (Exception flushEx)
        {
            Console.WriteLine($"Exception while writing to log: {flushEx.Message}");
        }
    }
        
    public static void LogDebug(string message)
    {
        if(Helpers.CurrentLogLevel > Helpers.LoggingLevel.Debug)
            return;
        LogToDebugStream("Debug", message);
    }
        
    public static void LogInfo(string message)
    {
        if(Helpers.CurrentLogLevel > Helpers.LoggingLevel.Info)
            return;
        LogToDebugStream("Info", message);
    }
        
    public static void LogWarning(string message)
    {
        if(Helpers.CurrentLogLevel > Helpers.LoggingLevel.Warning)
            return;
        LogToDebugStream("Warning", message);
    }
        
    public static void LogError(string message, string additionalInfo)
    {
        message += $"\n\nAdditional info:\n{additionalInfo}\n\nStacktrace:\n";
        message += Environment.StackTrace;
        LastErrorLog = message;
        LogToDebugStream("Error", message);
    }
}

public static class GlobalOptions
{
    /// <summary>
    /// Override the current project path. Used by the generator to flush logs or lookup files.
    /// </summary>
    public const string ProjectPath = "ceres.sourcegenerator.projectpath";
        
    /// <summary>
    /// Override the output folder where the generator flush logs and generated files.
    /// </summary>
    public const string OutputPath = "ceres.sourcegenerator.outputfolder";
        
    /// <summary>
    /// Skip validation of missing assmebly references. Mostly used for testing.
    /// </summary>
    public const string DisableRerencesChecks = "ceres.sourcegenerator.disable_references_checks";
        
    /// <summary>
    /// Enable/Disable support for passing custom templates using additional files. Mostly for testing
    /// </summary>
    public const string TemplateFromAdditionalFiles = "ceres.sourcegenerator.templates_from_additional_files";
        
    /// <summary>
    /// Enable/Disable writing generated code to output folder
    /// </summary>
    public const string WriteFilesToDisk = "ceres.sourcegenerator.write_files_to_disk";
        
    /// <summary>
    /// Enable/Disable writing logs to the file (default is Temp/CeresGenerated/sourcegenerator.log)
    /// </summary>
    public const string WriteLogsToDisk = "ceres.sourcegenerator.write_logs_to_disk";
        
    /// <summary>
    /// The minimal log level. Available: Debug, Warning, Error. Default is error. (NOT SUPPORTED YET)
    /// </summary>
    public const string LoggingLevel = "ceres.sourcegenerator.logging_level";
        
    /// <summary>
    /// Enable/Disable writing logs to the file (default is Temp/CeresGenerated/sourcegenerator.log)
    /// </summary>
    public const string EmitTimings = "ceres.sourcegenerator.emit_timing";
        
    /// <summary>
    /// Enable/Disable writing logs to the file (default is Temp/CeresGenerated/sourcegenerator.log)
    /// </summary>
    public const string AttachDebugger = "ceres.sourcegenerator.attach_debugger";

    ///<summary>
    /// return if a flag is set in the GlobalOption dictionary.
    /// A flag is consider set if the key is in the GlobalOptions and its string value is either empty or "1"
    /// Otherwise the flag is considered as not set.
    ///</summary>
    public static bool GetOptionsFlag(this GeneratorExecutionContext context, string key, bool defaultValue = false)
    {
        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(key, out var stringValue))
            return string.IsNullOrEmpty(stringValue) || (stringValue is "1" or "true");
        return defaultValue;
    }

    /// <summary>
    /// Return the string value associated with the key in the GlobalOptions if the key is present
    /// </summary>
    /// <param name="context"></param>
    /// <param name="key"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static string GetOptionsString(this GeneratorExecutionContext context, string key, string defaultValue = null)
    {
        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(key, out var stringValue))
            return stringValue;
        return defaultValue;
    }
}