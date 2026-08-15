# Resource

Resource is an Addressables-based loading and export layer for Ceres projects.
It keeps runtime code address-based, gives editor tooling for lightweight asset
references, and provides a local remote-content export pipeline for packages that
are loaded through an external Addressables catalog.

## Overview

The Resource module has four main parts:

- `ResourceSystem` wraps Addressables loading, instantiation, catalog loading,
  and release calls.
- `ResourceHandle<T>` is a lightweight handle around Addressables
  `AsyncOperationHandle<T>` with UniTask await support.
- `SoftAssetReference<T>` stores an address for runtime loading while keeping
  editor-only GUID data to make inspector workflows fast and stable.
- `ResourceExporter` builds selected Addressable groups into a self-contained
  local remote package that can later be mounted with `ResourceSystem.LoadCatalog`
  or `ResourceSystem.LoadCatalogAsync`.

For projects whose authoritative content source is not persistent Addressable
groups, see [Content Pipeline](./core_content_pipeline.md). It builds a dependency graph
from project-defined scopes, creates transient Addressables settings, supports
validated scoped updates, and can mount the same graph through AssetDatabase in
Editor Play Mode.

## Runtime Loading

Load by Addressables address and release the returned handle when the asset is no
longer needed.

```csharp
using Ceres.Resource;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class IconLoader : MonoBehaviour
{
    private ResourceHandle<Texture2D> _iconHandle;

    public async UniTask LoadIconAsync()
    {
        _iconHandle = ResourceSystem.LoadAssetAsync<Texture2D>("UI/Icons/Main");
        Texture2D icon = await _iconHandle;

        // Use icon here.
    }

    private void OnDestroy()
    {
        ResourceSystem.Release(_iconHandle);
    }
}
```

`ResourceHandle<T>` can be awaited directly, converted to UniTask, or observed
with a callback.

```csharp
ResourceHandle<AudioClip> handle = ResourceSystem.LoadAssetAsync<AudioClip>("Audio/Open");

handle.RegisterCallback(clip =>
{
    Debug.Log($"Loaded clip: {clip.name}");
});

AudioClip result = await handle.ToUniTask();
ResourceSystem.Release(handle);
```

Use `LoadAssetsAsync` when loading all assets under one key or a merged set of
keys. The merge mode follows Addressables semantics.

```csharp
string[] labels = { "Character", "Common" };

ResourceHandle<IList<GameObject>> handle =
    ResourceSystem.LoadAssetsAsync<GameObject>(
        labels,
        ResourceSystem.MergeMode.Union);

IList<GameObject> prefabs = await handle;
ResourceSystem.Release(handle);
```

Use `InstantiateAsync` for prefab instances. Release either through the original
handle while it is still valid or through the created instance.

```csharp
ResourceHandle<GameObject> handle =
    ResourceSystem.InstantiateAsync("Actors/Npc01", transform);

GameObject instance = await handle;

// Later, when the instance is no longer needed.
ResourceSystem.ReleaseInstance(instance);
```

Use `EnsureAssetExists` when you want an explicit failure before starting a load.
It throws `InvalidResourceRequestException` when Addressables cannot resolve the
requested location.

```csharp
try
{
    await ResourceSystem.EnsureAssetExistsAsync<GameObject>("Actors/Npc01");
}
catch (InvalidResourceRequestException exception)
{
    Debug.LogError(exception.Message);
}
```

## Handle Lifetime

`ResourceHandle` and `ResourceHandle<T>` are value types. A default handle or a
released handle is invalid, so calling `ResourceSystem.Release` on an invalid
handle is safe.

```csharp
ResourceHandle<Material> handle = ResourceSystem.LoadAssetAsync<Material>("Materials/Shared");
Material material = await handle;

if (handle.IsValid())
{
    ResourceSystem.Release(handle);
}
```

The handle also implements `IDisposable`, so short-lived loads can use `using`.
Only use this pattern when the loaded asset does not need to stay referenced
after the scope ends.

```csharp
using ResourceHandle<TextAsset> handle =
    ResourceSystem.LoadAssetAsync<TextAsset>("Configs/Startup");

TextAsset text = await handle;
```

## SoftAssetReference

`SoftAssetReference<T>` is a serializable reference that uses only the Addressable
address at runtime. In the Unity Editor it also stores a GUID and lock state to
support fast object-field lookup and validation.

