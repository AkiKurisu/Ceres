# Graph model and variables

The runtime Graph assembly owns serializable graph structure and reusable data semantics; Flow adds executable behavior on top.

- `CeresGraph` owns graph data and lifecycle.
- `CeresGraphModule` extends compilation or runtime behavior without coupling the base graph to a product.
- `CeresGraphCompiler` validates and compiles graph structure.
- `SharedVariable<T>` and its concrete types carry value plus scope semantics. Use an existing scope before creating a new global registry.
- `UObjectLink`, `ManagedReferenceType`, and serialized type helpers preserve Unity and managed references across serialization boundaries.

When adding a graph field, trace serialization, clone/copy behavior, editor mutation, compiler input, and IL2CPP preservation. Prefer stable serialized names and explicit migrations for persisted assets. Runtime models must not reference GraphView, EditorWindow, or `UnityEditor`.

Source anchors: `Runtime/Graph/Models/Graph`, `Runtime/Graph/Models/Graph/Variables`, and `Runtime/Graph/Models`.
