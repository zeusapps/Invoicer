# exchange-rate Specification

## Purpose
Gives an invoice issued in a currency other than PLN a traceable exchange rate: which date determines it, where its value comes from, how it can be corrected by hand, and how it and its source are shown to the person issuing the invoice and to the recipient.

## Requirements

### Requirement: Carry An Exchange Rate On Foreign-Currency Invoices

An invoice SHALL carry an exchange rate, the date of the National Bank of Poland (NBP) table the rate was taken from, and that table's identifier.

These values SHALL apply only when the invoice currency is not `PLN`. For an invoice in `PLN` the system SHALL NOT resolve, request, display or require an exchange rate.

The rate SHALL express how many PLN one unit of the invoice currency is worth.

#### Scenario: Invoice in PLN

- **WHEN** an invoice is prepared for a client whose currency is `PLN`
- **THEN** no exchange rate is requested or required, and no exchange rate fields are shown

#### Scenario: Invoice in a foreign currency

- **WHEN** an invoice is prepared for a client whose currency is `USD`
- **THEN** the system resolves an exchange rate and records the rate, the NBP table date and the table identifier alongside the invoice

### Requirement: Derive The Default Rate Date

The system SHALL derive a default **rate date** — the date that determines which exchange rate applies — from the invoice date and the service month, as the earlier of:

- the invoice date; and
- the last day of the invoice's service month.

This follows art. 31a of the VAT act: the rate is determined by the day the tax obligation arose, except where the invoice is issued before that day, in which case the invoice date determines it. The service month is already derived from the invoice date and the client's month offset rule.

The derived date SHALL be a default only. The system SHALL NOT treat it as authoritative, because the day a tax obligation arises is not always derivable from the data the system holds.

#### Scenario: Invoice issued after the service month ends

- **WHEN** an invoice dated 5 October 2026 has service month September 2026
- **THEN** the default rate date is 30 September 2026

#### Scenario: Invoice issued during the service month

- **WHEN** an invoice dated 25 October 2026 has service month October 2026
- **THEN** the default rate date is 25 October 2026

#### Scenario: Invoice dated on the last day of its service month

- **WHEN** an invoice dated 31 October 2026 has service month October 2026
- **THEN** the default rate date is 31 October 2026

### Requirement: Resolve The Rate From The NBP Average Rate Table

For a given rate date, the system SHALL use the average exchange rate (`kurs średni`, table A) published by the NBP for the **last publication date strictly earlier than the rate date**.

The rate date itself SHALL NOT be used even when a table was published on it, and days with no published table — weekends and public holidays — SHALL be skipped by moving further back. The system SHALL NOT require a holiday calendar to do this; absence of a published table is itself the evidence that a day was not a working day.

The system SHALL record the effective date and the identifier of the table it used, so that any rate on an invoice can be traced to a published NBP table.

#### Scenario: Rate date falls on a Sunday

- **WHEN** the rate date is Sunday 20 September 2026 and NBP published no table on 19 or 20 September
- **THEN** the rate is taken from the table effective Friday 18 September 2026, identified as `182/A/NBP/2026`, giving 3.7998 PLN per USD

#### Scenario: Rate date falls on a working day with its own published table

- **WHEN** the rate date is Thursday 17 September 2026, for which NBP published table `181/A/NBP/2026`
- **THEN** that table is not used, and the rate is taken from the preceding table effective Wednesday 16 September 2026

#### Scenario: Rate date follows a long non-working period

- **WHEN** the rate date falls after a run of consecutive days on which no table was published
- **THEN** the rate is taken from the most recent table published before that run

### Requirement: Preserve The Published Precision Of The Rate

The system SHALL carry the rate at exactly the precision NBP published it, and SHALL NOT round, pad or truncate it.

NBP publishes table A rates at a precision that varies by currency — four decimal places for most major currencies, and up to eight for low-value ones — so the system SHALL NOT assume a fixed number of decimal places.

#### Scenario: Rate published with four decimal places

- **WHEN** NBP publishes a USD rate of 3.7998
- **THEN** the invoice carries 3.7998, not 3.80 or 3.799800

#### Scenario: Rate published with six decimal places

- **WHEN** NBP publishes an HUF rate of 0.012007
- **THEN** the invoice carries 0.012007

### Requirement: Allow The Rate Date And Rate To Be Overridden

