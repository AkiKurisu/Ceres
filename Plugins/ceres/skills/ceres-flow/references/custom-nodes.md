# Custom Flow nodes

Use a custom node for stateful behavior, nontrivial execution control, async work, dynamic ports, or a specialized editor representation. Prefer an executable function for a stateless method call.

## Design rules

- Choose the nearest existing runtime node base and follow its execution lifecycle.
- Declare data and execution ports through the established metadata; do not infer runtime behavior from editor labels.
- Port arrays need stable element identity and deterministic resize/serialization behavior.
- Generic nodes must define how type arguments are discovered, displayed, serialized, and preserved for player builds.
- A custom node view edits node state; runtime behavior remains in the runtime node.
- Search metadata must make the node discoverable without feature-local registration.

Check adjacent nodes under `Runtime/Flow/Models/Nodes` and their views under `Editor/Flow/UIElements/Nodes`. Reuse established validation and port compatibility checks. Do not create programmatic graphs unless the user explicitly requests it and the current source exposes a suitable supported path.