```csharp
using Ceres.Resource;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class CharacterPortrait : MonoBehaviour
{
    [SerializeField]
    private SoftAssetReference<Texture2D> portrait;

    private ResourceHandle<Texture2D> _portraitHandle;

    public async UniTask<Texture2D> LoadPortraitAsync()
    {
        _portraitHandle = portrait.LoadAsync();
        return await _portraitHandle;
    }

    private void OnDestroy()
    {
        ResourceSystem.Release(_portraitHandle);
    }
}
```

`SoftAssetReference.LoadAsync()` caches the first valid handle so repeated calls
do not start duplicate requests. After the returned handle is released, the
cached handle becomes invalid and the next `LoadAsync()` call can load again.

In the inspector, dragging an unmanaged asset into a `SoftAssetReference` field
will add it to Addressables. If no custom address formatter is configured, the
asset path is used as the address.

![SoftAssetReference Inspector](../resources/images/soft_asset_reference.png)

Use `AssetReferenceConstraintAttribute` to restrict the object field, choose a
target Addressables group, customize the generated address, or force an existing
entry into the configured group.

```csharp
using Ceres.Resource;
using UnityEngine;
using UObject = UnityEngine.Object;

public sealed class CharacterDefinition : ScriptableObject
{
    [AssetReferenceConstraint(
        typeof(Texture2D),
        formatter: nameof(FormatPortraitAddress),
        group: "Characters",
        forceGroup: true)]
    [SerializeField]
    private SoftAssetReference<Texture2D> portrait;

    private string FormatPortraitAddress(UObject asset)
    {
        return $"Characters/Portraits/{asset.name}";
    }
}
```

## Dynamic Catalog Loading

`ResourceSystem.LoadCatalog(path)` and `ResourceSystem.LoadCatalogAsync(path)`
load an external Addressables catalog. `path` can point to either the catalog
file or the package directory that contains `catalog` plus the platform catalog
extension returned by `ResourceSystem.GetCatalogExtension()`.

Catalogs produced by Ceres use `{DYNAMIC_LOCAL_PATH}` in bundle locations.
During catalog loading, Resource replaces that placeholder with the directory
that contains the catalog and normalizes path separators.

```csharp
using Ceres.Resource;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class RemoteContentBootstrap : MonoBehaviour
{
    private ResourceHandle<GameObject> _heroHandle;

    public async UniTask MountAsync(string packageDirectory)
    {
        bool loaded = await ResourceSystem.LoadCatalogAsync(packageDirectory);
        if (!loaded)
        {
            Debug.LogError($"Failed to load remote content catalog: {packageDirectory}");
            return;
        }

        _heroHandle = ResourceSystem.LoadAssetAsync<GameObject>("Characters/Hero");

        GameObject prefab = await _heroHandle;

        // Use prefab here.
    }

    private void OnDestroy()
    {
        ResourceSystem.Release(_heroHandle);
    }
}
```

When loading a remote package, the runtime directory must contain the complete
package contents: `catalog.bin` or `catalog.json`, the matching `catalog.hash`,
and every bundle referenced by the catalog. If the editor export created a zip,
extract the zip contents before passing the directory or catalog path to
`LoadCatalogAsync`.

`LoadCatalogAsync` returns `false` when the catalog file cannot be found or when
Addressables fails to load it. Treat that result as a hard stop before resolving
assets from the remote package.

## Editor Export Pipeline

Resource export is editor-only and lives in the `Ceres.Resource.Editor`
namespace. Ceres provides the code-level exporter primitives but does not provide
a profile asset or a built-in editor window. Projects own their build settings
and user-facing orchestration.

Create a `ResourceExportContext` and pass the builders that should participate in
the pipeline. The Addressables builder temporarily includes the selected groups,
writes bundle locations with `{DYNAMIC_LOCAL_PATH}`, restores settings in
cleanup, and postprocesses the catalog so the package can be loaded locally.

```csharp
#if UNITY_EDITOR
using Ceres.Resource.Editor;
using UnityEditor.AddressableAssets.Settings;

public static class CustomResourceExport
{
    public static bool ExportCharacterGroups()
    {
        var context = new ResourceExportContext
        {
            Name = "Characters",
            AssetGroupFilter = IsCharacterGroup
        };

        return ResourceExporter
            .CreateFromContext(context, new IResourceBuilder[]
            {
                new AddressableAssetBuilder(),
                new DefaultBundleNamePatchBuilder()
            })
            .Export();
    }

    private static bool IsCharacterGroup(AddressableAssetGroup group)
    {
        return group && group.Name.StartsWith("Characters");
    }
}
#endif
```

The exporter does not deploy files to any runtime folder automatically. The game
or developer tooling must copy the complete package contents to the directory
that will be passed to `ResourceSystem.LoadCatalogAsync`.
