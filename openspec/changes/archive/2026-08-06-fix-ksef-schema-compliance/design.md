## Context

`KsefXmlGenerator` writes FA(3) XML through a shared `WriteParty` helper used for both `Podmiot1` (seller) and `Podmiot2` (buyer). The helper emits `KodKraju` whenever the party carries a country code, and `KsefInvoiceDocument.FromInvoice` sets `CountryCode: "PL"` on the seller. The official schema disagrees:

```
schemat.xsd:1541   <xsd:complexType name="TPodmiot1">
                     <xsd:sequence>
                       <xsd:element name="NIP"   type="etd:TNrNIP"/>
                       <xsd:element name="Nazwa" type="tns:TZnakowy512"/>
                     </xsd:sequence>
```

`TPodmiot1` is a closed sequence of two elements. `TPodmiot2` is different — its choice branch for a foreign identifier does define an optional `KodKraju` before `NrID` — so the buyer block is correct and only the seller block is wrong. Both reference drafts in `tests/Invoicer.Tests/TestData/` agree: seller identification is `NIP` then `Nazwa`.

The same inspection clears the other two differences observed between a rejected file and an accepted one. `DataWytworzeniaFa` resolves to `etd:TDataCzas`, which is plain `xsd:dateTime` with `whiteSpace="collapse"` and no pattern, so .NET's round-trip format with seven fractional digits is valid; the FA(3) element merely bounds the value to `2025-09-01T00:00:00Z .. 2050-01-01T23:59:59Z`. The absent `xmlns:xsi`/`xmlns:xsd` declarations are unused prefixes with no bearing on validity.

The deeper constraint is process, not code. The archived `add-ksef-xml-generation` design listed "building a full XSD validator pipeline" as a non-goal and recorded a follow-up: *if strict XSD validation becomes required, add it as a follow-up change without redesigning the generator pipeline.* Two import rejections later (the first produced the `JST`/`GV` commit, the second this change), that condition is met. The existing tests compare hand-picked values between generated output and a fixture, which is structurally incapable of detecting an element that should not be there.

The schemas are published and reachable at stable CRD URLs, forming a four-file closure:

```
schemat.xsd                                    ns: .../wzor/2025/06/25/13775/     184 KB
  └─ import ─→ StrukturyDanych_v10-0E.xsd      ns: .../eD/DefinicjeTypy/           32 KB
       └─ include ─→ ElementarneTypyDanych_v10-0E.xsd                              12 KB
            └─ include ─→ KodyKrajow_v10-0E.xsd    (no further references)         40 KB
```

## Goals / Non-Goals

**Goals:**

- Produce FA(3) XML that validates against the official schema, so import rejection stops being the discovery mechanism for structural defects.
- Make `DataWytworzeniaFa` mean what the schema says it means, without giving up a testable determinism guarantee.
- Keep schema validation offline, deterministic, and fast enough to live in the normal `dotnet test` run.
- Leave the generator's architecture, the DOCX/PDF paths, and the shipped binary untouched.

**Non-Goals:**

- Validating at runtime inside the application before writing a file (see Decision 5 and Open Questions).
- Submitting to the KSeF API, or implementing its business-rule layer beyond schema conformance.
- Fixing the double-encoded supplier address in `config.toml`.
- Supporting a future wzór. A new schema version means a new namespace and therefore a new change.

## Decisions

**1. Fix the seller block by scoping `KodKraju` to the buyer, not by clearing the seller's country code.**

`WriteParty` writes `KodKraju` for whichever party supplies one; the fix is to stop supplying one for `Podmiot1`, or to gate the element on the party being the buyer. Preferred: keep `WriteParty` honest about what each element means and stop passing a seller `CountryCode`, since the value has no representation in `TPodmiot1`. `Podmiot1/Adres/KodKraju` comes from `AddressCountryCode` and stays as it is.

- Alternative — add a `bool includeIdentificationCountryCode` flag to `WriteParty`: rejected as a second way to say the same thing; the party record already distinguishes `Nip` from `Identifier`.
- Alternative — split into `WriteSeller`/`WriteBuyer`: rejected for this change. The two blocks now differ in three ways (`KodKraju`, `NIP` vs `NrID`, `JST`/`GV`), so a split is defensible, but it is a refactor and the XSD test is what actually prevents recurrence. Worth revisiting if a fourth difference appears.

**2. Vendor all four schema files verbatim under `tests/Invoicer.Tests/TestData/Schema/`.**

Byte-identical copies of the published files, with a short `README.md` beside them recording each source URL and the date retrieved.

- Rationale: refreshing the schema becomes re-downloading rather than re-patching, and provenance stays verifiable.
- Alternative — fetch at test time: rejected. It makes the suite depend on a government host being up, breaks offline and sandboxed CI, and silently changes what "passing" means when the remote file changes.
- Alternative — ship the schemas with the application: rejected. 268 KB in a single-file self-contained executable buys nothing at runtime today.

**3. Resolve the import/include chain with a local `XmlResolver` rather than rewriting `schemaLocation` attributes.**

The vendored files reference each other by absolute `http://crd.gov.pl/...` URL. A resolver that maps those URIs to files in `Schema/` and **throws on any URI it does not recognise** keeps the copies pristine and turns an accidental network dependency into a loud test failure.

- Alternative — rewrite `schemaLocation` to relative paths: rejected; it edits official documents and makes every future refresh a manual re-patch.
- Alternative — pre-load every file into the `XmlSchemaSet` by namespace and hope the resolver is never consulted: rejected as implicit. `xsd:include` is same-namespace and location-driven, so this depends on set-deduplication behaviour rather than on anything guaranteed.

