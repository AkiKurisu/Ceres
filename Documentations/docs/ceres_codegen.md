# Code Generation in Ceres

Ceres uses source generation and IL post-processing to remove repetitive container code and generate typed Flow execution paths.

## Source Generator

Flow uses `IFlowGraphContainer` so Unity objects can host visual scripting without sharing a common component base class.

To reduce duplication of code, `Ceres.Flow.SourceGenerator` analyzes partial classes annotated with `GenerateFlowAttribute`.
And their implementation will be done by generator.

For more details, see [Code Generation in Flow](./flow_codegen.md#source-generator).

## Generated C# Runtime

Generated C# Runtime is a Flow graph runtime mode. It generates graph execution into C# program files, instead of generating partial C# container code.

Use it for hot Flow graphs when you want generated event dispatch, typed data slots, and cached invokers while keeping the normal graph runtime as the disabled or Editor fallback path.

For more details, see [Code Generation in Flow](./flow_codegen.md#generated-c-runtime).

## ILPP

Ceres graph use ILPP to emit IL for initialization logic of `CeresNode` to enhance runtime performance.

In Flow we use ILPP to emit IL for methods annotated with `ImplementableEventAttribute` that let you execute graph event in C#.

For more details, see [Code Generation in Flow](./flow_codegen.md#il-post-process).

## Build the source generators

The analyzer DLLs are built outside Unity with the .NET SDK. Run the following command from `Packages/com.kurisu.ceres/Runtime/SourceGenerators/Source~`:

`dotnet publish -c Release`

The `Ceres.SourceGenerators.sln` solution contains the Core, Graph, and Flow generator projects. Use the `Debug` configuration when attaching a debugger to generator development.

## Related guides

- Learn about [Flow Code Generation](./flow_codegen.md#generated-c-runtime) for Generated C# Runtime details
- Explore [Function Library](./flow_function_library.md) to see source generator in action
- Check [Ceres Concept](./ceres_concept.md) for understanding Ceres core architecture
