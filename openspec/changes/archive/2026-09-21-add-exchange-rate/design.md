# Design

## Context

See `proposal.md` — Why. Constraints that shape the approach:

- `KsefXmlGenerator.Generate` is synchronous and pure apart from writing the file, and `ksef-xml-generation` requires byte-identical output for identical input under a fixed `TimeProvider`. Any network call inside generation breaks that guarantee and forces the method async.
- The Create Invoice preview (`Tui/Views/CreateInvoiceView.cs`) is a single read-only `Label` sized with `Dim.Auto(DimAutoStyle.Text)`. It renders one text blob and cannot host an editable control; editable fields live in the form pane on the left.
- `Update/UpdateChecker` already establishes the project's pattern for an optional network call from a desktop app: short timeout, every failure mapped to a message, never an exception reaching the UI.
- Tests must not touch the network. The FA(3) schema closure is vendored under `tests/Invoicer.Tests/TestData/Schema` with a resolver that throws on any URI outside it; rate lookups follow the same principle with recorded fixtures.

The FA(3) schema was read directly from the vendored copy. `KursWaluty` is `Fa/FaWiersz/KursWaluty`, type `TIlosci` (`xsd:decimal`, max 22 total digits, max 6 fraction digits, pattern `-?([1-9]\d{0,15}|0)(\.\d{1,6})?`), optional, positioned after `Procedura` and before `StanPrzed`.

## Goals / Non-Goals

**Goals:**

- Keep XML generation synchronous, network-free and deterministic.
- Make the rate and the date that produced it visible before the invoice is issued, not after.
- Fail loudly on anything the schema cannot represent, rather than rounding to fit.

**Non-Goals:**

- Showing the rate on the DOCX or PDF invoice. Those documents go to the customer; the rate is a Polish tax-reporting value and belongs in the KSeF XML only.

- A general currency-conversion layer. One rate, one currency pair, PLN on one side.
- Caching or persisting fetched rates. Each invoice resolves its own rate; a rate already written into a generated document is the record.
- Deriving the tax obligation date from anything richer than the invoice date and service month.

## Decisions

### Resolve the rate in the TUI, pass it to the generator as data

The rate is fetched in `CreateInvoiceView` and carried on `Invoice` as three plain fields (rate, table effective date, table identifier). `KsefXmlGenerator` reads them and performs no I/O of its own.

This mirrors how generation time is handled: `Generate(invoice, timeProvider)` already takes the one non-deterministic input as a parameter rather than reaching for `DateTime.UtcNow`. Doing the same for the rate keeps the determinism requirement intact and keeps `Generate` synchronous, which matters because it is called from a Terminal.Gui event handler.

*Alternative considered:* fetch inside the generator. Rejected — it makes `Generate` async, makes the output depend on when it ran, and would require mocking HTTP in every schema-validation test.

### Use a date-range query to find the preceding publication day

The relevant date `D` is the earlier of the invoice date and the last day of the service month. The rate comes from the last NBP table A publication **strictly before** `D`.

NBP has no "latest before date" endpoint, and the single-date endpoint returns **404** for any day without a table — which is every weekend and every Polish public holiday. Instead the implementation requests a window ending the day before `D` and takes the last entry:

```
GET https://api.nbp.pl/api/exchangerates/rates/a/{code}/{D-14}/{D-1}/?format=json
                                                                 ^^^ take last entry
```

A 14-day window covers the longest run of consecutive non-publication days in the Polish calendar with margin, and is far inside NBP's 93-day limit for a single query. Crucially this needs no holiday calendar: NBP's own publication gaps already encode which days were working days.

Verified against live data for the worked example — `D` = Sunday 20.09.2026 resolves to table `182/A/NBP/2026` effective Friday 18.09.2026, rate 3.7998.

*Alternative considered:* request `D-1` and retry backwards on 404. Rejected — up to four round trips over a holiday weekend, each one a chance to fail, versus one request that cannot miss.

*Alternative considered:* `/rates/a/{code}/last/1/`. Rejected — it returns the newest table regardless of the rate date, which is only correct when issuing an invoice today and silently wrong for a back-dated one.

### The editable date is the relevant date, not the table date

Two dates exist: the relevant date `D` that determines which rate applies, and the effective date of the table actually used. The user edits `D`; the table date is derived and shown read-only beside the rate.

