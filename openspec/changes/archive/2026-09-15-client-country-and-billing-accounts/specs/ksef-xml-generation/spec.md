## MODIFIED Requirements

### Requirement: Validate Required KSeF Fields Before Writing XML

The system SHALL validate required KSeF fields before writing the XML file and SHALL fail generation for XML output if required values are missing or invalid.

Client country and client VAT SHALL be validated as follows:

- The client's country SHALL be required and SHALL be a country code accepted by the FA(3) schema.
- A client outside Poland SHALL have a VAT rate of 0. A Polish client SHALL have a VAT rate of 23, 22, 8, 7 or 5.
- A VAT number SHALL be required when the country is Poland or another EU member state, and optional otherwise.
- For a Polish client, the VAT number with any `PL` prefix removed SHALL be a valid NIP.
- For another EU client, a letter prefix on the VAT number, if present, SHALL match the EU VAT code of the client's country.

The client's billing account SHALL resolve to a defined account with a non-empty IBAN. The system SHALL NOT substitute a default country or account for a missing value.

#### Scenario: Required field missing

- **WHEN** XML output is requested and a required KSeF field is missing
- **THEN** the system does not write the XML file and returns a validation error identifying the missing field

#### Scenario: Client country missing

- **WHEN** XML output is requested for a client with an empty country
- **THEN** no XML file is written and the error states that the client country is required

#### Scenario: EU client without VAT

- **WHEN** XML output is requested for a client with country `DE` and an empty VAT
- **THEN** no XML file is written and the error states that VAT is required for that client

#### Scenario: Non-EU client without VAT

- **WHEN** XML output is requested for a client with country `US` and an empty VAT
- **THEN** validation passes

#### Scenario: Foreign client with a VAT rate

- **WHEN** XML output is requested for a client with country `US` and VAT rate 23
- **THEN** no XML file is written and the error states that a client outside Poland must have a VAT rate of 0

#### Scenario: Polish client with an unsupported rate

- **WHEN** XML output is requested for a client with country `PL` and VAT rate 0
- **THEN** no XML file is written and the error states that the rate is not supported for a Polish client

#### Scenario: VAT prefix contradicts country

- **WHEN** XML output is requested for a client with country `DE` and VAT `FR12345678901`
- **THEN** no XML file is written and the error states that the VAT prefix does not match the client country

### Requirement: Identify The Seller By NIP And Name Only

The seller identification block `Podmiot1/DaneIdentyfikacyjne` SHALL contain exactly `NIP` followed by `Nazwa`, as required by the schema type `TPodmiot1`. The system SHALL NOT emit a `KodKraju` element in that block. The seller's country code SHALL continue to be emitted in `Podmiot1/Adres`, where the schema defines it. Buyer identification in `Podmiot2/DaneIdentyfikacyjne` is governed by the requirement "Identify The Buyer According To Client Country".

#### Scenario: Seller identification contains no country code

- **WHEN** an XML invoice is generated
- **THEN** `Podmiot1/DaneIdentyfikacyjne` contains only `NIP` and `Nazwa`, in that order

#### Scenario: Seller address retains its country code

- **WHEN** an XML invoice is generated
- **THEN** `Podmiot1/Adres` contains `KodKraju` followed by `AdresL1`

#### Scenario: Buyer identification is unchanged

- **WHEN** an XML invoice is generated
- **THEN** the seller identification rules do not constrain `Podmiot2/DaneIdentyfikacyjne`, whose content follows the client's country as defined in "Identify The Buyer According To Client Country"

## ADDED Requirements

### Requirement: Identify The Buyer According To Client Country

`Podmiot2/DaneIdentyfikacyjne` SHALL use exactly one of the identification forms defined by the FA(3) type `TPodmiot2`. The form SHALL be selected from the client's country and VAT number, and `Nazwa` SHALL follow it:

- **Poland:** `NIP` holding the VAT number with any `PL` prefix and any whitespace removed.
- **Another EU member state:**
  - `KodUE` holding the country's EU VAT code, which is the country code except `GR` becomes `EL`;
  - then `NrVatUE` holding the VAT number with that prefix and any whitespace removed.
- **Outside the EU, with a VAT number:** `KodKraju` holding the client's country, then `NrID` holding the VAT number as entered.
- **Outside the EU, without a VAT number:** `BrakID` with value `1`.

The selection SHALL NOT depend on the letters at the start of the VAT number.

#### Scenario: Polish buyer

- **WHEN** an XML invoice is generated for a client with country `PL` and VAT `PL9999999999`
- **THEN** `Podmiot2/DaneIdentyfikacyjne` contains `NIP` = `9999999999` followed by `Nazwa`, and the document validates against the FA(3) schema

#### Scenario: EU buyer

- **WHEN** an XML invoice is generated for a client with country `DE` and VAT `DE123456789`
- **THEN** `Podmiot2/DaneIdentyfikacyjne` contains `KodUE` = `DE`, `NrVatUE` = `123456789`, then `Nazwa`, and the document validates

#### Scenario: Greek buyer uses EL prefix

