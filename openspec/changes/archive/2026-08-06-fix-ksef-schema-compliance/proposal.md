## Why

Generated KSeF XML is rejected on import by the KSeF Taxpayer Application because `KsefXmlGenerator` emits a `KodKraju` element inside `Podmiot1/DaneIdentyfikacyjne`, where the official FA(3) schema defines `TPodmiot1` as `sequence(NIP, Nazwa)` and permits nothing else. The failure is a whole-document schema violation, so the importer reports a generic error with no field identified, and the current tests cannot catch it because they assert selected values are equal rather than that the document is valid.

## What Changes

- Remove `KodKraju` from the seller's `DaneIdentyfikacyjne` block. The seller is identified by `NIP` and `Nazwa` only; the country code remains in `Podmiot1/Adres`, where the schema does define it. The buyer block is unaffected — `TPodmiot2` allows an optional `KodKraju` alongside `NrID`.
- Set `DataWytworzeniaFa` to the actual UTC generation timestamp instead of midnight of the invoice date. FA(3) constrains this field to `2025-09-01T00:00:00Z .. 2050-01-01T23:59:59Z`, so the current derivation makes any invoice dated before September 2025 schema-invalid, and misstates the document's creation time in every other case.
- Vendor the official FA(3) XSD and its three transitively referenced schemas into the test project, and validate generated XML against them so that any element that is unexpected, missing, or out of order fails `dotnet test` rather than a manual import attempt.
- Narrow the determinism guarantee so it covers structure and mapped business values but explicitly excludes the generation timestamp, which is now expected to differ between runs.
- Replace the placeholder identifiers in the default configuration. The shipped `Tin = "0000000000"` fails the schema type `TNrNIP`, which requires a nonzero leading digit, so a first-run user cannot produce a valid invoice even with the structural defect fixed. Found by validating the sample output rather than by reasoning about it.

No breaking changes: the XML contract moves toward the published schema, and DOCX/PDF output is untouched.

## Capabilities

### New Capabilities

- None. Schema conformance is a property of the XML this application already produces, so it belongs to the existing capability rather than a parallel one.

### Modified Capabilities

- `ksef-xml-generation`: adds a requirement that generated documents validate against the official FA(3) XSD; adds a requirement fixing seller identification to `NIP` + `Nazwa`; adds a requirement that `DataWytworzeniaFa` reflects actual generation time within the schema's permitted range; amends the existing determinism requirement to exclude that timestamp.

## Impact

- Affected code:
  - `src/Invoicer/Generation/KsefXmlGenerator.cs` — `WriteParty` no longer writes `KodKraju` for the seller; `KsefInvoiceDocument.FromInvoice` supplies a real UTC timestamp.
  - `src/Invoicer/Config/ConfigManager.cs` — schema-valid placeholder identifiers in `CreateDefault`. Affects new installations only, since an existing `config.toml` is never rewritten from the defaults.
  - `tests/Invoicer.Tests/KsefXmlGeneratorTests.cs` — new XSD validation test; determinism test must tolerate a varying `DataWytworzeniaFa`.
  - `tests/Invoicer.Tests/Invoicer.Tests.csproj` — the `TestData\*.xml` copy glob does not cover `.xsd` files.
- New checked-in assets (~268 KB total), retrieved from the Centralne Repozytorium Wzorów Dokumentów Elektronicznych:
  - `schemat.xsd` (FA(3), namespace `http://crd.gov.pl/wzor/2025/06/25/13775/`)
  - `StrukturyDanych_v10-0E.xsd`, `ElementarneTypyDanych_v10-0E.xsd`, `KodyKrajow_v10-0E.xsd`
- No new package dependencies; `System.Xml.Schema` is part of the framework. Tests must not resolve schemas over the network.
- Out of scope: the double-encoded supplier address in `config.toml` (`GeneraÅa JÃ³zefa` for `Generała Józefa`), which corrupts DOCX and PDF output as well as XML and is schema-valid. It warrants its own change.
