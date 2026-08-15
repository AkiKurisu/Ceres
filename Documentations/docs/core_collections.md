# Collections

The `Ceres.Collections` namespace provides small runtime collections used by Ceres systems. `SparseArray<T>` is the main public container; the remaining types are focused helpers rather than a general-purpose collections library.

## SparseArray

`SparseArray<T>` assigns an integer slot when an element is added. Removing an element leaves a hole and does not move other allocated elements. A later `Add` reuses a free slot before growing the backing storage.

Use it when another structure needs to retain array-like indices while unrelated elements are inserted or removed, such as runtime registries and handle tables. Use `List<T>` when elements should remain contiguous or their indices do not escape the collection.

```csharp
using Ceres.Collections;

var actors = new SparseArray<ActorState>(
    length: 64,
    capacity: 4096);

int playerIndex = actors.Add(new ActorState("Player"));
int enemyIndex = actors.Add(new ActorState("Enemy"));

actors.RemoveAt(playerIndex);

// enemyIndex still refers to the same allocated slot.
ActorState enemy = actors[enemyIndex];

// The next insertion may reuse playerIndex.
int replacementIndex = actors.Add(new ActorState("Replacement"));
```

The constructor arguments have distinct roles:

- `length` creates that many initially free slots.
- `capacity` is the maximum number of backing slots. `Add` throws when growth would exceed it.

Pass a non-negative `length` that does not exceed `capacity`; the constructor does not validate that relationship.

### Slot contract

- An allocated index remains stable until that slot is removed.
- A removed index can be reused by a later `Add`; the index alone is not a generation-safe handle.
- `IsAllocated(index)` distinguishes live slots from holes and returns `false` for an out-of-range index.
- Reading an unallocated slot returns `default`; writing one has no effect.
- `RemoveAt` expects a currently allocated, in-range index.
- `Count` reports allocated elements, not backing-slot count.
- Enumeration visits allocated elements in ascending slot order and skips holes.

`AddUninitialized` reserves a slot containing `default`. `Clear` returns every existing slot to the free list. `Shrink` removes only trailing free storage; it does not compact live elements or change their indices.

Use normal enumeration when the slot number is not required. Keep the indices returned by `Add` in the owning registry when later lookup by slot is required.

```csharp
foreach (ActorState actor in actors)
{
    Process(actor);
}
```

## Capability index

| Type | Contract |
| --- | --- |
| `PriorityQueue<T>` | Binary min-heap for `IComparable<T>`. `Peek` and `Dequeue` return the smallest item. Enumeration exposes heap storage order, not sorted order. Empty access is the caller's responsibility. |
| `NativeCollectionsExtensions` | `DisposeSafe` guards disposal of `NativeArray<T>`, `NativeList<T>`, and `NativeParallelMultiHashMap<TKey, TValue>`. `Resize` grows a `NativeArray<T>` when needed and preserves an existing larger allocation; it does not preserve data when reallocating. |
| `IOCContainer` | Internal exact-type instance registry used by higher-level Ceres systems. It is not a public dependency-injection container. |
| `ArrayUtils` | Copy-based mutation and lookup helpers for managed arrays. Prefer `List<T>` for mutation-heavy code. |
| `RandomList<T>` | Weighted selection that avoids the immediately previous item and decays the selected weight. |
| `ShufflingExtension` | In-place Fisher-Yates shuffle plus small random-selection helpers for managed lists. |

`PriorityQueue<T>` uses the type's `CompareTo` result directly. Lower values are dequeued first:

```csharp
using Ceres.Collections;

var queue = new PriorityQueue<int>();
queue.Enqueue(30);
queue.Enqueue(10);
queue.Enqueue(20);

int first = queue.Dequeue(); // 10
```
