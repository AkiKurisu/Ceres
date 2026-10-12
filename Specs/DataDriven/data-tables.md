# Data Tables

For authoring steps and public APIs, see the
[Data Driven guide](../../Documentations/docs/core_data_driven.md).

## Purpose and Boundary

A Data Table stores keyed, project-authored records of one row schema. Tables are
authored in the Editor and are read-only at runtime. Process-wide values that
players or tools change at runtime belong to Configs, not Data Tables.

A row schema is a C# type implementing `IDataTableRow`. Its persisted members
follow Unity's serialization rules for fields, and neither the schema nor the
types it contains need serialization attributes. Rows reference assets through
soft references, never through `UnityEngine.Object` fields, so a table carries no
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
the source and omitted from the runtime table. A persisted member that exists
only in the Editor must be marked Editor-only, so the Editor and players agree on
the layout.

Runtime tables expose no mutation. Rows returned by lookup are owned by the table
and must be treated as read-only. Content that extends a project, such as a mod,
ships additional tables rather than modifying loaded ones.

### Schema Descriptor and Positional Rows

Member names are stored once per table, not once per row. Every runtime table
carries a schema descriptor: for the row schema and for each type it contains, the
persisted member names and their value kinds in encoding order, together with a
layout hash. Rows store member values by position in that order.

A value kind identifies how a member is encoded: integer, floating point, boolean,
string, enumeration, list, nested type, Unity value type or soft reference.
Enumerations are stored as their underlying integer, so renaming an enumeration
member keeps existing values.

### Reading Under the Current Schema

When the descriptor's layout hash equals the running schema's, rows decode
directly by position. Tables imported in the project always take this path,
because changing a schema reimports its tables.

When the hashes differ, the table maps its descriptor to the running schema by
member name, once per table, before decoding its first row. A member found in both
with the same value kind is read. A member missing from the descriptor takes its
initial value. A member missing from the running schema, or whose value kind
changed, is skipped. Nested types map the same way. This keeps content built
against an earlier schema, such as a separately built package or mod, readable
with the same add and remove tolerance as the source.

### Decoding Code

Decoding code for each row schema and the types it contains is generated at
compile time. Nothing is generated at runtime, so tables decode in IL2CPP players.
A schema type with non-public persisted members must be declared `partial` so the
generated code can reach them; otherwise compilation reports which type and
member need it.

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
