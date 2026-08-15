# Pool

Ceres Pool reuses GameObjects, component wrappers, and short-lived UniTask collections. It is intended for repeated gameplay objects whose ownership has a clear release point.

## Pool a prefab component

Derive a small wrapper from `PooledComponent<T, TComponent>`, then instantiate it from a prefab. The component is cached with the pooled GameObject and is not searched again on every reuse.

```csharp
using Ceres.Pool;
using UnityEngine;

public sealed class PooledAudioSource
    : PooledComponent<PooledAudioSource, AudioSource>
{
    protected override void OnDispose()
    {
        Component.Stop();
        Component.clip = null;
    }
}

public static class AudioPlayback
{
    public static PooledAudioSource Play(AudioClip clip, GameObject prefab)
    {
        PooledAudioSource instance = PooledAudioSource.Instantiate(prefab);
        instance.Component.clip = clip;
        instance.Component.Play();
        return instance;
    }
}
```

Return the instance when its owner is finished with it.

```csharp
PooledAudioSource playback = AudioPlayback.Play(clip, audioPrefab);

// Later, after playback or cancellation.
playback.Dispose();
```

Use the position and rotation overload of `Instantiate` for world objects. Use `PooledComponent<T, TComponent>.Get()` when an empty GameObject with the component is sufficient.

## Lifetime and ownership

`PooledGameObject` implements both `IDisposable` and the Ceres R3 lifetime scope. Disposing it performs the following work:

- disposes subscriptions and other disposables registered with `AddTo(pooledObject)`;
- cancels scheduler handles tracked by a derived wrapper;
- deactivates and returns the GameObject to `GameObjectPoolManager`;
- returns the managed wrapper to its object pool.

`Dispose()` is idempotent for an active wrapper. Do not keep `GameObject`, `Transform`, or `Component` references after disposal because the same instance can immediately be reused by another caller.

Call `GameObjectPoolManager.ReleasePool(key)` to destroy inactive objects for one `PoolKey`, or `ReleaseAll()` to clear every inactive GameObject pool. These operations are cleanup boundaries, not substitutes for disposing active wrappers.

## Pooled UniTask collections

`UniParallel`, `UniParallel<T>`, `UniSequence`, and `SequenceTask<T>` reuse their backing lists. Dispose the collection after awaiting it.

```csharp
using Ceres.Pool;
using Cysharp.Threading.Tasks;

public static async UniTask LoadBoth(UniTask loadPlayer, UniTask loadWorld)
{
    using var operations = UniParallel.Create(loadPlayer, loadWorld);
    await operations;
}
```

`UniParallel` awaits all operations together. `UniSequence` awaits them in list order. `SequenceTask<T>.GetNonAlloc(results)` lets the caller supply the result array for a typed sequence.

## Constraints

- A prefab pool is keyed by the prefab instance ID. Release it with `PooledComponent<T, TComponent>.GetPooledKey(prefab)` when explicit cleanup is required.
- `PooledGameObject.Get(string)` uses the string as a pool key and creates an empty GameObject; it does not load an Addressable asset.
- Reset component state in `OnDispose()` so the next caller does not observe stale data.
- Pooling avoids repeated creation only after an object has first been returned to the pool.

## Related documentation

- [Schedulers](./core_schedulers.md) for cancelable delayed release
- [Reactive Integration](./core_reactive.md) for binding subscriptions to a pooled lifetime
- [Resource](./core_resource.md) for Addressables loading and handle ownership
