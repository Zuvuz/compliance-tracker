Feature: Risk assessment report

  # US-054: the project's risk assessment as one document on the Reports tab (Excel, CSV or PDF), with the criteria,
  # the risks by level with their measures and actions, the accepted risks with who accepted them and their role, and
  # the closed ones. It can be locked like the other reports (US-050).

  Background:
    Given an empty database
    And the time is "2026-01-05T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-054 @cra-Art.13 @iso27001-6.1.2 @integration
  Scenario: The risks are listed by level, highest first
    Given I add the risk "Phishing" to "Acme Portal" with likelihood 2 and impact 2
    And I add the risk "Stolen session cookie" to "Acme Portal" with likelihood 3 and impact 4
    And I add the risk "Ransomware" to "Acme Portal" with likelihood 4 and impact 5
    When I download the risk assessment of "Acme Portal" as CSV
    Then the risk assessment lists "Ransomware", "Stolen session cookie" and "Phishing" in that order
    And the risk assessment shows "Stolen session cookie" with the level "High (12)"

  @US-054 @iso27001-6.1.2 @integration
  Scenario: A risk shows its measures
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the risk "Stolen session cookie" exists in "Acme Portal"
    And the risk "Stolen session cookie" has the measure "[US-004] Sign in"
    When I download the risk assessment of "Acme Portal" as CSV
    Then the risk assessment shows "Stolen session cookie" with the measures "[US-004] Sign in"

  @US-054 @iso27001-8.2 @integration
  Scenario: A risk whose review is due is marked
    Given I add the risk "Phishing" to "Acme Portal" with likelihood 2 and impact 2
    And the time is "2027-02-01T09:00:00Z"
    When I download the risk assessment of "Acme Portal" as CSV
    Then the risk assessment shows "Phishing" with the next review "Review due"

  @US-054 @iso27001-6.1.3 @integration
  Scenario: An accepted risk shows who accepted it and their role
    Given I add the risk "Phishing" to "Acme Portal" with likelihood 2 and impact 2
    And I accept the risk "Phishing" with the reason "Training given every year"
    When I download the risk assessment of "Acme Portal" as PDF from the Reports tab
    Then the PDF contains "Accepted risks"
    And the PDF contains "Organisation admin"
    And the PDF contains "Training given every year"

  @US-054 @integration
  Scenario: Closed risks are kept apart
    Given I add the risk "Phishing" to "Acme Portal" with likelihood 2 and impact 2
    And I add the risk "Old TLS" to "Acme Portal" with likelihood 3 and impact 3
    And I close the risk "Old TLS" with the reason "API retired"
    When I download the risk assessment of "Acme Portal" as CSV
    Then the risk assessment lists only "Phishing"

  @US-054 @cra-Art.13 @integration
  Scenario: The criteria come first in the PDF
    When I download the risk assessment of "Acme Portal" as PDF from the Reports tab
    Then the PDF contains "Criteria"
    And the PDF contains "Critical (25)"

  @US-054 @iso27001-A.5.33 @integration
  Scenario: The risk assessment can be locked
    Given I add the risk "Phishing" to "Acme Portal" with likelihood 2 and impact 2
    When I lock the risk assessment of "Acme Portal"
    Then the locked reports of "Acme Portal" list "Risk assessment – Acme Portal" locked by "Anna (Organisation admin)" with a fingerprint
