# Proposal

## Why

An invoice issued in a foreign currency carries no exchange rate today, so the KSeF XML omits `Fa/FaWiersz/KursWaluty` and nothing in the app states the PLN value of the sale. Issuing a USD invoice therefore means looking the rate up by hand and typing it into KSeF, which is where it gets typed wrong: a rate rounded to two decimals (`3.80` instead of the published `3.7998`) misstates the PLN taxable base by roughly 0.005%, silently and on every invoice.

## What Changes

- Add an exchange rate to the invoice model: the rate, the NBP table date it came from, and the table number, so the value that goes on an invoice can be traced back to a published NBP table.
- Derive a default **rate date** from the invoice date and the service month, following art. 31a of the VAT act: the relevant date is the earlier of the invoice date and the end of the service month, and the rate is the last NBP table A publication strictly before it.
- Fetch the rate from the NBP public API (table A, average rates) when the invoice currency is not PLN, pre-filling both the rate date and the rate in the Create Invoice form.
- Let both fields be edited. The rate date rule depends on when the tax obligation arose, which the app cannot always know, so the derived default is a starting point and not a verdict. A rate typed by hand is used as typed.
- Keep working offline: when NBP cannot be reached the fields stay empty and editable, and generation of DOCX and PDF is unaffected.
- Emit `Fa/FaWiersz/KursWaluty` in the KSeF XML for foreign-currency invoices, with the rate written at full published precision.
- Show the rate, its NBP table number and effective date, and the PLN equivalent in the invoice preview. The rate SHALL NOT appear on the DOCX or PDF invoice: those go to the customer, while the rate exists for Polish tax reporting and belongs in the KSeF XML only.
- Reject a Polish client billed in a foreign currency during KSeF validation. FA(3) requires the tax fields of such an invoice to be expressed in PLN while the sales values stay in the invoice currency; the app does not do that conversion, and today nothing stops the combination being configured. **BREAKING** for any configuration that pairs `country = "PL"` with a non-PLN currency and generates XML.

## Capabilities

### New Capabilities

- `exchange-rate`: Resolving, overriding and carrying the NBP exchange rate for an invoice issued in a currency other than PLN — the rate date rule, the NBP table A lookup, the manual override, offline behaviour, and how the rate and its provenance are displayed in the invoice preview.

### Modified Capabilities

- `ksef-xml-generation`: `Fa/FaWiersz/KursWaluty` is written for foreign-currency invoices and omitted for PLN invoices; the exchange rate becomes a required and precision-constrained field when the currency is not PLN; a Polish client with a foreign currency is rejected.

## Impact

- **New code**: an NBP table A client (`Exchange/`), reusing the failure-tolerant `HttpClient` pattern already established by `Update/UpdateChecker`; a rate-date derivation helper.
- **Modified code**: `Models/Invoice.cs` (three new fields, populated at construction); `Generation/KsefXmlGenerator.cs` (emission plus two validation rules); `Generation/DocxGenerator.cs` and `Generation/PdfGenerator.cs` (rate line); `Tui/Views/CreateInvoiceView.cs` (two conditional fields, asynchronous pre-fill, preview).
- **External dependency**: `api.nbp.pl`, unauthenticated, called only when the invoice currency is not PLN. Tests use recorded fixtures and make no network calls, matching how the FA(3) schema closure is already vendored.
- **Determinism**: `KsefXmlGenerator.Generate` stays synchronous and performs no network access. The rate reaches it as data on the invoice, the same way generation time reaches it through an injected `TimeProvider`, so the existing determinism requirement is preserved.
- **Not in scope**: converting `P_14_n` to PLN for a Polish client invoiced in a foreign currency, `KursWalutyZ` for advance invoices, and `KursUmowny`/`WalutaUmowna` for contractual rates.
