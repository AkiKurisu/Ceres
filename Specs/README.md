# Ceres Design Specifications

These specifications define the framework's design principles, subsystem boundaries and end-to-end flows for repository developers and agents. [Documentations](../Documentations/docs/getting_started.md) contains self-contained guidance for external users and integrators.

The following architecture and workflow conventions do not replace the structured visual-design format of `UIElement/DESIGN.md`.

## What belongs here

- Responsibilities and dependency direction between framework services and host projects.
- How authored assets become build artifacts and runtime state.
- Resource ownership, publication, lifecycle and release.
- Correctness requirements, failure boundaries and supported behavior that implementations must preserve.

Keep implementation structure, private fields, detailed algorithms and repeated API inventories in source. Public API examples, configuration, troubleshooting and extension instructions belong in developer documentation.

## Maintenance

- Describe the current design and update it with changes to the contract.
- Keep one authoritative specification for each contract and link it from related specifications rather than copying it.
- Preserve caller-facing requirements in usage documentation when they are needed to use an API correctly. Explain the underlying system principle here without repeating the full API reference.
- Do not add local investigation logs, build measurements, migration histories, descriptions of superseded behavior or to-do lists to these specifications.
- Describe concepts rather than code. Do not reference types, methods, fields, namespaces, source or assembly paths, enumeration values, configuration keys, menu paths, Unity version notes, code or layout samples, binary layouts and encodings, or tuning constants. The visual tokens of `UIElement/DESIGN.md` are the exception.
- Do not attribute a design to the projects or references it was derived from; state the rule itself.
- State framework rules only. Product policy, such as a product's languages, fonts, pages or presentation choices, belongs to the product's own specifications.
- Public documentation, site navigation, the repository root README, and skills (including their reference files) do not reference specifications. Skills use public guides and package source. Specifications may link to public guides for usage examples.
- Group ordinary specifications by subsystem and use descriptive lowercase kebab-case filenames. `DESIGN.md` is reserved for the structured frontend design-system format; preserve the existing UI Element document, including its colors, typography and spacing metadata.

## Index

| Specification | Design boundary | Developer guide |
|---|---|---|
| [Content Pipeline](ContentPipeline/content-build-and-release.md) | Content ownership, dependency/build flow, update correctness, releases and runtime mounting. | [Content Pipeline](../Documentations/docs/core_content_pipeline.md) |
| [Data Tables](DataDriven/data-tables.md) | Row schemas, text source and import flow, runtime tables, schema compatibility and loading. | [Data Driven](../Documentations/docs/core_data_driven.md) |
| [Animation Packing](AnimationPacking/animation-packing.md) | Animation fidelity, conversion/import flow and runtime asset lifetime. | [Animation Packing](../Documentations/docs/animation_packing.md) |
| [Event Dispatch](Events/event-dispatch.md) | Event ownership, frame-phase delivery, re-entrancy, batching, propagation, subscription lifetime and the debugger. | [Events](../Documentations/docs/core_events.md) |
| [Flow](Flow/runtime-execution.md) | Graph ownership, execution flow, generated runtime, build boundaries and hot reload. | [Runtime containers](../Documentations/docs/flow_runtime_architecture.md) |
| [Game World](Gameplay/game-world.md) | World and subsystem lifecycle, tick phases and order, and world access during destruction. | [World and Actors](../Documentations/docs/gameplay_world.md) |
| [Localization](Localization/localization.md) | String tables, lookup and fallback, language selection, loading and UI refresh. | [Localization](../Documentations/docs/core_localization.md) |
| [Runtime Settings](Settings/runtime-settings.md) | Setting model, edit state, apply and persistence semantics, ownership and the settings window. | [Settings](../Documentations/docs/gameplay_settings.md) |
| [UI Elements](UIElement/DESIGN.md) | Structured frontend design system, visual tokens and interaction rules. | [UI Toolkit](../Documentations/docs/ui_toolkit.md) |
