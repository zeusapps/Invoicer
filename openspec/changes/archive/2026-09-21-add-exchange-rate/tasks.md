# Tasks

## 1. Rate Date Derivation And Invoice Model

- [x] 1.1 Add `ExchangeRate` (`decimal?`), `ExchangeRateDate` (`DateTime?`) and `ExchangeRateTable` (`string?`) to `Models/Invoice.cs`, and accept them in `Invoice.Create`; verify the project builds and existing `InvoiceOutputBehaviorTests` still pass
- [x] 1.2 Add a rate-date helper computing `min(invoiceDate, lastDayOf(serviceMonth))`; verify a new `ExchangeRateDateTests` covers the three cases in the `exchange-rate` spec (05 Oct/September → 30 Sep, 25 Oct/October → 25 Oct, 31 Oct/October → 31 Oct) plus both `MonthOffsetRule` values
- [x] 1.3 Add a helper reporting whether an invoice needs a rate (currency is not `PLN`, case-insensitive, whitespace trimmed); verify unit tests cover `PLN`, `pln`, `" PLN "`, `USD` and an empty currency

## 2. NBP Rate Lookup

- [x] 2.1 Add `Exchange/NbpRateProvider` issuing the table A range query `{D-14}/{D-1}` and returning the last entry as rate, effective date and table number; verify against a recorded fixture that a Sunday 20.09.2026 rate date resolves to 3.7998 from `182/A/NBP/2026` effective 18.09.2026
- [x] 2.2 Verify with a fixture that a rate date falling on a working day that has its own published table resolves to the *preceding* table (17.09.2026 → 3.7639 from 16.09.2026), which is the `strictly before` rule
- [x] 2.3 Map every failure — unreachable host, timeout, non-success status, 404 with no data in range, unparseable body — to a result carrying a message rather than throwing, following the `Update/UpdateChecker` pattern; verify each path has a test asserting a message and no exception
- [x] 2.4 Parse the rate as `decimal` preserving published precision; verify fixtures for 3.7998 (4 dp), 0.012007 (6 dp) and 0.00021425 (8 dp) round-trip without loss, and that no test in the suite performs a network call

## 3. KSeF XML Emission And Validation

- [x] 3.1 Write `KursWaluty` as the last element of `FaWiersz` when the currency is not `PLN`, formatted `0.######`; verify a generated USD invoice contains `3.7998` in the correct position and passes `KsefSchemaValidator`
- [x] 3.2 Omit `KursWaluty` for a `PLN` invoice; verify a generated PLN invoice contains no `KursWaluty` and still validates
- [x] 3.3 Add validation rejecting a missing or non-positive rate when the currency is not `PLN`; verify no file is written and the error names the exchange rate
- [x] 3.4 Add validation rejecting a rate with more than six decimal places; verify 0.00021425 produces an error naming the six-decimal limit and that no rounding occurs
- [x] 3.5 Add validation rejecting client country `PL` combined with a non-PLN currency; verify no XML file is written, the error states the combination is unsupported, and DOCX/PDF generation for the same invoice still succeeds
- [x] 3.6 Verify `KsefXmlGenerator.Generate` remains synchronous and performs no network access, and that the existing byte-identical determinism test still passes with a rate present on the invoice
- [x] 3.7 Verify a generated document contains no `KursWalutyZ`, `KursUmowny` or `WalutaUmowna` element

## 4. Create Invoice Form And Preview

- [x] 4.1 Add `Rate Date` and `Exchange Rate` fields plus a `Refresh` button to the form pane, shown only when the selected client's currency is not `PLN`; verify by running the app that the rows appear for a USD client and are absent for a PLN client
- [x] 4.2 Pre-fill the rate date from the derived default and refresh it when the client or invoice date changes, reusing the events that already refresh the service month; verify the date tracks a changed invoice date
- [x] 4.3 Fetch the rate off the UI thread and apply the result via `Application.Invoke`; verify by running the app that the fields populate without freezing and that no control is touched from a continuation thread
- [x] 4.4 Re-resolve the rate when the rate date is edited, and use a hand-typed rate exactly as entered without attributing it to an NBP table; verify typing `3.80` over `3.7998` produces `3.80` in the generated XML
- [x] 4.5 On lookup failure, show the message and leave both fields empty and editable; verify with the network disabled that DOCX and PDF generation still succeed and that a typed rate generates XML
- [x] 4.6 Show the rate, table identifier, effective date and PLN equivalent of the net amount in the preview; verify a 5000.00 USD invoice at 3.7998 previews a PLN equivalent of 18999.00

## 5. DOCX And PDF Output — dropped

Built, then removed at the user's direction: the exchange rate is for Polish tax reporting and
belongs in the KSeF XML only, not on the documents the customer receives. `DocxGenerator`,
`PdfGenerator`, `InvoiceText` and `InvoiceDocumentTextTests` are unchanged by this change.

- [x] 5.1 ~~Add a bilingual exchange-rate line to `DocxGenerator`~~ — dropped, not wanted
- [x] 5.2 ~~Add the same line to `PdfGenerator`~~ — dropped, not wanted

## 6. Verification

- [x] 6.1 Run `dotnet build` and `dotnet test` and verify the whole suite passes with no new warnings
- [x] 6.2 Generate a real USD invoice end to end in the running app and verify the XML validates against the vendored FA(3) schema and the rate matches the NBP table shown in the preview
- [x] 6.3 Run `openspec validate add-exchange-rate --strict` and verify it reports no issues
