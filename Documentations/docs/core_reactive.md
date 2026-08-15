# Reactive Integration

Ceres adds a small integration layer between its runtime contracts and [R3](https://github.com/Cysharp/R3). The extensions live in `R3.Ceres` and cover event streams, subscription ownership, UGUI binding, and `ReactiveProperty<T>` JSON serialization.

This page documents the Ceres-specific bridge only. Observable creation and operators otherwise follow R3.

## Event observables

`AsObservable<TEvent>()` adapts a `Ceres.Events.CallbackEventHandler` to an R3 `Observable<TEvent>`. The default overload registers for `TrickleDown.NoTrickleDown`; pass `TrickleDown.TrickleDown` when the subscription must observe the trickle-down phase instead.

```csharp
using System;
using Ceres.Events;
using R3.Ceres;

IDisposable subscription = EventSystem.EventHandler
    .AsObservable<DamageEvent>()
    .SubscribeSafe(evt => ApplyDamage(evt.Amount));

// Required for handlers without an attached Behaviour lifetime.
subscription.Dispose();
```

When the source handler implements `IBehaviourScope`, Ceres also binds the observable registration to the attached `MonoBehaviour.destroyCancellationToken`. Destroying that behaviour completes the observable and unregisters the event callback.

### Pooled event lifetime

The adapter acquires each pooled `EventBase<T>` before forwarding it. `SubscribeSafe` invokes an `EventCallback<T>` and disposes that acquired reference after the callback returns.

Use `SubscribeSafe` for ordinary Ceres event consumption. Do not retain the event object after the callback, and do not let callback exceptions escape before the acquired reference is released. If a normal R3 `Subscribe` is used instead, the observer owns the acquired reference and must dispose it exactly once.

## Subscription ownership

`IDisposableUnregister` is the Ceres lifetime-owner contract:

```csharp
using System;
using R3.Ceres;

public sealed class SubscriptionOwner : IDisposableUnregister, IDisposable
{
    private readonly System.Collections.Generic.List<IDisposable> subscriptions = new();

    public void Register(IDisposable disposable)
    {
        subscriptions.Add(disposable);
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in subscriptions)
        {
            subscription.Dispose();
        }

        subscriptions.Clear();
    }
}
```

`AddTo(owner)` registers an `IDisposable` with that owner and returns the same disposable:

```csharp
using R3;
using R3.Ceres;

var owner = new SubscriptionOwner();
var value = new ReactiveProperty<int>(0);

value.Subscribe(OnValueChanged).AddTo(owner);

// Disposes every registered subscription.
owner.Dispose();
```

Ceres graphs, generated Flow runtimes, and pooled objects implement this contract where their lifetime should own subscriptions. In pooled code, bind to the pooled wrapper rather than its `GameObject`; pooled objects are released without necessarily being destroyed.

## UGUI two-way binding

`BindProperty` connects a supported UGUI control to a `ReactiveProperty<T>` in both directions and registers both subscriptions with an `IDisposableUnregister` owner.

| Control | Property |
| --- | --- |
| `Slider` | `ReactiveProperty<float>` |
| `Slider` | `ReactiveProperty<int>` |
| `Toggle` | `ReactiveProperty<bool>` |

The initial property value is applied immediately. Later property updates use `SetValueWithoutNotify` or `SetIsOnWithoutNotify`, preventing the UI write from feeding back into the property.

```csharp
using R3;
using R3.Ceres;
using UnityEngine.UI;

ReactiveProperty<float> volume = new(0.8f);
Slider volumeSlider = GetVolumeSlider();
IDisposableUnregister lifetime = GetViewLifetime();

volumeSlider.BindProperty(volume, lifetime);
```

For the integer slider overload, the control's `float` value is converted to `int` when written to the property.

## JSON conversion

`ReactivePropertyConverter` serializes an exact `ReactiveProperty<T>` as its current `Value`. Deserialization creates a new `ReactiveProperty<T>` containing the stored value; subscriptions and other runtime state are not serialized.

Ceres Config registers this converter in its Newtonsoft.Json settings. Add it explicitly when using separate serializer settings:

```csharp
using Newtonsoft.Json;
using R3;
using R3.Ceres;

public sealed class ReactiveModel
{
    public ReactiveProperty<float> Volume { get; set; } = new(1f);

    public static ReactiveModel RoundTrip(ReactiveModel source)
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new ReactivePropertyConverter());

        string json = JsonConvert.SerializeObject(source, settings);
        return JsonConvert.DeserializeObject<ReactiveModel>(json, settings);
    }
}
```

The converter applies only when the declared runtime type is exactly `ReactiveProperty<T>`. If value deserialization fails, it returns a default instance of that reactive property type.
