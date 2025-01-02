# Code Generation in Ceres
Flow use two kinds of code generation to enhance runtime performance and speed up your time.  

## Source Generator

In Unity, most of time we use component to provide modular functions which
 often results in multiple modules being unable to share a parent class.

Therefore, in Unity's GamePlay development, interfaces are more flexible than 
abstract classes.

For this reason, in Flow we use `IFlowGraphContainer` to let Unity Object has visual scripting ability.

To reduce duplication of code, Ceres.SourceGenerator will analyze any partial class inherit `IFlowGraphContainer` and annotated with `GenerateFlowAttribute`. 
And their implementation will be done by generator.

In addition, Ceres.SourceGenerator will add bridge functions to facilitate communication between Graph and C#.

## ILPP

By default, Ceres graph use `System.Reflection` to initialize all ports and shared variables which will cause obvious overhead.

ILPP make those ports and variables to be collected by owner node it self to enhance runtime performance.

## Build Source Generator

> Following document are from com.unity.netcode

Source generator DLLs need to be compiled manually outside of the Unity compilation pipeline using the .NET SDK 6.0 or higher:
https://dotnet.microsoft.com/en-us/download/dotnet/6.0
That can be done with dotnet from within the `Packages\com.unity.netcode\Runtime\SourceGenerators\Source~` directory via command prompt:

`dotnet publish -c Release`

Additionally, they can be built/debugged with the `Ceres.SourceGenerator.sln` solution in the same folder. In order to debug source generators you can replace `Release` with `Debug` when running the publish command.