The system SHALL present the rate date and the rate as editable values before an invoice is generated, pre-filled with the derived date and the resolved rate.

- Editing the rate date SHALL re-resolve the rate from the NBP table for the new date.
- Editing the rate SHALL replace the resolved value and SHALL use it exactly as entered, without rounding or correction.
- A manually entered rate SHALL be recorded as such, and SHALL NOT be attributed to an NBP table it did not come from.

Pre-filling is what protects against a rate typed from memory or rounded by hand, so the system SHALL pre-fill whenever it can rather than presenting an empty field.

#### Scenario: User corrects the rate date

- **WHEN** the user changes the rate date to a different date
- **THEN** the rate, table date and table identifier are re-resolved for that date and the displayed values update

#### Scenario: User enters a rate by hand

- **WHEN** the user types 3.80 over a pre-filled rate of 3.7998
- **THEN** the invoice carries 3.80 exactly, and the invoice does not claim that 3.80 came from an NBP table

### Requirement: Refuse A Rate Date That Has Not Happened Yet

When the rate date is later than the current date, the system SHALL report that no rate exists for it yet and SHALL NOT contact the rate source.

A rate date in the future has no published rate by definition, and asking for one yields a rejection the user cannot act on. The message SHALL name the date and SHALL say that an earlier rate date, or entering the rate once it is published, is the way forward.

#### Scenario: Rate date later than today

- **WHEN** the rate date is 16 October 2026 and the current date is 15 October 2026
- **THEN** no request is made to the rate source, and the message states that no rate has been published for 16.10.2026 yet

#### Scenario: Rate date is today

- **WHEN** the rate date is the current date
- **THEN** the lookup proceeds normally, because the rate comes from a publication earlier than that date

### Requirement: Discard A Resolved Rate When The Client Changes

When the selected client changes, the system SHALL discard any rate, table date and table identifier already resolved, and SHALL resolve the rate again for the newly selected client.

A rate belongs to the currency it was resolved for, so it SHALL NOT carry over to another client. Two clients can share a rate date while needing different currencies, so an unchanged rate date SHALL NOT be treated as grounds for reusing the previous result.

#### Scenario: Switching between two foreign-currency clients

- **WHEN** the user switches from a `USD` client with a resolved rate to a client whose currency is different, on the same rate date
- **THEN** the previous rate and its NBP table are no longer shown, and a fresh rate is resolved for the new client

#### Scenario: Switching to a PLN client and back

- **WHEN** the user selects a `PLN` client and then returns to a foreign-currency client
- **THEN** the exchange rate fields are populated again rather than left empty

### Requirement: Tolerate An Unavailable Rate Source

When the NBP rate cannot be retrieved — the service is unreachable, times out, returns an error, or has no data for the requested period — the system SHALL report that plainly, leave the rate fields empty and editable, and SHALL NOT substitute a guessed, cached-as-current or zero rate.

Failure to retrieve a rate SHALL NOT block DOCX or PDF generation, and SHALL NOT prevent the user from entering a rate by hand.

#### Scenario: Rate source unreachable

- **WHEN** the NBP service cannot be reached while preparing a foreign-currency invoice
- **THEN** the system shows that the rate could not be retrieved, the rate fields remain empty and editable, and DOCX and PDF generation remain available

#### Scenario: User supplies the rate after a failed lookup

- **WHEN** the rate could not be retrieved and the user types a rate
- **THEN** the invoice is generated using the typed rate

### Requirement: Show The Rate And Its Source In The Preview

For a foreign-currency invoice, the system SHALL show the rate, the NBP table identifier and effective date it came from, and the PLN equivalent of the net amount, in the invoice preview.

The exchange rate SHALL NOT appear on the generated DOCX or PDF invoice. Those are the documents the customer receives, and the rate exists for Polish tax reporting rather than for the customer; it belongs in the KSeF XML only. DOCX and PDF output SHALL therefore be unchanged by the presence or absence of a rate.

#### Scenario: Preview of a foreign-currency invoice

- **WHEN** a USD invoice for 5000.00 is previewed with a rate of 3.7998 from table `182/A/NBP/2026` effective 18 September 2026
- **THEN** the preview shows the rate, the table identifier, the effective date, and a PLN equivalent of 18999.00

#### Scenario: Document for a foreign-currency invoice

- **WHEN** a DOCX or PDF invoice is generated in USD with a resolved exchange rate
- **THEN** the document contains no exchange rate line