**4. Treat validation warnings as failures, not just errors.**

`XmlReaderSettings` must enable `XmlSchemaValidationFlags.ReportValidationWarnings`, and the test must fail on warnings as well as errors. Without this, a document whose namespace matches no schema in the set produces a *warning* ("no schema information") and validates vacuously — a test that passes precisely when it has stopped testing anything. Failures should report every accumulated message with line and position, not just the first.

**5. Validate in the test suite only, for now.**

The defect class this change closes is structural: the generator emits a fixed element layout, so a layout that validates once validates for every input. Data-driven violations are a different class and are discussed under Risks.

- Alternative — validate in `KsefXmlGenerator.Generate` before writing: deferred, not dismissed. It would catch data-driven violations at the point of generation with a clear message, but it costs schema-loading time on every run and grows the shipped binary. Revisit when a real data-driven rejection occurs, or when the field-level validator is extended (Open Questions).

**6. Inject the clock through an optional `TimeProvider` parameter.**

`Generate(Invoice invoice, TimeProvider? timeProvider = null)` defaulting to `TimeProvider.System`, with `DataWytworzeniaFa` taken from `GetUtcNow()`. Callers are unchanged.

- Rationale: preserves an exact determinism test — same invoice, same fixed clock, byte-identical output — instead of one that scrubs a node before comparing and therefore no longer covers the whole document.
- Alternative — normalize `DataWytworzeniaFa` out of both documents in the determinism test: no production change, but it weakens the assertion and leaves the timestamp itself untested.
- Alternative — keep deriving the timestamp from `InvoiceDate`: rejected. It is what makes any pre-September-2025 invoice date schema-invalid, and it misstates the field's meaning in every other case.

**7. Do not assert that the existing reference fixtures validate.**

They are `Wersja robocza` (working draft) exports, and a draft is not required to be a complete, valid FA(3) document. They stay what they are: evidence of the shape the application accepts. Running the validator over them once during implementation is still worth doing as an informational check — a surprise there would say something useful about draft exports or about the resolver.

Outcome of that check (task 4.5): both fixtures validate with zero errors and zero warnings. The caution above was unnecessary in practice — these particular draft exports are complete FA(3) documents — and the clean result on documents the generator did not produce is independent evidence that the resolver and schema set are wired up correctly. Still not asserted, since validity is a property of these two files rather than a guarantee about draft exports in general.

## Risks / Trade-offs

- **Schema-valid is not the same as KSeF-accepted.** The XSD constrains structure, types, and lengths; it does not check NIP checksums, that `P_13_1 + P_14_1 = P_15`, or the API's business rules. → Keep `KsefXmlGenerator.Validate` as the field-presence gate and describe the new test as necessary-but-not-sufficient wherever it is documented. A green suite must not be read as "KSeF will accept this".
- **Data-driven violations remain possible at runtime.** The schema constrains values, not just structure: `Nazwa` is `TZnakowy512`, `NIP` carries a pattern, `KodWaluty` is an enumeration, and `P_7` has a length bound. A client name over 512 characters or a malformed TIN in `config.toml` yields a file that passes today's tests and fails at import — the exact failure mode this change is meant to end, one layer down. → Fixture-based tests cannot cover this; either runtime validation (Decision 5) or property-style tests over hostile config values would. Recorded as an open question rather than silently accepted.
- **Vendored schemas drift from the published ones.** → The `README.md` records source URLs and retrieval date; refreshing is a deliberate re-download. A wzór change alters the namespace, which the existing metadata constants already centralize.
- **Previously generated XML on disk is invalid.** `output/2026/Invoices/20260511_SAMPLE_PL.xml` and any real invoices generated before this fix carry the bad element. → Regenerate anything not yet submitted; nothing needs migrating in the codebase.
- **`TimeProvider` changes a public signature.** It is an optional parameter on a static method in a single-project application, so the blast radius is nil, but it is still API surface added for testability. Accepted as the cheaper option against an assertion that skips a node.

## Migration Plan

1. Vendor the schemas and add the copy rule to `Invoicer.Tests.csproj` — the existing `TestData\*.xml` glob covers neither `.xsd` files nor subdirectories.
2. Add the resolver and the validation test. It should fail against current output, naming `KodKraju`. A test that passes before the generator is touched has not been wired up correctly.
3. Fix the generator (seller block, then timestamp) and watch the test go green.
4. Update the determinism test to pin the clock.
5. Regenerate any unsubmitted invoice XML.

Rollback: revert the generator change; the vendored schemas and the validation test are additive and harmless on their own.

## Open Questions

- Should field-level validation be extended to the XSD's value constraints (`TZnakowy512` lengths, `TNrNIP` pattern, `KodWaluty` enumeration), or should the application validate the finished document against the schema at runtime? The second subsumes the first and is less work to keep correct; the first gives better error messages. Deferred until a data-driven rejection is actually observed, but it is the most likely source of the next import failure.
- Should validation extend to the DOCX/PDF paths' shared inputs — that is, should `config.toml` values be validated on load rather than at generation? Related to the excluded encoding defect, and better decided alongside it.

**Resolution (task 5.4): both should become one follow-up change, not two.** They are the same question asked at different layers — whether `config.toml` values are trustworthy by the time they reach a generator. The double-encoded address excluded from this change is the same defect class as an over-length `Nazwa` or a malformed TIN: bad config data that no structural test can catch and that surfaces as a corrupted or rejected document. Validating config on load would address the encoding defect, the value-level XSD constraints, and the DOCX/PDF paths together; adding runtime XSD validation would cover only the XML path and only at the end. Recommend a follow-up change scoped to config-data validation on load, with runtime XSD validation reconsidered there rather than assumed.
