# Flow Runtime Execution

## Ownership

Flow adds event-driven execution to the typed Graph model. Authored graph data
and runtime execution state have separate lifetimes: reusable assets describe
behavior, while a runtime owner supplies execution context, variable scope, and
cleanup. Sharing an asset must not accidentally share instance-owned state.
Scene components and ScriptableObject logic containers can both own runtimes;
the choice follows the application's lifetime rather than requiring a common
scene component base.

Flow builds on Graph and Core without depending on Gameplay. Gameplay may expose
its APIs to Flow. Editor authoring and generation remain separate from runtime
execution and do not introduce Editor dependencies into Player code.

## Authoring to execution

Authors connect typed events, functions, variables, and data dependencies.
Runtime creation binds this authored behavior to its owner. Events start forward
execution; values needed by an executing node are evaluated through its data
dependencies. Runtime-owned subscriptions and state end with their owner.

Generated C# execution is derived from the authored graph, not a second manually
maintained behavior. It preserves event semantics, context, and lifetime while
avoiding interpreted graph traversal in the generated path. Custom behavior
must have supported generation before it participates in that path; generated
programs do not silently embed interpreted execution for unsupported nodes.

## Editing and build boundaries

Disabled generation uses the normal graph runtime. In Editor, missing or stale
generated programs may fall back to that runtime so authors can continue editing.
A build with generation enabled requires a current, supported generated program;
missing, stale, or unsupported output fails validation rather than silently
changing the shipped execution mode.

Editor hot reload replaces the runtime used by subsequent events. In-flight
execution may finish against the previous graph. This boundary must remain
explicit when debugging state changes.

## Validation and guides

Validate instance isolation, owner cleanup, typed event/data behavior, generated
execution parity, stale-generation build rejection, and hot-reload event
boundaries when changing these flows.

[Runtime Architecture](../../Documentations/docs/flow_runtime_architecture.md)
covers container selection and examples; [Code Generation](../../Documentations/docs/flow_codegen.md)
covers authoring and generation commands; [Debugging](../../Documentations/docs/flow_debugging.md)
covers inspection and hot reload.
