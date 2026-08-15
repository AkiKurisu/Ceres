# Modules

The Modules system runs small initialization units before the first scene
loads. It provides ordering and discovery only; ownership, shutdown, and
per-frame behavior remain the responsibility of the initialized service.

Use a `RuntimeModule` when a subsystem must register global services or warm
process-wide state before scene content starts. Do not use it as a replacement
for scene lifecycle components.

## Define a Runtime Module

Derive from `RuntimeModule`, implement `Initialize`, and override `Order` when
the module depends on another startup step:

```csharp
using Ceres.Modules;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class TelemetryModule : RuntimeModule
{
    public override int Order => 200;

    public override void Initialize()
    {
        Debug.Log("Telemetry services initialized.");
    }
}
```

Modules are instantiated with `Activator.CreateInstance`, so each concrete type
must have an accessible parameterless constructor. Initialization runs in
ascending `Order`; the default order is `100`.

`Initialize` is synchronous. Start asynchronous work explicitly from the module
or delegate it to a project-owned startup coordinator when later systems must
await completion.

## Startup Contract

An internal loader marked with
`RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` performs startup in this order:

1. Prepare packaged config files through the Configs module.
2. Load `ModuleConfig`.
3. Resolve module types from the explicit list or by assembly scanning.
4. Instantiate the modules, sort them by `Order`, and call `Initialize()` once.

`ModuleConfig.Modules` is authoritative when it contains at least one entry. An
empty list falls back to scanning loaded non-Editor assemblies for every
non-abstract `RuntimeModule` subtype.

Configure the explicit list in **Project Settings > Ceres > Module Settings**.
**Register All** scans the current project and writes the discovered types to
the project config.

Use the explicit list for IL2CPP builds. It avoids depending on runtime assembly
scanning and gives the build pipeline concrete serialized type references.
Apply Unity's `PreserveAttribute` or an equivalent linker rule to module types
whose only reachability is reflective construction.

## Ordering and Dependencies

`Order` is the only dependency mechanism supplied by the module loader. Keep
the values coarse enough to insert another module later:

```csharp
public sealed class NetworkModule : RuntimeModule
{
    public override int Order => 100;
    public override void Initialize() { }
}

public sealed class MatchmakingModule : RuntimeModule
{
    public override int Order => 200;
    public override void Initialize() { }
}
```

Modules with the same order have no documented relative order. Assign distinct
orders when one module requires another to be initialized first.

The loader does not catch exceptions from constructors or `Initialize`. A
failure stops the remaining startup sequence, so surface configuration errors
with enough context to diagnose them.

## Module Configuration

`ModuleConfig` is a normal Ceres config stored at `Ceres.Modules`. It contains:

| Member | Purpose |
| --- | --- |
| `Modules` | Explicit list of serialized `RuntimeModule` types. |
| `MetaData` | Project-defined string key/value data available during startup. |

The module array does not determine execution order; each constructed module's
`Order` property does. Invalid serialized types are skipped while resolving the
explicit list.

Project code may read or update metadata through the standard Configs API:

```csharp
using Ceres.Modules;

var config = ModuleConfig.Get();
config.MetaData["environment"] = "staging";
config.Save();
```

Use metadata only for small startup values. Structured subsystem settings
belong in their own `Config<TConfig>` type.

Related API: <xref:Ceres.Modules.RuntimeModule> and
<xref:Ceres.Modules.ModuleConfig>.
