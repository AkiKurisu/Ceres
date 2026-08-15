# Tasks

Ceres Tasks is a lightweight, frame-driven task state machine. It is designed for gameplay work that needs prerequisites, ordered composition, and event-based completion. It is separate from `UniTask`: a `TaskBase` advances through `Tick()` while the internal runner owns its active lifetime.

## Define and run a task

Frequently created tasks should derive from `PooledTaskBase<T>`. Initialize their state after `GetPooled()`, complete them through `CompleteTask()`, and clear custom state in `Reset()`.

```csharp
using Ceres.Tasks;

public sealed class CountFramesTask : PooledTaskBase<CountFramesTask>
{
    private int _remaining;

    public static CountFramesTask Create(int frameCount)
    {
        CountFramesTask task = GetPooled();
        task._remaining = frameCount;
        return task;
    }

    public override void Tick()
    {
        if (--_remaining <= 0)
        {
            CompleteTask();
        }
    }

    protected override void Reset()
    {
        base.Reset();
        _remaining = 0;
    }
}
```

Call `Run()` to start the task and register it with the internal runner. Observe completion through `TaskCompleteEvent` when an external owner needs a notification.

```csharp
using Ceres.Events;
using Ceres.Tasks;
using UnityEngine;

public sealed class TaskExample : MonoBehaviour
{
    private void OnEnable()
    {
        EventSystem.RegisterCallback<TaskCompleteEvent>(OnTaskComplete);
    }

    private void Start()
    {
        CountFramesTask.Create(3).Run();
    }

    private void OnDisable()
    {
        EventSystem.UnregisterCallback<TaskCompleteEvent>(OnTaskComplete);
    }

    private static void OnTaskComplete(TaskCompleteEvent evt)
    {
        Debug.Log($"Completed: {evt.Task.GetTaskID()}");
    }
}
```

## Prerequisites

Register prerequisites before running the dependent task. Calling `Run()` while prerequisites remain does not register the dependent task; completion of the final prerequisite starts it automatically.

```csharp
DelayTask warmup = DelayTask.GetPooled(0.5f);
CountFramesTask gameplay = CountFramesTask.Create(3);

gameplay.RegisterPrerequisite(warmup);
gameplay.Run();
warmup.Run();
```

Every prerequisite must itself run and complete. A stopped prerequisite does not emit `TaskCompleteEvent` and therefore does not release its dependents.

## Sequences

`SequenceTask` owns its appended tasks and advances them in order. Each child is disposed after it completes or stops.

```csharp
SequenceTask sequence = SequenceTask
    .GetPooled(DelayTask.GetPooled(0.25f))
    .Append(CountFramesTask.Create(2))
    .Append(DelayTask.GetPooled(0.25f));

sequence.Run();
```

## Lifecycle and ownership

- `Run()` starts a task only when it has no unresolved prerequisites. The internal runner acquires active pooled tasks and disposes them after completion or stop.
- `Running` tasks receive `Tick()`. `Paused` tasks remain registered but do not tick. `Stopped` tasks are removed without a completion event.
- `CompleteTask()` changes the state to `Completed`; the runner sends `TaskCompleteEvent` and releases the task afterward.
- A `SequenceTask` acquires appended children. Do not run those child instances separately.
- Do not retain or reuse a pooled task after it completes. Store durable result data outside the task before completion if another system needs it.
- Application code calls `Run()`; `TaskRunner` is an internal implementation detail and is created automatically.

## Related documentation

- [Events](./core_events.md) for completion-event routing
- [Schedulers](./core_schedulers.md) for callback-based delays without a task state machine
- [Pool](./core_pool.md) for pooled object and UniTask collection lifetimes
