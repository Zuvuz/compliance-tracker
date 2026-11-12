Feature: Recording an audit

  # US-134: an audit that took place is recorded with its scope, date, auditor, result, the auditor's signed report and
  # any certificate. Findings get an owner and a deadline and are closed with a reason. A certificate shows as a badge,
  # 90 days before it ends under "Needs attention". A recorded audit is never changed, only corrected by a new one.

  Background:
    Given an empty database
    And the time is "2026-11-12T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-134 @iso27001-9.2 @integration
  Scenario: An external audit of the organisation with a certificate
    When I record an external audit of "the organisation" for "ISO/IEC 27001:2022" by "Bureau Veritas" with the certificate "DK-1234" valid to "2027-06-30"
    Then "Acme Portal" shows the certificate "ISO/IEC 27001 certified until 30-06-2027"
    And the activity log has "AuditRecorded" saying "External audit of ISO/IEC 27001:2022 by Bureau Veritas on 12-10-2026: Passed; certificate DK-1234 to 30-06-2027"

  @US-134 @iso27001-10.2 @integration
  Scenario: A finding is followed up
    Given an audit of "Acme Portal" found "Access reviews not documented" for "Anna" due "2026-12-31"
    Then "Needs attention" of "Acme Portal" shows "Audit finding: Access reviews not documented (due 31-12-2026)"

  @US-134 @iso27001-10.2 @integration
  Scenario: A finding is closed with a reason
    Given an audit of "Acme Portal" found "Access reviews not documented" for "Anna" due "2026-12-31"
    When I close the finding "Access reviews not documented" of "Acme Portal" because "Reviews are monthly now"
    Then the finding "Access reviews not documented" of "Acme Portal" says "Closed: Reviews are monthly now"
    And the activity log has "AuditFindingClosed" saying "Access reviews not documented: Reviews are monthly now"

  @US-134 @integration
  Scenario: A finding cannot be closed without a reason
    Given an audit of "Acme Portal" found "Access reviews not documented" for "Anna" due "2026-12-31"
    When I close the finding "Access reviews not documented" of "Acme Portal" because ""
    Then I see the error "Give a reason for closing the finding"

  @US-134 @iso27001-9.2 @integration
  Scenario: A certificate that ends within 90 days needs attention
    When I record an external audit of "Acme Portal" for "ISO/IEC 27001:2022" by "Bureau Veritas" with the certificate "DK-1234" valid to "2026-12-30"
    Then "Needs attention" of "Acme Portal" shows "Certificate DK-1234 expires in 48 days"

  @US-134 @integration
  Scenario: An expired certificate says so
    When I record an external audit of "Acme Portal" for "ISO/IEC 27001:2022" by "Bureau Veritas" with the certificate "DK-1234" valid to "2026-11-01"
    Then "Acme Portal" shows the certificate "ISO/IEC 27001 certificate expired 01-11-2026"

  @US-134 @integration
  Scenario: An audit in the future cannot be recorded
    When I record an audit of "Acme Portal" held on "2026-12-01"
    Then I see the error "An audit is recorded after it took place; the date cannot be in the future"

  @US-134 @integration
  Scenario: The auditor's report is needed
    When I record an audit of "Acme Portal" without the auditor's report
    Then I see the error "Attach the auditor's report"

  @US-134 @iso27001-7.5.3 @integration
  Scenario: A mistake is corrected by a new record
    Given I record an external audit of "Acme Portal" for "ISO/IEC 27001:2022" by "Bureau Veritas" with the certificate "DK-1234" valid to "2027-06-30"
    When I record a correction of the audit of "Acme Portal" with the certificate "DK-1235" valid to "2027-07-31"
    Then "Acme Portal" shows the certificate "ISO/IEC 27001 certified until 31-07-2027"
    And the first audit of "Acme Portal" says it is corrected

  @US-134 @iso27001-A.5.15 @integration
  Scenario: A developer cannot record an audit
    Given "Carl" has joined "Acme ApS" as "Developer"
    When "Carl" records an audit of "Acme Portal" anyway
    Then the project "Acme Portal" has no audits
