Feature: Security events that are not reportable (yet)
  US-127: a vulnerability that nobody exploits, or an incident that is not severe, is recorded without reports or
  deadlines and followed to its fix (CRA Annex I, Part II). If it becomes actively exploited or severe, it is upgraded,
  and the reports of CRA Art. 14 and their deadlines start from that moment.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the time is "2026-11-01T09:00:00Z"

  @US-127 @cra-annexI @integration
  Scenario: A vulnerability that is not exploited has no deadlines
    When I record the vulnerability "XSS in search", not exploited, for "Acme Portal", aware since "2026-10-31T08:00:00Z"
    Then the event "XSS in search" needs no reports

  @US-127 @cra-Art.14 @integration
  Scenario: Upgrading starts the deadlines from the upgrade time
    Given I have recorded the vulnerability "XSS in search", not exploited, for "Acme Portal", aware since "2026-10-31T08:00:00Z"
    When I upgrade "XSS in search" to reportable at "2026-11-01T08:00:00Z" because "Listed as actively exploited"
    Then the "Early warning" for "XSS in search" is due "2026-11-02T08:00:00Z"
    And the event "XSS in search" says it is reportable since "2026-11-01T08:00:00Z"

  @US-127 @integration
  Scenario: An event cannot become reportable before you knew of it
    Given I have recorded the vulnerability "XSS in search", not exploited, for "Acme Portal", aware since "2026-10-31T08:00:00Z"
    When I upgrade "XSS in search" to reportable at "2026-10-30T08:00:00Z" because "Listed as actively exploited"
    Then I see the error "It became reportable between the time you became aware of the event and now"

  @US-127 @cra-annexI @integration
  Scenario: A vulnerability that is not exploited is closed when fixed and disclosed
    Given I have recorded the vulnerability "XSS in search", not exploited, for "Acme Portal", aware since "2026-10-31T08:00:00Z"
    When I close the security event "XSS in search" without a reason
    Then I see the error "Mark the fix as available and the vulnerability as disclosed, or give a reason to close it"
    When I mark the fix for "XSS in search" as available
    And I mark "XSS in search" as disclosed
    And I close the security event "XSS in search" without a reason
    Then the event "XSS in search" is closed

  @US-127 @integration
  Scenario: A vulnerability can be closed with a reason
    Given I have recorded the vulnerability "Old parser", not exploited, for "Acme Portal", aware since "2026-10-31T08:00:00Z"
    When I close the security event "Old parser" because "The component was removed"
    Then the event "Old parser" is closed because "The component was removed"

  @US-127 @iso27001-A.5.26 @integration
  Scenario: An incident that is not severe can be closed at once
    Given I have recorded the incident "Phishing mail to support", not severe, for "Acme Portal", aware since "2026-11-01T07:00:00Z"
    When I close the security event "Phishing mail to support" without a reason
    Then the event "Phishing mail to support" is closed
