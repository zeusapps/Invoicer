# Spec Delta

## MODIFIED Requirements

### Requirement: Derive The Default Rate Date

The system SHALL derive a default **rate date** from the invoice issue date and the resolved service period, as the earlier of:

- the invoice date; and
- the inclusive end of the selected service period.

In Automatic mode the service period SHALL be the full month derived from the existing client month rule, preserving the existing monthly default. In Custom mode the default SHALL use the actual selected end date rather than that month's last day. Dates SHALL be compared without time-of-day components.

The derived date SHALL be a default only. The system SHALL NOT treat it as authoritative, because the day a tax obligation arises is not always derivable from the data the system holds. Manual rate-date overrides SHALL remain available.

#### Scenario: Invoice issued after the service month ends

- **WHEN** an invoice dated 5 October 2026 has an Automatic service period of 1 through 30 September 2026
- **THEN** the default rate date is 30 September 2026

#### Scenario: Invoice issued during the service month

- **WHEN** an invoice dated 25 October 2026 has an Automatic service period of 1 through 31 October 2026
- **THEN** the default rate date is 25 October 2026

#### Scenario: Invoice dated on the last day of its service month

- **WHEN** an invoice dated 31 October 2026 has an Automatic service period of 1 through 31 October 2026
- **THEN** the default rate date is 31 October 2026

#### Scenario: Custom period ends partway through a month

- **WHEN** an invoice dated 5 October 2026 has a Custom service period of 1 through 15 September 2026
- **THEN** the default rate date is 15 September 2026 rather than 30 September 2026

#### Scenario: Invoice precedes the service period

- **WHEN** an invoice dated 25 September 2026 has a selected service period of 1 through 31 October 2026
- **THEN** the default rate date is 25 September 2026

### Requirement: Allow The Rate Date And Rate To Be Overridden

The system SHALL present the rate date and the rate as editable values before an invoice is generated, pre-filled with the derived date and the resolved rate.

- Editing the rate date SHALL re-resolve the rate from the NBP table for the new date. A date different from the current derived default SHALL be manually overridden; entering the current derived default SHALL restore automatic rate-date updates.
- Editing the rate SHALL replace the resolved value and SHALL use it exactly as entered, without rounding or correction.
- A manually entered rate SHALL be recorded as such, and SHALL NOT be attributed to an NBP table it did not come from.
- Changing the invoice date or service period SHALL update an automatic rate date, but SHALL preserve a manually overridden rate date.
- A change to the effective lookup currency or rate date SHALL invalidate the previous rate and its source metadata, including manually entered rates, before resolving a replacement. A dependency change that leaves the effective lookup currency and rate date unchanged SHALL preserve the rate and source metadata.
- Selecting another client SHALL reset the rate-date override and continue to discard and resolve rates according to the existing client-change requirement.
- Beginning the next invoice after successful generation SHALL reset rate-date ownership to automatic for that invoice's current dates.

Pre-filling is what protects against a rate typed from memory or rounded by hand, so the system SHALL pre-fill whenever it can rather than presenting an empty field. A lookup result superseded by a date or client change SHALL NOT replace the current rate.

#### Scenario: User corrects the rate date

- **WHEN** the user changes the rate date to a different date
- **THEN** the rate, table date and table identifier are re-resolved for that date and the displayed values update

#### Scenario: User enters a rate by hand

- **WHEN** the user types 3.80 over a pre-filled rate of 3.7998
- **THEN** the invoice carries 3.80 exactly, and the invoice does not claim that 3.80 came from an NBP table

#### Scenario: Automatic rate date follows the period end

- **WHEN** an invoice dated 5 October 2026 changes its service-period end from 30 to 15 September 2026 and its rate date has not been overridden
- **THEN** the rate date becomes 15 September 2026 and the previous rate/source are cleared before a new lookup

#### Scenario: Manual rate date survives a period change

- **WHEN** the user manually sets the rate date to 10 September 2026 and then changes the service-period end
- **THEN** the rate date remains 10 September 2026 and the rate is not attributed to another date

#### Scenario: Manual rate date survives an invoice-date change

- **WHEN** the user changes the invoice issue date after manually overriding the rate date
- **THEN** the manual rate date is preserved

#### Scenario: Period start changes without changing the rate date

- **WHEN** the user changes only the service-period start and the effective currency and rate date remain unchanged
- **THEN** the current rate and source metadata are retained

#### Scenario: Restoring the default rate date

- **WHEN** the user replaces a manually overridden rate date with the currently derived default and later changes the service-period end
- **THEN** the rate date follows the newly derived default again

#### Scenario: Superseded lookup completes later

- **WHEN** an older rate lookup completes after a newer period-dependent lookup was started
- **THEN** the older result does not replace the current rate or source metadata
