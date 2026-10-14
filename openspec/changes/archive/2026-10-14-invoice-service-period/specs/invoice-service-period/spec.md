# Spec Delta

## Purpose

Represent the inclusive dates when invoiced services were provided, with automatic monthly defaults and manual exceptions that remain consistent across the invoice preview and generated documents.

## ADDED Requirements

### Requirement: Derive Automatic Service Periods From The Client Month Rule

The system SHALL derive the automatic service period from the invoice date and the existing client month rule. For `early_previous`, days 1 through 20 SHALL select the previous month and later days SHALL select the current month. For `early_current`, days 1 through 20 SHALL select the current month and later days SHALL select the next month. The existing fallback for an unrecognized rule SHALL select the current month.

The period SHALL start on the selected month's first day and end on its actual last day, inclusive, regardless of the invoice date's day or time. Month lengths, leap years and transitions between years SHALL be handled. The invoice issue date SHALL remain independent of the service period.

#### Scenario: September services invoiced in October

- **WHEN** the invoice date is 5 October 2026 and the rule is `early_previous`
- **THEN** the automatic period is 1 through 30 September 2026 and the issue date remains 5 October 2026

#### Scenario: December services invoiced in January

- **WHEN** the invoice date is 5 January 2027 and the rule is `early_previous`
- **THEN** the automatic period is 1 through 31 December 2026 and numbering and output locations continue to follow the January 2027 invoice date

#### Scenario: Invoicing before the selected month

- **WHEN** the invoice date is 25 December 2026 and the rule is `early_current`
- **THEN** the automatic period is 1 through 31 January 2027

#### Scenario: Month rule cutoff

- **WHEN** the date changes from 20 to 21 October 2026 in Automatic mode
- **THEN** `early_previous` changes the period from September to October and `early_current` changes it from October to November

#### Scenario: February in a leap year

- **WHEN** the selected service month is February 2028
- **THEN** the automatic period is 1 through 29 February 2028

### Requirement: Allow Custom Service Dates Before Generation

Invoice creation SHALL offer Automatic and Custom service-period modes, starting in Automatic mode. It SHALL display From and To dates in `dd.MM.yyyy` form. Automatic mode SHALL prevent manual changes to these dates; Custom mode SHALL allow keyboard date editing and calendar selection for each endpoint.

Entering Custom mode SHALL initially retain the displayed automatic dates. Custom ranges SHALL support partial months, multiple months, year transitions and a single day. A calendar selection SHALL affect only its corresponding endpoint when accepted; cancelling SHALL leave it unchanged. The override SHALL apply to the invoice being prepared rather than modifying the client's month rule or saving custom dates as configuration defaults.

#### Scenario: Editing a partial service period

- **WHEN** the user selects Custom for a September invoice and chooses From `15.09.2026` and To `30.09.2026`
- **THEN** the selected inclusive period is 15 through 30 September 2026

#### Scenario: Period across two months

- **WHEN** the user enters From `15.09.2026` and To `14.10.2026` in Custom mode
- **THEN** the selected period retains those exact dates without truncating it to a calendar month

#### Scenario: Cancelling the calendar

- **WHEN** the user opens the From calendar and cancels after navigating to another day
- **THEN** the From date and selected period stay unchanged

### Requirement: Keep Period Selection Predictable When Inputs Change

In Automatic mode, changing the invoice date SHALL recalculate both period dates. In Custom mode, changing the invoice date SHALL preserve both chosen service dates. Returning to Automatic mode SHALL discard the override and recalculate the period using the current invoice date and client rule.

Selecting another client or beginning the next invoice after successful generation SHALL reset the period to Automatic using the applicable client and invoice date. Failed validation SHALL preserve the selected mode and user-entered dates for correction.

#### Scenario: Automatic period follows the invoice date

- **WHEN** an Automatic invoice for an `early_previous` client changes from 5 October to 5 November 2026
- **THEN** its service period changes from September to October 2026

#### Scenario: Custom period survives an invoice-date change

- **WHEN** the user changes the invoice date after selecting a Custom period of 15 through 30 September 2026
- **THEN** the service period stays 15 through 30 September 2026