This is the right way round because the user's uncertainty is about which date governs — invoice date or service completion — not about which NBP table exists. It also means any date is editable without error: typing a Sunday resolves fine, whereas an editable table date would 404 on two days in seven.

```
+-- Create Invoice ---------------+-- Preview -----------------------+
| Invoice Date:   [05.10.2026]    | Net:     5,000.00 USD            |
| Amount:         [5000.00   ]    | VAT:     N/A (np I)              |
| Service Month:  September 2026  | Gross:   5,000.00 USD            |
|                                 |                                  |
| Rate Date:      [30.09.2026]    | Rate:    3.7213 PLN/USD          |
| Exchange Rate:  [3.7213    ]    |          NBP A 189/A/NBP/2026    |
|                       [Refresh] |          effective 29.09.2026    |
|                                 | Net PLN: 18,606.50               |
| Output Format: [x]DOCX [x]PDF   |                                  |
|                [x]KSeF XML      | Output:  2026/Invoices/          |
+---------------------------------+----------------------------------+
        ^ both rows hidden when currency == PLN
```

### One expression covers both branches of art. 31a

`D = min(invoiceDate, lastDayOf(serviceMonth))`.

Art. 31a ust. 2 — invoice issued before the tax obligation arises, so the invoice date governs — applies exactly when `invoiceDate < lastDayOf(serviceMonth)`. Otherwise ust. 1 applies and the service completion date governs. The minimum selects correctly in both cases, so there is no branch on `MonthOffsetRule`:

| Rule | Invoice date | Service month | `D` |
|---|---|---|---|
| `early_previous` | 05 Oct | September | 30 Sep |
| `early_previous` | 25 Oct | October | 25 Oct |
| `early_current` | 05 Oct | October | 05 Oct |

The result is a default, not a verdict — the tax obligation can arise on payment receipt, which the app cannot see. Hence the field is editable.

### Store the rate as `decimal`, format with `0.######`

`decimal` is exact for the values NBP publishes and matches every other monetary field in the codebase. `0.######` renders 3.7998 as `3.7998` and 0.012007 as `0.012007`, adding no trailing zeros, and satisfies the `TIlosci` pattern. The existing `0.##` used for amounts would silently truncate a six-decimal rate and must not be reused here.

Rates needing more than six decimals — `IDR` publishes at eight — are rejected by validation rather than rounded. Rounding a rate is exactly the failure this change exists to prevent, and doing it silently inside the generator would be worse than doing it by hand.

### Reject Polish client plus foreign currency

FA(3) expects the tax fields of a foreign-currency invoice in PLN while sales values stay in the invoice currency. Implementing that conversion means touching `P_14_n` and the `Adnotacje` block and is a materially larger change. Until then the combination is rejected during KSeF validation, which turns a silent misstatement into a refusal. DOCX and PDF are unaffected, so the configuration remains usable for non-KSeF output.

## Risks / Trade-offs

- **Terminal.Gui v2 UI-thread affinity** → the lookup runs off the UI thread and must marshal results back via `Application.Invoke` before touching any control. Writing to a `TextField` from a continuation thread is the likely source of a hard-to-reproduce hang.
- **Fetch in flight when Generate is pressed** → validation already blocks XML output when the rate is missing, so the worst case is a clear error rather than an invoice with a blank rate. The rate field is not auto-filled after generation begins.
- **A stale rate after the user edits the invoice date** → the rate date is recomputed on invoice-date and client changes, the same events that already refresh the service month. Anything the user typed by hand is preserved until they change the rate date themselves.
- **The default rate date encodes a reading of art. 31a** → it is a pre-filled, editable field displayed next to the resulting rate and table, so a wrong default is visible before issuing rather than discovered afterwards. The design does not claim to determine the tax obligation date.
- **`api.nbp.pl` availability and future shape** → every failure path leaves the field editable, so the app degrades to what it does today (type the rate in) rather than becoming unusable. Response parsing is covered by recorded fixtures, so a shape change surfaces as a parse failure with a message, not a crash.
- **`KursWaluty` is optional in the schema** → the XSD cannot tell us whether MF's business validation requires it for a foreign-currency invoice. Emitting it is correct either way; this design does not depend on the answer.

## Open Questions

- Whether a KSeF invoice already submitted with a hand-rounded rate warrants a correcting invoice is a question for an accountant, not for this design. It changes nothing here: for an `np I` supply no amount on the invoice derives from the rate, and the PLN base reported in JPK is computed independently.
