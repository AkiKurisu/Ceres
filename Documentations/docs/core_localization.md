# Localization

Localization turns keys into text in the player's language. Text lives in string
tables, one Data Table per language, and the current language can change at any
time without a restart.

## Create String Tables

A string table is a `.datatable` file whose row type is `LocalizedTextRow`. The
row id is the key, `text` is the translation and `note` is an Editor-only hint
for translators. Name each table `{group}.{language}`:

```
Assets/Localization/Menu.ja.datatable
Assets/Localization/Menu.en.datatable
Assets/Localization/Menu.zh-Hans.datatable
```

The group only organizes files; keys are global. Add the tables to Addressables
with the label `Localization`. Content packs can ship more tables under the same
label to add or override text.

Set the language that defines every key in the project config:

```json
"Localization": "{\"SourceLanguage\":\"ja\"}"
```

Register `Ceres.Localization.LocalizationModule` in the module config so tables
load at startup.

## Read Text

```csharp
using Ceres.Localization;

string title = LocalizationSystem.Get("menu.title");
string saved = LocalizationSystem.Format("menu.saved", fileName);
```

`Get` returns the text of the current language, then of the source language,
then the key itself. The Editor and development players report each missing key
once. Text assigned from code should be assigned again when
`LocalizationSystem.Changed` fires; it fires on a language change and when tables
finish loading.

## Bind UI Toolkit Text

Bind `text`, `label` or `tooltip` to a key in UXML. The value follows the
language without code:

```xml
<ui:Label>
    <Bindings>
        <Ceres.Localization.LocalizedTextBinding property="text" key="menu.title" />
    </Bindings>
</ui:Label>
```

## Fonts per Language

`LanguageClass.Bind(root)` keeps the class `ceres-lang-{code}` on an element and
returns a subscription to dispose with the UI. Choose a primary font per language
in USS instead of relying on fallbacks, which would mix glyphs of two fonts in one
word:

```css
.menu-root.ceres-lang-zh-Hans {
    -unity-font-definition: url("Fonts/SimplifiedChinese.asset");
}
```

The settings window adds the class to its root.

## Change the Language

The player's choice is `LocalizationConfig.Language`. An empty value follows the
system language when tables for it exist, otherwise the source language. Expose it
as a setting:

```csharp
var language = section.Add(new ChoiceSetting<string>("display.language", LocalizationSystem.Get("settings.language"))
    .Option(string.Empty, LocalizationSystem.Get("settings.language.system"))
    .Option("ja", "日本語")
    .Option("en", "English"));
language.Bind<LocalizationConfig>(config => config.Language);
```

Settings read their wording when the registry is built. A language change
rebuilds the registry, and an open settings window rebuilds on the same page
without ending its session. Give
the window host a `TextSource` so its runtime wording is created again too.

The language `qps-ploc` shows pseudo text derived from the source language,
useful for finding untranslated or clipped text.

## Translate

**Tools > Ceres > Localization** lists every group with the translated count per
language. **Export CSV** writes one row per key with the note and one column per
language; **Import CSV** writes the non-empty cells back and creates missing
language tables next to the source table. Expanding a group lists missing keys
and keys that only a translation defines. The toolbar previews any loaded
language, or the pseudo language, in the Editor.

Related API: <xref:Ceres.Localization.LocalizationSystem>,
<xref:Ceres.Localization.LocalizedTextBinding>, and
<xref:Ceres.Localization.LocalizationConfig>.
