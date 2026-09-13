# Graph editor extension

Extend the Graph Editor through its current factories and metadata so search, serialization, and view creation agree.

- Node views live under `Editor/Graph/UIElements/Graph/Nodes` and should represent runtime node state rather than own business data.
- Port behavior belongs in the port model and `CeresPortView`; do not encode connection rules only in drag handlers.
- Search entries must come from the same discoverable metadata used by node creation.
- Blackboard and inspector changes must write through the graph's serialized mutation path so undo and asset dirtiness remain correct.
- Use existing manipulators and internal bridges only for Editor behavior that public UI Toolkit/GraphView APIs cannot express.

If a task concerns execution pins, executable node views, functions, events, or Flow validation, also load `ceres-flow`. If it concerns general runtime UI rather than graph authoring, use `ceres-uitoolkit` instead.
