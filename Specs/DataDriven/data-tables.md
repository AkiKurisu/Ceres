# Data Tables

For authoring steps and public APIs, see the
[Data Driven guide](../../Documentations/docs/core_data_driven.md).

## Purpose and Boundary

A Data Table stores keyed, project-authored records of one row schema. Tables are
authored in the Editor and are read-only at runtime. Process-wide values that
players or tools change at runtime belong to Configs, not Data Tables.

A row schema is a C# type implementing `IDataTableRow`. Its persisted members
follow Unity's serialization rules for fields. Rows reference assets through soft
references, never through `UnityEngine.Object` fields, so a table carries no
asset dependencies of its own.

## Model

A table records its row schema once and holds rows in authored order. Every row
has an identifier that is unique within the table and is the key for lookup.
Row order is significant and is preserved by every representation.

## Authoring and Import Flow

The authoring flow is source table → import → runtime table. The source is a
`.datatable` text file and is the only persistent form of the table. It must stay
readable and mergeable in version control: UTF-8 without escaped non-ASCII text,
the schema written once, one object per row, and a stable member order so that an
unchanged row produces an unchanged diff.

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

## Runtime Format

The runtime table is binary and is produced only by the importer. Each row can be
read on its own, so lookup by identifier decodes only that row and a table never
needs to decode rows it does not serve. Members marked as Editor-only are kept in
the source and omitted from the runtime table.

Runtime tables expose no mutation. Rows returned by lookup are owned by the table
and must be treated as read-only. Content that extends a project, such as a mod,
ships additional tables rather than modifying loaded ones.

Binary row data is keyed by member name so that a table built against an earlier
schema still reads under the current one, with the same add and remove tolerance
as the source.

## Loading and Ownership

Tables are ordinary assets in the content pipeline. Managers own sets of tables,
load them through the Resource system and keep the public read API independent of
the storage format. Addressables registration is an explicit Editor action; the
group, address and labels derive from the row schema. Import never registers a
table, so projects that manage content groups themselves keep full control.