#### Scenario: Restoring automatic calculation

- **WHEN** the user returns to Automatic with invoice date 5 October 2026 and rule `early_previous`
- **THEN** the period becomes 1 through 30 September 2026 and the date controls prevent editing

#### Scenario: Switching clients clears the override

- **WHEN** the user switches clients while preparing a Custom period
- **THEN** the selected client starts in Automatic mode with dates derived from its own rule

#### Scenario: Preparing another invoice

- **WHEN** generation of a Custom-period invoice succeeds
- **THEN** its files retain the selected dates and the creation form starts the next invoice in Automatic mode

### Requirement: Validate Service Periods Before Any Output Is Written

A selected service period SHALL contain two valid calendar dates with start no later than end. A single-day period SHALL be accepted. Invalid, missing or reversed user-entered dates SHALL be reported as service-period errors and SHALL NOT be replaced by today's date or automatic defaults. An invalid invoice issue date SHALL also be rejected rather than silently replaced when deriving a period.

These checks SHALL apply to every selected output format and direct generation entry point before any file is created or replaced. Validation failure SHALL leave existing files, invoice numbering and saved output preferences unchanged. The preview SHALL identify invalid period input rather than presenting a valid-looking substitute, and no period-dependent rate lookup SHALL start from invalid dates.

#### Scenario: End before start with combined outputs

- **WHEN** the selected period is 30 through 15 September 2026 and PDF, DOCX and XML are requested
- **THEN** generation reports the reversed period, creates or replaces no selected file and changes no numbering or saved output preference

#### Scenario: Invalid input with documents only

- **WHEN** one service date is missing or invalid and only PDF and DOCX are requested
- **THEN** the preview reports the input problem and generation refuses both files without substituting another date

#### Scenario: Invalid invoice date in Automatic mode

- **WHEN** the invoice issue date is unparseable in Automatic mode
- **THEN** the form reports the date error, does not derive a period from today's date and refuses generation

#### Scenario: One day of service

- **WHEN** both service dates are 15 September 2026 and other selected-format validation passes
- **THEN** the invoice is generated with a single-day service period

### Requirement: Present The Same Period Across Outputs

The preview, XML, DOCX and PDF SHALL consume the same resolved service period for one generation request. The preview SHALL display both exact service dates separately from the invoice issue date.

DOCX and PDF SHALL retain the existing bilingual service-period label and show the existing month/year text when the selected dates cover exactly one full calendar month, including a full month selected manually. Other periods SHALL be rendered as `dd.MM.yyyy - dd.MM.yyyy`. Each generator SHALL use supplied invoice period data without recalculating from mutable client configuration.

#### Scenario: Complete month keeps document presentation

- **WHEN** the selected period is 1 through 30 September 2026
- **THEN** PDF and DOCX show the existing September 2026 month/year presentation, while the preview and XML carry the matching exact dates

#### Scenario: Custom range is explicit everywhere

- **WHEN** an invoice is generated for 15 September through 14 October 2026
- **THEN** PDF and DOCX show `15.09.2026 - 14.10.2026`, the preview shows those endpoints and XML contains the same start and end

### Requirement: Preserve Automatic Behavior For Existing Invoice Inputs

Existing configuration and invoice-creation requests that omit custom service dates SHALL continue to derive a full calendar month through the existing month rule. Existing invoice inputs that provide a service month without explicit endpoints SHALL resolve to that month's first and last day. Explicit endpoints SHALL take precedence; supplying only one endpoint SHALL fail validation.

#### Scenario: Existing caller supplies no override

- **WHEN** an existing invoice-creation request omits custom dates
- **THEN** it generates the same automatic service month as before, now represented by its inclusive boundaries

#### Scenario: Existing invoice specifies a service month

- **WHEN** invoice data identifies September 2026 as its service month and provides no endpoints
- **THEN** the resolved period is 1 through 30 September 2026

#### Scenario: Incomplete explicit period

- **WHEN** invoice data supplies a service start without an end, or an end without a start
- **THEN** generation rejects the incomplete period rather than mixing an explicit date with an automatic one
