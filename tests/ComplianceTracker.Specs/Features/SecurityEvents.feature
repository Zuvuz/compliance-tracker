Feature: Follow-up while the product is on the market

  # US-073: actively exploited vulnerabilities and severe incidents get the reporting deadlines of CRA Art. 14, counted
  # from when the manufacturer became aware. The tracker does not send the reports; a person marks each as sent with a
  # reference. The Overview shows deadlines that are close or missed, and the end of the support period.
  # Times in the scenarios are UTC; the pages show local time.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the time is "2026-11-01T09:00:00Z"
    And "Acme Portal" is released with support until "2031-11-01"

  @US-073 @cra-Art.14 @integration
  Scenario: An actively exploited vulnerability gets its deadlines
    Given the time is "2026-11-04T10:00:00Z"
    When I record the actively exploited vulnerability "CVE-2026-1234" for "Acme Portal", aware since "2026-11-04T10:00:00Z"
    Then the "Early warning" for "CVE-2026-1234" is due "2026-11-05T10:00:00Z"
    And the "Notification" for "CVE-2026-1234" is due "2026-11-07T10:00:00Z"
    And the "Final report" for "CVE-2026-1234" is not due yet

  @US-073 @cra-Art.14 @integration
  Scenario: The final report on a vulnerability is due 14 days after the fix
    Given the time is "2026-11-04T10:00:00Z"
    And I have recorded the actively exploited vulnerability "CVE-2026-1234" for "Acme Portal", aware since "2026-11-04T10:00:00Z"
    And the time is "2026-11-06T12:00:00Z"
    When I mark the fix for "CVE-2026-1234" as available
    Then the "Final report" for "CVE-2026-1234" is due "2026-11-20T12:00:00Z"

  @US-073 @cra-Art.14 @integration
  Scenario: A severe incident's final report is due a month after the notification
    Given the time is "2026-11-04T10:00:00Z"
    And I have recorded the severe incident "Ransomware on the update server" for "Acme Portal", aware since "2026-11-04T08:00:00Z"
    When I mark the "Notification" for "Ransomware on the update server" as sent with the reference "ENISA 4711"
    Then the "Final report" for "Ransomware on the update server" is due "2026-12-04T10:00:00Z"

  @US-073 @cra-Art.14 @integration
  Scenario: A deadline due soon is shown on the overview
    Given the time is "2026-11-04T10:00:00Z"
    And I have recorded the actively exploited vulnerability "CVE-2026-1234" for "Acme Portal", aware since "2026-11-04T10:00:00Z"
    And the time is "2026-11-05T07:00:00Z"
    When I open the "Overview" tab of "Acme Portal"
    Then "Needs attention" says "Early warning for CVE-2026-1234 due in 3 hours"

  @US-073 @cra-Art.14 @integration
  Scenario: A missed deadline is shown on the overview in red
    Given the time is "2026-11-04T10:00:00Z"
    And I have recorded the actively exploited vulnerability "CVE-2026-1234" for "Acme Portal", aware since "2026-11-04T10:00:00Z"
    And the time is "2026-11-05T11:00:00Z"
    When I open the "Overview" tab of "Acme Portal"
    Then "Needs attention" says "Early warning for CVE-2026-1234 missed" in red

  @US-073 @cra-Art.14 @integration
  Scenario: A report is marked as sent with its reference
    Given the time is "2026-11-04T10:00:00Z"
    And I have recorded the actively exploited vulnerability "CVE-2026-1234" for "Acme Portal", aware since "2026-11-04T10:00:00Z"
    When I mark the "Early warning" for "CVE-2026-1234" as sent with the reference "ENISA 4711"
    Then the "Early warning" for "CVE-2026-1234" is sent with the reference "ENISA 4711"
    And the activity log for "Acme ApS" has "SecurityReportSent" for "Anna" concerning "Acme Portal"

  @US-073 @iso27001-A.5.26 @integration
  Scenario: An event is closed only when every report is sent
    Given the time is "2026-11-04T10:00:00Z"
    And I have recorded the actively exploited vulnerability "CVE-2026-1234" for "Acme Portal", aware since "2026-11-04T10:00:00Z"
    When I close the security event "CVE-2026-1234"
    Then I see the error "Send every report before closing the event"

  @US-073 @cra-Art.13 @integration
  Scenario: End of support waits for open security events
    Given I have recorded the actively exploited vulnerability "CVE-2026-1234" for "Acme Portal", aware since "2026-11-01T08:00:00Z"
    When I change the phase of "Acme Portal" to "EndOfSupport"
    Then I see the error "Close every security event before end of support"

  @US-073 @cra-Art.13 @integration
  Scenario: The end of the support period is shown 90 days before
    Given the time is "2031-09-05T09:00:00Z"
    When I open the "Overview" tab of "Acme Portal"
    Then "Needs attention" says "Support ends 01-11-2031 (in 57 days)"

  @US-073 @iso27001-A.5.15 @integration
  Scenario: An auditor sees the events but cannot record any
    Given "Bo" has joined "Acme ApS" as "Auditor"
    When "Bo" opens the "Security events" tab of "Acme Portal"
    Then there is no "Record" button
