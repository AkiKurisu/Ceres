# Localization

| Field | Value |
|---|---|
| Version | 1.1.2 |
| Status | Living |
| Date | 2026-10-18 |
| Related Specs | [Data Tables](../DataDriven/data-tables.md), [Runtime Settings](../Settings/runtime-settings.md) |

This specification owns the runtime localization contract: how text is stored,
which language is shown, how missing text falls back, and how UI follows a
language change. Usage belongs to the
[Localization guide](../../Documentations/docs/core_localization.md).

## Purpose and Boundary

Localization turns stable keys into player-visible text in the current language.
Ceres provides the storage, lookup, language selection, UI binding and authoring
tools. The product owns the keys, the text, the list of languages it ships, the
source language and the fonts that cover them.

Keys are never shown to players on purpose; a key appears only when no language
has text for it.

## Model

Text lives in string tables. A string table is a [Data Table](../DataDriven/data-tables.md)
whose row identifier is the key and whose row holds the text and an Editor-only
note for translators. Each table holds one language. A table's name combines a
group and a language, with the language as its last dot-separated segment; the
group only organizes files. Keys are global across groups.

Language codes follow BCP 47. The product declares one source language. Source
tables define every key; tables of other languages may translate a subset.

## Lookup and Fallback

A lookup returns the text of the current language, then the source language,
then the key itself. Empty text counts as missing. Formatting arguments are
applied after the fallback.

In the Editor and development players every missing key is reported once per
language. A pseudo language derives text from the source language with accented
characters, brackets and extra length, so layouts can be checked for
untranslated and overflowing text without a translation.

## Language Selection

The current language is a player setting stored in a config. An empty value
follows the system language when the product ships it, otherwise the source
language. Changing the language applies immediately and notifies observers once;
no restart is needed.

## Loading

String tables are discovered through a shared content label and loaded at
startup. Every language present is loaded, so a language change needs no further
loading. Content packs such as mods ship additional tables under the same label;
a later table overrides earlier text for the same key and language. Until tables
finish loading, lookups fall back to keys, and observers are notified when
loading completes.

In the Editor outside Play Mode, tables are read from the project so previews
show real text, and edits to tables take effect without entering Play Mode.

## UI

UI elements bind displayed text, such as a label or tooltip, to a key in their
layout asset; bound text refreshes when the language changes or tables load.
Text assigned from code reads keys at assignment time and refreshes on the same
notification.

A panel root can carry a style class naming the current language, kept in sync
on every change, so product styles can vary by language. The settings window
carries the class on its root. How settings and an open settings window follow a
language change is owned by [Runtime Settings](../Settings/runtime-settings.md).

## Authoring Tools

String tables are edited as ordinary Data Tables. The Editor also provides:

- Export and import of one group as CSV, with one row per key and one column per
  language plus the note, for translators and translation platforms. Import
  creates missing language tables next to the source table and writes only
  non-empty cells.
- A report per group and language of missing keys, which exist in the source but
  have no text, and orphan keys, which exist only in a translation.

## Validation

Validate fallback order and empty-text handling, one missing-key report per key
and language, pseudo-language derivation, immediate language changes with a
single notification, override order of content-pack tables, key fallback before
loading completes, and refresh of bound and code-assigned text.
