# Data and reactive utilities

Inspect the owning subsystem before introducing a new container, serializer, or update loop.

## Decision guide

- **DataDriven**: use `DataTable` rows and `DataTableManager` for authored lookup data. Row validation belongs at import or load boundaries, not at every read.
- **Schedulers**: use `Scheduler` and its frame/timer helpers when work belongs to Ceres timing. Do not add another hidden runner for the same lifetime.
- **Serialization**: use `SerializedType`, `SerializedObject<T>`, `SaveLoadSerializer`, and redirect attributes for Ceres polymorphic data. A renamed serialized type needs an intentional redirect.
- **Collections**: prefer `SparseArray<T>`, `PriorityQueue<T>`, `RandomList<T>`, and existing extensions when their semantics fit. Use standard collections when Ceres adds no benefit.
- **R3**: compose observables with the existing Ceres extensions and attach disposables to the actual owner. Programmatic state synchronization should avoid feedback loops.

## Source anchors

- `Runtime/Core/DataDriven`
- `Runtime/Core/Schedulers`
- `Runtime/Core/Serialization`
- `Runtime/Core/Collections`
- `Runtime/Core/R3`

Public concepts live in the corresponding `Documentations/docs/core_*.md` pages. Verify behavior against source when persistence, disposal, or update order matters.
