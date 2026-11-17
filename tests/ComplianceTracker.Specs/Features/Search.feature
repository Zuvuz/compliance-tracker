Feature: Search

  # US-100: a search field in the top bar finds requirements, work items, evidence, risks and projects of the member's
  # own organisation, grouped by kind. English word forms are found ("log" finds "logging"); the term is always a
  # parameter, never SQL text.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the risk "Phishing without MFA" exists in "Acme Portal"
    And I add the evidence "MFA config" with the link "https://example.com/mfa" to "I.2(d)"

  @US-100 @integration
  Scenario: A word is found in several kinds
    When I search for "MFA"
    Then the results show the risk "Phishing without MFA" in "Acme Portal"
    And the results show the evidence "MFA config" in "Acme Portal"
    And the risk "Phishing without MFA" opens in the side panel of the Risks tab

  @US-100 @integration
  Scenario: Word forms and codes are found
    Given the risk "Weak logging of sign-ins" exists in "Acme Portal"
    When I search for "log"
    Then the results show the risk "Weak logging of sign-ins" in "Acme Portal"
    When I search for "I.2(d)"
    Then the results show the requirement "I.2(d)"

  @US-100 @iso27001-A.5.15 @integration
  Scenario: Another organisation's data is not found
    Given "Bo" has created the organisation "Beta ApS"
    And I am signed in as "Bo"
    When I search for "Acme Portal"
    Then there are no results
    When I search for "MFA"
    Then there are no results

  @US-100 @iso27001-A.8.28 @integration
  Scenario: The search term is never SQL
    When I search for "%' OR 1=1 --"
    Then there are no results
    When I search for "MFA"
    Then the results show the evidence "MFA config" in "Acme Portal"
