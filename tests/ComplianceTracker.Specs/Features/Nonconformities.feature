Feature: Nonconformities and corrective actions

  # US-212: one register for all nonconformities, from audits, security events, missed deadlines and complaints, each with
  # the correction, the cause, a corrective action and a later check that the action worked (ISO/IEC 27001 10.2). The page
  # is "Nonconformities" in the ISMS area.

  Background:
    Given an empty database
    And the time is "2026-11-24T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-212 @iso27001-10.2 @integration
  Scenario: A nonconformity is closed when its action worked
    Given the nonconformity "Backups not tested" from an internal audit
    When I record the cause "nobody owned the task"
    And I plan the corrective action "Quarterly restore test" for "Anna" before "2027-01-01"
    And the corrective action is done
    And I record the effectiveness check "two restore tests done" as effective
    Then the nonconformity "Backups not tested" is closed
    And the activity log has "NonconformityChanged" saying "Backups not tested: effective, closed"

  @US-212 @iso27001-10.2 @integration
  Scenario: A nonconformity cannot be closed without a cause
    Given the nonconformity "Backups not tested" from an internal audit
    When I try to close it
    Then I am told "Record the cause, the corrective action and the effectiveness check first"

  @US-212 @iso27001-10.2 @nis2-art21-2f @integration
  Scenario: An action that did not work opens the nonconformity again
    Given the nonconformity "Backups not tested" from an internal audit
    And I record the cause "nobody owned the task"
    And I plan the corrective action "Quarterly restore test" for "Anna" before "2027-01-01"
    And the corrective action is done
    When I record the effectiveness check "still no restore test" as not effective
    Then the nonconformity "Backups not tested" is "Open"
    And the nonconformity "Backups not tested" lists the action "Quarterly restore test" as not working

  @US-212 @iso27001-10.2 @integration
  Scenario: A finding from an audit becomes a nonconformity, once
    Given the project "Webshop" exists
    And an audit of "Webshop" found "No access review" for "Anna" due "2026-12-31"
    When I raise a nonconformity from "No access review"
    Then the nonconformity "No access review" is "Open" from "ExternalAudit"
    And "No access review" can no longer be chosen as the source of a nonconformity

  @US-212 @iso27001-a5.27 @integration
  Scenario: A security event becomes a nonconformity
    Given the project "Webshop" exists
    And I have recorded the incident "Phishing mail to support", not severe, for "Webshop", aware since "2026-11-20T07:00:00Z"
    When I raise a nonconformity from "Phishing mail to support"
    Then the nonconformity "Phishing mail to support" is "Open" from "SecurityEvent"

  @US-212 @iso27001-10.2 @integration
  Scenario: A corrective action cannot have a deadline in the past
    Given the nonconformity "Backups not tested" from an internal audit
    When I plan the corrective action "Quarterly restore test" for "Anna" before "2026-11-01"
    Then I am told "The deadline has passed; choose a later date"

  @US-212 @iso27001-10.2 @integration
  Scenario: A member who does not decide compliance reads the register
    Given the nonconformity "Backups not tested" from an internal audit
    And "Dan" has joined "Acme ApS" as "Developer"
    And I am signed in as "Dan"
    When I open the nonconformities
    Then the nonconformity "Backups not tested" is "Open"
    And the nonconformities are read-only