- **WHEN** an XML invoice is generated for a client with country `GR` and VAT `EL123456789`
- **THEN** `KodUE` is `EL` and `NrVatUE` is `123456789`

#### Scenario: Non-EU buyer with tax ID

- **WHEN** an XML invoice is generated for a client with country `US` and VAT `12-3456789`
- **THEN** `Podmiot2/DaneIdentyfikacyjne` contains `KodKraju` = `US`, `NrID` = `12-3456789`, then `Nazwa`, and the document validates

#### Scenario: Non-EU buyer without tax ID

- **WHEN** an XML invoice is generated for a client with country `US` and an empty VAT
- **THEN** `Podmiot2/DaneIdentyfikacyjne` contains `BrakID` = `1` followed by `Nazwa`, contains no `KodKraju` or `NrID`, and the document validates

### Requirement: Take Buyer Address Country From Client

`Podmiot2/Adres/KodKraju` SHALL equal the client's country for every buyer, regardless of the identification form used.

#### Scenario: US buyer address

- **WHEN** an XML invoice is generated for a client with country `US`
- **THEN** `Podmiot2/Adres/KodKraju` is `US`

#### Scenario: Country independent of VAT prefix

- **WHEN** an XML invoice is generated for a client with country `US` and a VAT number that starts with `PL`
- **THEN** `Podmiot2/Adres/KodKraju` is `US`

### Requirement: Code The Tax Rate According To Client Country

The line tax rate `FaWiersz/P_12`, the invoice summary fields and the reverse-charge annotation `Adnotacje/P_18` SHALL be derived from the client's country and VAT rate. The mapping follows the FA(3) definitions of `TStawkaPodatku`:

- **Client outside the EU:** services outside Poland not covered by art. 100 ust. 1 pkt 4 of the VAT act.
  - `P_12` SHALL be `np I`;
  - the net amount SHALL be written to `P_13_8`;
  - no `P_14_x` element SHALL be written;
  - `P_18` SHALL be `1`.
- **Client in another EU member state:** services under art. 100 ust. 1 pkt 4.
  - `P_12` SHALL be `np II`;
  - the net amount SHALL be written to `P_13_9`;
  - no `P_14_x` element SHALL be written;
  - `P_18` SHALL be `1`;
  - `Podmiot1/PrefiksPodatnika` SHALL be `PL`.
- **Polish client:** `P_12` SHALL be the numeric rate, and `P_18` SHALL be `2`. The net and VAT amounts SHALL be written to the pair matching the rate:
  - 23 or 22: `P_13_1` and `P_14_1`;
  - 8 or 7: `P_13_2` and `P_14_2`;
  - 5: `P_13_3` and `P_14_3`.

In every case `P_15` SHALL equal the gross amount, which is the net amount when no VAT applies. The code `oo` SHALL NOT be used, because it is limited to domestic reverse charge.

#### Scenario: US client

- **WHEN** an XML invoice is generated for a client with country `US`, VAT rate 0 and net amount 2500
- **THEN** `P_12` is `np I`, `P_13_8` is `2500`, no `P_13_1` or `P_14_x` exists, `P_15` is `2500`, `P_18` is `1`, and the document validates against the FA(3) schema

#### Scenario: EU client

- **WHEN** an XML invoice is generated for a client with country `DE`, VAT `DE123456789`, VAT rate 0 and net amount 1000
- **THEN** `P_12` is `np II`, `P_13_9` is `1000`, no `P_14_x` exists, `P_15` is `1000`, `P_18` is `1`, `Podmiot1/PrefiksPodatnika` is `PL`, and the document validates

#### Scenario: Polish client at 23%

- **WHEN** an XML invoice is generated for a client with country `PL` and VAT rate 23
- **THEN** `P_12` is `23`, net and VAT are in `P_13_1` and `P_14_1`, `P_15` is net plus VAT, `P_18` is `2`, there is no `PrefiksPodatnika`, and the document validates

#### Scenario: Polish client at 8%

- **WHEN** an XML invoice is generated for a client with country `PL` and VAT rate 8
- **THEN** `P_12` is `8` and net and VAT are in `P_13_2` and `P_14_2`

### Requirement: Take Payment Details From The Client's Billing Account

`Fa/Platnosc/RachunekBankowy` SHALL be populated from the billing account assigned to the invoice's client:

- `NrRB` SHALL hold the account's IBAN with whitespace removed.
- `SWIFT` SHALL hold the account's SWIFT when it is non-empty.
- `NazwaBanku` SHALL hold the account's bank name when it is non-empty.

The elements SHALL appear in the order the schema defines.

#### Scenario: Account with bank name

- **WHEN** an XML invoice is generated for a client whose account has IBAN `PL42 1090 1320 0000 0001 5470 1995`, SWIFT `WBKPPLPP` and bank `Santander`
- **THEN** `RachunekBankowy` contains `NrRB` = `PL42109013200000000154701995`, `SWIFT` = `WBKPPLPP` and `NazwaBanku` = `Santander`, in that order, and the document validates

#### Scenario: Account without SWIFT or bank name

- **WHEN** the client's account has an IBAN but empty SWIFT and bank
- **THEN** `RachunekBankowy` contains only `NrRB`
