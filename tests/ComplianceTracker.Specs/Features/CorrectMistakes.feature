Feature: Correct mistakes

  # US-176: what was made by mistake can be taken back by a member with the same right: withdrawn with a reason, removed,
  # reopened or undone. It stays in the history and the activity log; withdrawn risks are left out of the register and
  # the reports. A security event that was reported cannot be recorded in error.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-176 @iso27001-A.5.33 @integration
  Scenario: A risk created by mistake is withdrawn with a reason
    Given the risk "Typo risk" exists in "Acme Portal"
    When I withdraw the risk "Typo risk" because "Created by mistake"
    Then the open register of "Acme Portal" does not list "Typo risk"
    And the activity log has "RiskWithdrawn" saying "Typo risk: Created by mistake"

  @US-176 @integration
  Scenario: A risk accepted by mistake is reopened
    Given the risk "Stolen sessions" exists in "Acme Portal"
    And I accept the risk "Stolen sessions" with the reason "Accepted on the wrong risk"
    When I reopen the risk "Stolen sessions" because "Wrong risk accepted"
    Then the risk "Stolen sessions" is open again
    And the activity log has "RiskReopened" saying "Stolen sessions"

  @US-176 @integration
  Scenario: An action added by mistake is removed
    Given the risk "Stolen sessions" exists in "Acme Portal"
    And I have added the action "Wrong action" to the risk "Stolen sessions"
    When I remove the action "Wrong action" from the risk "Stolen sessions"
    Then the risk "Stolen sessions" has no action "Wrong action"

  @US-176 @integration
  Scenario: A finding accepted by mistake is open again
    Given "Acme Portal" has a "trivy" scan with the findings "CVE-2026-1111" and "CVE-2026-2222"
    When I accept the finding "CVE-2026-1111" because "Not reachable"
    And I undo the acceptance of the finding "CVE-2026-1111"
    Then the finding "CVE-2026-1111" is open
    And the activity log has "FindingAcceptanceUndone" saying "CVE-2026-1111"

  @US-176 @cra-Art14 @integration
  Scenario: A security event recorded by mistake is closed as recorded in error
    Given I have recorded the severe incident "Test entry" for "Acme Portal", aware since "2026-11-13T08:00:00Z"
    When I close "Test entry" as recorded in error because "It was a test"
    Then the activity log has "SecurityEventRecordedInError" saying "Recorded in error: It was a test"

  @US-176 @integration
  Scenario: A reported security event cannot be recorded in error
    Given I have recorded the severe incident "Breach" for "Acme Portal", aware since "2026-11-13T08:00:00Z"
    When I mark the "Early warning" for "Breach" as sent with the reference "CSIRT-1"
    Then "Breach" cannot be closed as recorded in error
