# Runtime services

Use the smallest service that owns the required lifetime. Confirm current signatures in `Packages/com.kurisu.ceres/Runtime/Core`.

## Choose the subsystem

- **Pool**: use `ObjectPool`, `GameObjectPoolManager`, or pooled task types when allocation reuse is part of the object's contract. Ensure release returns all externally visible state to its initial value.
- **Events**: use `EventBase<T>`, `IEventHandler`, and an event coordinator for routed or scoped events. Preserve propagation and pooled-event disposal; use ordinary C# events when routing is unnecessary.
- **Configs**: derive from `Config<TConfig>` and use the configured location/provider rather than directly reading arbitrary files. Bind editable values through the existing config-variable attributes.
- **Resources**: use `ResourceSystem`, `ResourceCache<TAsset>`, and `SoftAssetReference<T>` for addressable or cached asset ownership. Dispose caches and handles according to the current consumer pattern.
- **Tasks**: use `TaskBase`, `PooledTaskBase<T>`, sequences, and delay tasks for Ceres task composition. Do not confuse them with `UniTask`; choose the model already used by the owning subsystem.
- **Modules**: derive framework startup work from `RuntimeModule` and register it through module configuration. Avoid static initialization that bypasses module ordering.

## Source anchors

- `Runtime/Core/Pool`
- `Runtime/Core/Events`
- `Runtime/Core/Configs`
- `Runtime/Core/Resource`
- `Runtime/Core/Tasks`
- `Runtime/Core/Modules`

Read the matching page under `Documentations/docs/core_*.md` for public usage. Keep implementation-only ordering, cache, and failure details in code or this skill rather than expanding public documentation.
