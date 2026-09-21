# Spec Delta

## ADDED Requirements

### Requirement: Write The Exchange Rate For Foreign-Currency Invoices

When the invoice currency is not `PLN`, the generated XML SHALL carry the invoice's exchange rate in `Fa/FaWiersz/KursWaluty`, positioned where the FA(3) schema defines it: after `Procedura` and before `StanPrzed`.

The value SHALL be written at the invoice's full recorded precision, with no rounding, padding or trailing zeros added, and SHALL satisfy the schema's numeric form — no leading zeros on the integer part and no trailing decimal point.

When the invoice currency is `PLN`, no `KursWaluty` element SHALL be written.

The system SHALL NOT write `Fa/KursWalutyZ` or `Fa/WarunkiTransakcji/KursUmowny`. Those fields cover advance invoices and contractual rates respectively, neither of which this system issues.

#### Scenario: USD invoice carries the rate

- **WHEN** an XML invoice is generated for a `USD` invoice with an exchange rate of 3.7998
- **THEN** `Fa/FaWiersz/KursWaluty` is `3.7998`, it appears as the last element of `FaWiersz`, and the document validates against the FA(3) schema

#### Scenario: Rate with six decimal places

- **WHEN** an XML invoice is generated with an exchange rate of 0.012007
- **THEN** `KursWaluty` is `0.012007` and the document validates

#### Scenario: PLN invoice omits the rate

- **WHEN** an XML invoice is generated for a `PLN` invoice
- **THEN** no `KursWaluty` element is present anywhere in the document, and the document validates

#### Scenario: Other rate fields are never written

- **WHEN** any XML invoice is generated
- **THEN** the document contains no `KursWalutyZ`, `KursUmowny` or `WalutaUmowna` element

### Requirement: Take The Exchange Rate From Invoice Data

XML generation SHALL read the exchange rate from the invoice data supplied to it and SHALL NOT retrieve a rate while generating.

This keeps generation free of network access, so that the existing determinism guarantee continues to hold: the same invoice data generated twice produces documents differing at most in `Naglowek/DataWytworzeniaFa`.

#### Scenario: Generation performs no rate lookup

- **WHEN** an XML invoice is generated for a foreign-currency invoice with no network available
- **THEN** generation succeeds using the rate recorded on the invoice

#### Scenario: Determinism with a rate present

- **WHEN** the same foreign-currency invoice data is generated twice with the same fixed time source
- **THEN** both documents are byte-identical

### Requirement: Validate The Exchange Rate Before Writing XML

For an invoice whose currency is not `PLN`, the system SHALL validate before writing the XML file that:

- an exchange rate is present and greater than zero; and
- the rate has at most six decimal places, which is the maximum the FA(3) schema can represent.

When either check fails the system SHALL NOT write the XML file and SHALL return a validation error naming the exchange rate as the cause. A rate too precise to represent SHALL be reported as such rather than silently rounded to fit, because a silently rounded rate misstates the PLN taxable base.

#### Scenario: Foreign-currency invoice without a rate

- **WHEN** XML output is requested for a `USD` invoice with no exchange rate
- **THEN** no XML file is written and the error states that an exchange rate is required for a non-PLN invoice

#### Scenario: Rate too precise for the schema

- **WHEN** XML output is requested with an exchange rate of 0.00021425
- **THEN** no XML file is written and the error states that the rate exceeds six decimal places

#### Scenario: PLN invoice without a rate

- **WHEN** XML output is requested for a `PLN` invoice with no exchange rate
- **THEN** validation passes and the XML file is written

### Requirement: Reject A Polish Client Invoiced In A Foreign Currency

The system SHALL NOT generate KSeF XML for an invoice whose client country is Poland and whose currency is not `PLN`. No XML file SHALL be written, and the error SHALL state that the combination is unsupported.

FA(3) requires the tax amounts of such an invoice to be expressed in PLN while the sales values stay in the invoice currency. The system does not perform that conversion, so emitting the amounts unconverted would produce an invoice that is schema-valid and factually wrong. DOCX and PDF generation SHALL be unaffected.

#### Scenario: Polish client billed in USD

- **WHEN** XML output is requested for a client with country `PL`, VAT rate 23 and currency `USD`
- **THEN** no XML file is written and the error states that a Polish client cannot be invoiced in a foreign currency

#### Scenario: Foreign client billed in a foreign currency

- **WHEN** XML output is requested for a client with country `US` and currency `USD`
- **THEN** validation passes
