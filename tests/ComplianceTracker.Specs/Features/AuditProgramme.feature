Feature: An internal audit programme

  # US-211: the organisation plans its internal audits over a cycle of 1–5 years so that clauses 4–10 and the applicable
  # Annex A controls are all audited (ISO/IEC 27001 9.2). An auditor should not audit a control they own; the owners are set
  # on the page. A recorded audit (US-134) marks a planned one done, and its findings go to the nonconformities (US-212).
  # The page is "Internal audits" in the ISMS area.

  Background:
    Given an empty database
    And the time is "2026-11-25T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-211 @iso27001-9.2 @integration
  Scenario: The coverage shows what is not planned
    Given an audit programme of 3 years from "2027-01-01"
    When I plan an audit on "2027-03-01" for the clauses "9" and "10"
    Then the coverage shows the clause "9" as planned
    And the coverage shows the clause "4" as missing
    And the audit coverage says "Missing: 4, 5, 6, 7 and 8"

  @US-211 @iso27001-9.2 @integration
  Scenario: An auditor who owns a control gets a warning
    Given "Bo" has joined "Acme ApS" as "ComplianceManager"
    And an audit programme of 3 years from "2027-01-01"
    And "Bo" owns the control "A.8.13"
    When I plan an audit of "A.8.13" with "Bo" as auditor
    Then the audit programme warns "Bo owns this control"
    And nothing is planned

  @US-211 @iso27001-9.2 @integration
  Scenario: A reason keeps the plan with an auditor who owns the control
    Given "Bo" has joined "Acme ApS" as "ComplianceManager"
    And an audit programme of 3 years from "2027-01-01"
    And "Bo" owns the control "A.8.13"
    When I plan an audit of "A.8.13" with "Bo" as auditor because "the only one who knows the backup system"
    Then the coverage shows the control "A.8.13" as planned

  @US-211 @iso27001-9.2 @iso27001-10.2 @integration
  Scenario: A recorded audit marks the planned one done and its findings become nonconformities
    Given the project "Webshop" exists
    And an audit programme of 1 years from "2026-01-01"
    And I plan an audit on "2026-11-01" for the clauses "9" and "10"
    And an audit of "Webshop" found "No access review" for "Anna" due "2026-12-31"
    When I mark the audit planned on "2026-11-01" as done with the recorded audit
    Then the coverage shows the clause "9" as done
    And the nonconformity "No access review" is "Open" from "InternalAudit"

  @US-211 @iso27001-9.2 @integration
  Scenario: A planned audit past its date is overdue
    Given an audit programme of 1 years from "2026-01-01"
    And I plan an audit on "2026-11-01" for the clauses "9" and "10"
    When I open the internal audits
    Then the audit planned on "2026-11-01" is overdue

  @US-211 @iso27001-9.2 @integration
  Scenario: A programme lasts at most five years
    When I start an audit programme of 6 years from "2027-01-01"
    Then the audit programme warns "A programme lasts 1 to 5 years"

  @US-211 @iso27001-9.2 @integration
  Scenario: A member who does not decide compliance reads the programme
    Given an audit programme of 3 years from "2027-01-01"
    And "Dan" has joined "Acme ApS" as "Developer"
    And I am signed in as "Dan"
    When I open the internal audits
    Then the internal audits are read-only
