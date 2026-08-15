# Schedulers

Ceres Schedulers runs time- or frame-based callbacks without creating a coroutine per operation. Each registration returns a value-type `SchedulerHandle` that controls the scheduled work.

## Schedule work

Use `Delay` for elapsed time and `WaitFrame` for frame counts. Both support `Update`, `FixedUpdate`, and `LateUpdate` through `TickFrame`.

```csharp
using Ceres.Schedulers;
using UnityEngine;

public sealed class DelayedAction : MonoBehaviour
{
    private SchedulerHandle _handle;

    private void OnEnable()
    {
        _handle = Scheduler.Delay(
            1.5f,
            OnElapsed,
            TickFrame.Update,
            isLooped: false,
            ignoreTimeScale: true);
    }

    private void OnDisable()
    {
        _handle.Dispose();
        _handle = default;
    }

    private static void OnElapsed()
    {
        Debug.Log("Elapsed");
    }
}
```

Use the `ref SchedulerHandle` overload when replacing an existing schedule. It cancels the previous handle before assigning the new one.

```csharp
Scheduler.Delay(ref _handle, 0.25f, Refresh);
```

## Handle lifecycle

`SchedulerHandle` exposes `IsValid`, `IsDone`, `Pause`, `Resume`, `Cancel`, and `Dispose`. `Dispose` cancels unfinished work and is the normal lifetime boundary for a component-owned schedule.

Await an existing schedule when sequential async code is clearer than a completion callback.

```csharp
using Ceres.Schedulers;
using Cysharp.Threading.Tasks;
using System.Threading;

public static async UniTask WaitBeforeContinue(CancellationToken cancellationToken)
{
    SchedulerHandle handle = Scheduler.Delay(0.5f, static () => { });
    await handle.WaitAsync(cancellationToken);
}
```

`WaitAsync` cancels the schedule when its cancellation token is canceled by default. A completed handle no longer represents live work.

## Low-overhead callbacks

The `DelayUnsafe` and `WaitFrameUnsafe` overloads accept `SchedulerUnsafeBinding` values, including managed function pointers and instance-bound function pointers. Use them only in performance-sensitive code that can compile with unsafe code enabled. The delegate overloads are the default API for ordinary gameplay code.

## Scheduler Debugger

Open **Tools > Ceres > Debug > Scheduler Debugger** in Play Mode to inspect active schedules and pause, resume, cancel, or clear them.

Enable **Stack Trace** under **Project Settings > Ceres** to attach call-site information. Stack trace capture allocates in the Editor, so disable it while profiling allocations.

## Constraints

- A handle is a value type; keep the returned value if the operation may need cancellation.
- Looped schedules continue until canceled or disposed.
- `ignoreTimeScale` applies to time delays. Frame counters always advance on their selected Unity update phase.
- Capturing lambdas can allocate even though the scheduler storage itself is pooled.

## Related documentation

- [Events](./core_events.md) for routed notifications and frame-bound event dispatch
- [Tasks](./core_tasks.md) for prerequisite and sequence orchestration
