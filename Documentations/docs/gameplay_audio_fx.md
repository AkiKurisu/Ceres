# Audio and Effects

Ceres provides pooled playback for short audio and particle effects. Both systems accept loaded Unity assets or Addressables addresses, and their common operations are exposed to Flow.

## Audio playback

`AudioSystem` creates pooled `AudioSource` objects under the shared game-object pool. Choose an overload according to asset ownership and control requirements.

| Requirement | API |
| --- | --- |
| Loaded one-shot clip | `PlayClipAtPoint(AudioClip, ...)` |
| Addressable one-shot clip | `PlayClipAtPointAsync(string, ...)` |
| Loaded looping clip | `PlayLoopClipAtPoint(AudioClip, ...)` |
| Addressable looping clip | `PlayLoopClipAtPointAsync(string, ...)` |
| Loaded scheduled clip | `ScheduleClipAtPoint(AudioClip, ...)` |
| Addressable scheduled clip | `ScheduleClipAtPointAsync(string, ...)` |

The string `Play*` convenience overloads without an `Async` suffix start their asynchronous load as fire-and-forget operations and do not return a handle.

```csharp
using Ceres.Gameplay.Audios;
using UnityEngine;

AudioSourceHandle ambience = AudioSystem.PlayLoopClipAtPoint(
    ambienceClip,
    listenerPosition,
    volume: 0.7f,
    spatialBlend: 1f,
    minDistance: 8f);

if (ambience.IsPlaying())
{
    ambience.AudioSource.volume = 0.5f;
}

ambience.Stop();
```

For Addressables, await the async overload when the caller needs a handle:

```csharp
using Ceres.Gameplay.Audios;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static async UniTask<AudioSourceHandle> PlayAmbienceAsync(Vector3 position)
{
    return await AudioSystem.PlayLoopClipAtPointAsync(
        "Audio/Ambience/Forest",
        position,
        volume: 0.7f,
        spatialBlend: 1f,
        minDistance: 8f);
}
```

### Handle contract

`AudioSourceHandle` is a versioned sparse-slot handle. It becomes invalid when the pooled source is released, and a stale handle cannot resolve a later source that reused the same slot.

- `Stop` and `Dispose` stop the source and return it to the pool.
- `IsValid` checks both the slot and serial number.
- `AudioSource` and `PooledAudioSource` return `null` after invalidation.
- One-shot playback releases itself after the calculated clip duration.
- Looping sources can also be found or stopped by their clip or address. Prefer the returned handle for scheduled playback.

Only the latest active looping or scheduled source is retained for a given clip or address key. Registering another source for the same key releases the previous one.

## Voice queues

`VoiceProxy` manages a queue of pooled `VoiceCommand` values for one caller-owned `AudioSource`. Commands can hold a loaded clip or a `SoftAssetReference<AudioClip>`, and duplicate command names are suppressed while active.

```csharp
using Ceres.Gameplay.Audios;
using UnityEngine;

public sealed class CharacterVoice : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip greeting;

    private VoiceProxy voice;

    private void Awake()
    {
        voice = new VoiceProxy(source, maxCommandNum: 8);
    }

    public void EnqueueGreeting()
    {
        voice.EnqueueCommand(VoiceCommand.Get(
            "greeting",
            greeting,
            priority: 10,
            volume: 0.8f));
    }

    private void Update()
    {
        voice.Tick();
    }

    private void OnDestroy()
    {
        voice.Dispose();
    }
}
```

The owner must call `Tick`. The queue dequeues lower numeric `Priority` values first; while a command is playing, a queued command with a numerically higher value can interrupt it. `Clear` empties the queue, drops pending and playing command references, resets the proxy status, and releases cached Addressables assets. VoiceProxy does not own or stop the caller's AudioSource.

## Pooled particle effects

`FXSystem.PlayFX` is the fire-and-forget path. It obtains a pooled prefab instance, starts its first child `ParticleSystem`, and returns non-looping effects after their calculated duration.

```csharp
using Ceres.Gameplay.FX;
using UnityEngine;

FXSystem.PlayFX(
    impactPrefab,
    hitPoint,
    Quaternion.LookRotation(hitNormal));
```

Use `Instantiate` or `InstantiateAsync` when the caller needs explicit control:

```csharp
using Ceres.Gameplay.FX;

PooledParticleSystem effect = FXSystem.Instantiate(
    loopingPrefab,
    transform.position,
    transform.rotation,
    transform);

effect.Play(releaseOnEnd: false);

// Later: stop and return the instance to its pool.
effect.Stop(release: true);
```

Addressable instances retain their resource handle with the pooled object. `ReleaseFX(address)` destroys the inactive instances currently stored in that address pool and removes the pool entry; it does not stop a live checked-out instance.

Set `FXSystem.AddressSafeCheck` to `true` to enable preflight location checks in the address-based `PlayFX` overloads and the parent-only `InstantiateAsync` overload. The positional `InstantiateAsync` overload does not perform that optional check. The setting is disabled by default.

Looping particle systems are not released automatically by `Play`. Stop or dispose them explicitly.

## Flow integration

The Gameplay executable library provides Flow nodes for:

- 2D and 3D one-shot audio from a clip or address;
- scheduled 3D audio;
- stopping the active looping or scheduled audio by clip or address;
- playing a particle-system prefab or Addressables prefab.

`PooledParticleSystem.Play`, `Stop`, and `GetDuration` are executable functions for graphs that retain the pooled instance. Handle-based audio control remains a C# API; the convenience Flow nodes are fire-and-forget.
