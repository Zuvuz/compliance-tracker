Feature: Suggested security events
  US-128: the tracker suggests security events from what it already knows: scan findings whose CVE is on a public list of
  exploited vulnerabilities (CISA KEV, the EU Vulnerability Database), and new critical findings. It only suggests; a
  person records or dismisses (AI Act Art. 14 style oversight). CRA Art. 14(2)(a): the early warning is due 24 hours
  after becoming aware. Times are UTC; the pages show local time.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the time is "2026-11-01T08:12:00Z"
    And the exploited-vulnerability list "KEV" is switched on

  @US-128 @cra-Art.14 @iso27001-A.5.7 @integration
  Scenario: An exploited CVE in a finding is suggested as an event
    Given "Acme Portal" has an open high finding "CVE-2026-1234" from "Trivy"
    When "CVE-2026-1234" is added to the list "KEV"
    And I open the "Overview" tab of "Acme Portal"
    Then "Needs attention" says "CVE-2026-1234 is actively exploited and found in Acme Portal"
    And the Security events tab suggests "CVE-2026-1234" from "CISA KEV"

  @US-128 @cra-Art.14 @integration
  Scenario: Recording the suggested event starts the deadlines
    Given "Acme Portal" has an open high finding "CVE-2026-1234" from "Trivy"
    And "CVE-2026-1234" is added to the list "KEV"
    When I record the suggested event "CVE-2026-1234"
    Then the "Early warning" for "CVE-2026-1234" is due "2026-11-02T08:12:00Z"
    And nothing is suggested for "Acme Portal"

  @US-128 @cra-Art.14 @integration
  Scenario: A CVE with an event is not suggested again
    Given "Acme Portal" has an open high finding "CVE-2026-1234" from "Trivy"
    And I have recorded the actively exploited vulnerability "CVE-2026-1234" for "Acme Portal", aware since "2026-11-01T07:00:00Z"
    When "CVE-2026-1234" is added to the list "KEV"
    Then nothing is suggested for "Acme Portal"

  @US-128 @cra-annexI @integration
  Scenario: A new critical finding is suggested as a vulnerability that is not exploited
    Given "Bo" is in "Acme ApS" with the role "ComplianceManager"
    When "Acme Portal" gets a critical finding "CVE-2026-5678" from "Trivy"
    Then the Security events tab suggests "CVE-2026-5678" from "Trivy"
    When I record the suggested event "CVE-2026-5678"
    Then I see that it waits for another member's approval
    When "Bo" approves the request
    Then the event "CVE-2026-5678" needs no reports

  @US-128 @cra-Art.14 @integration
  Scenario: A vulnerability that is not exploited gets an upgrade suggestion
    Given "Acme Portal" has an open high finding "CVE-2026-1234" from "Trivy"
    And I have recorded the vulnerability "CVE-2026-1234 in the parser", not exploited, for "Acme Portal", aware since "2026-10-30T08:00:00Z"
    When "CVE-2026-1234" is added to the list "KEV"
    Then the Security events tab suggests 'CVE-2026-1234 is now exploited – upgrade "CVE-2026-1234 in the parser"?'
    When I record the suggested event "CVE-2026-1234"
    Then the "Early warning" for "CVE-2026-1234 in the parser" is due "2026-11-02T08:12:00Z"

  @US-128 @integration
  Scenario: A dismissed suggestion keeps its reason and does not come back
    Given "Acme Portal" has an open high finding "CVE-2026-1234" from "Trivy"
    And "CVE-2026-1234" is added to the list "KEV"
    When I dismiss the suggestion "CVE-2026-1234" because "The vulnerable function is not used"
    And the lists are fetched again
    Then nothing is suggested for "Acme Portal"
    And the dismissal of "CVE-2026-1234" is logged with "The vulnerable function is not used"

  @US-128 @integration
  Scenario: A list that cannot be fetched says when it was last fetched
    Given "Acme Portal" has an open high finding "CVE-2026-1234" from "Trivy"
    And "CVE-2026-1234" is added to the list "KEV"
    And the time is "2026-11-02T08:12:00Z"
    When the lists cannot be fetched
    Then the list "CISA KEV" says it was fetched "2026-11-01T08:12:00Z" and failed "2026-11-02T08:12:00Z"
    And the Security events tab suggests "CVE-2026-1234" from "CISA KEV"

  @US-128 @integration
  Scenario: Nothing is fetched while no list is switched on
    Given the exploited-vulnerability list "KEV" is switched off
    When the lists are fetched again
    Then no list was fetched
