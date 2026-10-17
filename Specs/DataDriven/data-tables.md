# Data Tables

| Field | Value |
|---|---|
| Version | 1.1.2 |
| Status | Living |
| Date | 2026-10-18 |

For authoring steps and public APIs, see the
[Data Driven guide](../../Documentations/docs/core_data_driven.md).

## Purpose and Boundary

A Data Table stores keyed, project-authored records of one row schema. Tables are
authored in the Editor and are read-only at runtime. Process-wide values that
players or tools change at runtime belong to Configs, not Data Tables.

A row schema declares the persisted members of a row. Persisted members follow
Unity's serialization rules for fields and need no serialization markup. Rows
reference assets only through soft references, never through direct asset
object references, so a table carries no asset dependencies of its own.

## Model

A table records its row schema once and holds rows in authored order. Every row
has an identifier that is unique within the table and is the key for lookup.
Row order is significant and is preserved by every representation.

## Authoring and Import Flow

The authoring flow is source table → import → runtime table. The source is a
text file and is the only persistent form of the table. It must stay readable
and mergeable in version control: every row is a separate entry and member order
is stable, so an unchanged row produces an unchanged diff.

The importer derives the runtime table from the source. The imported table is not
an editing surface. Editing works on a document created from the source; saving
writes the source back and the import runs again. Editor tools that create or
change tables follow the same path, so the source and the runtime table cannot
diverge.

Import fails with an actionable error and produces no table when the source is
malformed, names an unknown schema, or contains duplicate row identifiers. Row
validation reported by the schema is an import warning; it does not block the
table.

The runtime table depends on its schema. A change to a schema's persisted members
reimports every table that uses it. Source members unknown to the schema are
ignored and absent members take their defaults, so adding or removing a member
does not require editing existing sources.

## Inline Rows

Rows that only Editor tools consume, such as the content list a tool asset
compiles, may be stored inline in the asset that owns them instead of living in a
separate source. Inline rows open in the same editing document and editors as a
source and are written back into the owner asset on save. They are never
imported and have no runtime table; anything loaded at runtime comes from a
source table.

## Runtime Table

The runtime table is a compact binary form produced only by the importer. Its
layout is private to the importer and the reader; nothing outside them depends
on it. Each row can be read on its own, so lookup by identifier decodes only that
row and a table never decodes rows it does not serve. Members marked as
Editor-only are kept in the source and omitted from the runtime table. A
persisted member that exists only in the Editor must be marked Editor-only, so
the Editor and players agree on the persisted members.

Runtime tables expose no mutation. Rows returned by lookup are owned by the table
and must be treated as read-only. Content that extends a project, such as a mod,
ships additional tables rather than modifying loaded ones.

Decoding is fully determined at compile time. Nothing is generated at runtime,
so tables decode in ahead-of-time compiled players.

### Schema Compatibility

A runtime table stays readable when the running schema differs from the schema
it was built against, such as content in a separately built package or mod. The
table is matched to the running schema by member name, once per table, before
its first row is read:

- A member present in both with the same kind of value is read.
- A member the table does not contain takes its initial value.
- A member the running schema no longer has, or whose kind of value changed, is
  skipped.
- Nested types follow the same rules.

This gives built content the same add and remove tolerance as the source.
Renaming an enumeration member keeps the values stored in built tables. Tables imported in the
project always match their schema, because changing a schema reimports them, and
pay no matching cost.

### Performance Contract

For the same rows, a Data Table loaded from an AssetBundle must not be slower than a
ScriptableObject holding a row list when every row is read, and must not be
larger. Loading a table and reading a few rows must stay proportional to the rows
read rather than to the table size.

## Loading and Ownership

Tables are ordinary assets in the content pipeline. Managers own sets of tables,
load them through the Resource system and keep the public read API independent of
the storage format. Addressables registration is an explicit Editor action; the
group, address and labels derive from the row schema. Import never registers a
table, so projects that manage content groups themselves keep full control.

## Validation

Validate a change to these flows with round trips of representative sources
through import and lookup, import failure for malformed sources, unknown schemas
and duplicate identifiers, reading tables built against an earlier schema after
members are added, removed or change kind, Editor-only member omission in player
builds, and the performance contract against the row-list baseline.
