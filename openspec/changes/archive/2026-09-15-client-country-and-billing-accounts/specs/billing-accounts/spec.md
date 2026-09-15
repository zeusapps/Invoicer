## Purpose

Lets the supplier keep several bank accounts, assign one to each client, and have every invoice for that client carry that account's payment details. Several accounts may share a currency.

## ADDED Requirements

### Requirement: Define Multiple Billing Accounts

The configuration SHALL hold zero or more billing accounts. Each account SHALL have:

- a `key`, which is unique among accounts, non-empty, and used for references;
- a `label` for display;
- an `iban`, `bank` and `swift`;
- an optional `currency`.

Several accounts MAY share the same currency. Accounts SHALL be persisted in the configuration file and survive a save/load round trip unchanged.

#### Scenario: Two accounts round-trip

- **WHEN** a configuration with a PLN account and a USD account is saved and loaded again
- **THEN** both accounts are present with identical key, label, IBAN, bank, SWIFT and currency

#### Scenario: Accounts sharing a currency

- **WHEN** a configuration defines two accounts both with currency `PLN`
- **THEN** both accounts load and each can be assigned to clients

### Requirement: Supplier No Longer Holds Bank Details

Bank details SHALL be defined only on billing accounts. The supplier section of the configuration and the Supplier Info screen SHALL NOT contain IBAN, bank or SWIFT fields.

#### Scenario: Supplier screen

- **WHEN** a user opens Settings > Supplier Info
- **THEN** no IBAN, bank or SWIFT field is shown

#### Scenario: Saved supplier section

- **WHEN** the configuration is saved
- **THEN** the `[supplier]` section contains no `iban`, `bank` or `swift` keys

### Requirement: Manage Billing Accounts In The TUI

The application SHALL provide a Billing Accounts screen, reachable from the Settings menu, where a user can list, add, edit and delete accounts and save the configuration. The system SHALL refuse to save an account whose key is empty or duplicates another account's key. Changing an account's key SHALL update every client that referenced the old key. Deleting an account that is assigned to any client SHALL be refused, with a message naming those clients.

#### Scenario: Add an account

- **WHEN** a user adds an account on the Billing Accounts screen, fills in its fields and saves
- **THEN** the account is written to the configuration file

#### Scenario: Duplicate key rejected

- **WHEN** a user saves two accounts with the same key
- **THEN** the save is refused with an error naming the duplicate key

#### Scenario: Renaming a key keeps assignments

- **WHEN** a user changes an account's key from `USD` to `USD_WISE` while client `GREENFLOW` references `USD`
- **THEN** `GREENFLOW` references `USD_WISE` afterwards

#### Scenario: Deleting an assigned account

- **WHEN** a user deletes an account that client `GREENFLOW` references
- **THEN** the deletion is refused and the message names `GREENFLOW`

### Requirement: Assign A Billing Account To Each Client

Each client SHALL reference exactly one billing account by key through `billing_account`. The client editor SHALL let the user pick the account from the defined accounts, and the choice SHALL be persisted.

#### Scenario: Assign account in editor

- **WHEN** a user selects account `USD` for client `GREENFLOW` in the client editor and saves
- **THEN** the configuration stores `billing_account = "USD"` for `GREENFLOW`

### Requirement: Warn On Currency Mismatch

When a client's assigned account has a currency and that currency differs from the client's currency, the client editor SHALL show a warning. The warning SHALL NOT prevent saving or invoice generation. No warning SHALL be shown when the account has no currency.

#### Scenario: Mismatched currency

- **WHEN** a client with currency `USD` is assigned an account with currency `PLN`
- **THEN** the client editor shows a currency mismatch warning, and saving still succeeds

#### Scenario: Account without currency

- **WHEN** a client with currency `USD` is assigned an account with no currency
- **THEN** no currency warning is shown

### Requirement: Invoices Use The Client's Billing Account

Every generated invoice SHALL take its payment details from the billing account assigned to the invoice's client. The IBAN, bank and SWIFT in the bank account block of DOCX and PDF output SHALL be that account's values. The account SHALL be fixed by the client; the invoice creation screen SHALL NOT offer a per-invoice account override.

#### Scenario: USD client invoice

- **WHEN** a DOCX or PDF invoice is generated for a client assigned the USD account
- **THEN** the bank account block shows the USD account's IBAN, bank and SWIFT

#### Scenario: Two clients, two accounts

- **WHEN** invoices are generated for a client assigned the PLN account and for a client assigned the USD account
- **THEN** each invoice shows the details of its own client's account

### Requirement: Show The Billing Account In The Invoice Preview

The Create Invoice preview SHALL display the selected client's billing account, showing its label and IBAN, and SHALL update when the selected client changes. The value SHALL be read-only on that screen.

#### Scenario: Preview reflects client

- **WHEN** a user selects client `GREENFLOW`, which is assigned the USD account, on the Create Invoice screen
- **THEN** the preview shows the USD account's label and IBAN

#### Scenario: Switching clients

- **WHEN** the user then selects a client assigned the PLN account
- **THEN** the preview shows the PLN account's label and IBAN

### Requirement: Unresolved Billing Account Blocks Generation

If a client's `billing_account` is empty or does not match any defined account, invoice generation SHALL fail for every output format before any file is written. The error SHALL name the client and the missing key. The system SHALL NOT fall back to another account. The invoice preview SHALL indicate that no valid account is assigned.

#### Scenario: Unknown key

- **WHEN** a user generates an invoice for a client whose `billing_account` is `OLD` and no account `OLD` exists
- **THEN** generation fails with an error naming the client and `OLD`, and no DOCX, PDF or XML file is written

#### Scenario: Preview with no account

- **WHEN** the selected client has no valid billing account
- **THEN** the preview shows that no billing account is assigned

### Requirement: Migrate Legacy Supplier Bank Details

When a configuration that has no billing accounts is loaded, and its supplier section contains a non-empty `iban`, `bank` or `swift`, the system SHALL create one billing account. That account SHALL have key `DEFAULT`, label `Default`, the legacy IBAN, bank and SWIFT, and no currency. Every client without a `billing_account` SHALL be assigned that account. The migrated shape SHALL be written on the next save. A configuration that already defines billing accounts SHALL NOT be migrated.

#### Scenario: Legacy config

- **WHEN** a configuration with `[supplier] iban = "PL42..."` and no `[[billing_accounts]]` is loaded
- **THEN** one account `DEFAULT` with IBAN `PL42...` exists and every client references `DEFAULT`

#### Scenario: Migrated shape persisted

- **WHEN** that migrated configuration is saved
- **THEN** the file contains a `[[billing_accounts]]` entry, each client has `billing_account = "DEFAULT"`, and `[supplier]` has no bank keys

#### Scenario: Already migrated config

- **WHEN** a configuration that defines billing accounts is loaded
- **THEN** no `DEFAULT` account is added and client assignments are unchanged

### Requirement: Default Configuration Includes A Billing Account

The configuration created on first run SHALL contain one placeholder billing account. The sample client SHALL be assigned that account and SHALL have country `PL`, so that a first-run user can generate every output format without editing the configuration.

#### Scenario: First run

- **WHEN** the application starts with no configuration file
- **THEN** the created configuration has one billing account, and the sample client references it and has country `PL`
