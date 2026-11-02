Feature: Risk register per project

  # US-042: the product's cybersecurity risks (CRA Art. 13, ISO 27001 6.1.2, 6.1.3, 8.2),
  # with likelihood and impact from 1 to 5, an owner, a treatment, measures and reviews.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-042 @cra-art13 @iso27001-6.1.2 @integration
  Scenario: Add a risk and get its level
    When I add the risk "Stolen session cookie" to "Acme Portal" with likelihood 3 and impact 4
    Then the risk "Stolen session cookie" has the level "High"
    And the matrix shows 1 open risk at likelihood 3 and impact 4

  @US-042 @integration
  Scenario: A risk needs a title
    When I add the risk "" to "Acme Portal" with likelihood 3 and impact 4
    Then I see the error "Risk title is required"
    And the title field is shown again

  @US-042 @iso27001-6.1.3 @integration
  Scenario: An accepted risk needs a reason
    Given the risk "Old TLS on legacy API" exists in "Acme Portal"
    When I accept the risk "Old TLS on legacy API" with the reason ""
    Then I see the error "Give a reason for accepting the risk"

  @US-042 @iso27001-6.1.3 @integration
  Scenario: An accepted risk shows who accepted it and why
    Given the risk "Old TLS on legacy API" exists in "Acme Portal"
    When I accept the risk "Old TLS on legacy API" with the reason "Retired in Q1"
    Then the risk "Old TLS on legacy API" has the status "Accepted"
    And the risk "Old TLS on legacy API" says "Accepted by Anna (Organisation admin): Retired in Q1"

  @US-042 @iso27001-6.1.3 @integration
  Scenario: A risk is linked to what reduces it
    Given the risk "Stolen session cookie" exists in "Acme Portal"
    And the project "Acme Portal" has the user story "[US-004] Sign in with Keycloak"
    When I link the risk "Stolen session cookie" to "[US-004] Sign in with Keycloak"
    Then the risk "Stolen session cookie" shows "[US-004] Sign in with Keycloak" as a measure
    And the risk "Stolen session cookie" has the status "Treated"

  @US-042 @integration
  Scenario: A risk to reduce without measures is marked
    Given the risk "Stolen session cookie" exists in "Acme Portal"
    When I open the "Risks" tab of "Acme Portal"
    Then the risk "Stolen session cookie" is marked "No measures yet"

  @US-042 @iso27001-8.2 @integration
  Scenario: Risks not reviewed for a year are due
    Given the time is "2025-11-01T09:00:00Z"
    And the risk "Stolen session cookie" exists in "Acme Portal"
    And I mark the risk "Stolen session cookie" as reviewed
    When the time is "2026-11-02T09:00:00Z"
    And I open the "Risks" tab of "Acme Portal"
    Then the risk "Stolen session cookie" is marked "Review due"

  @US-042 @integration
  Scenario: Closed risks are kept
    Given the risk "Old TLS on legacy API" exists in "Acme Portal"
    When I close the risk "Old TLS on legacy API" with the reason "API removed"
    Then the risk "Old TLS on legacy API" has the status "Closed"
    And the register does not list "Old TLS on legacy API" unless closed risks are shown

  @US-042 @iso27001-A.5.15 @integration
  Scenario: Other organisations cannot see the risks
    Given "Bo" has created the organisation "Beta ApS"
    When "Bo" opens the address "/Projects/Details/{id}/risks" of "Acme Portal"
    Then the page is not found

  @US-042 @iso27001-A.8.15 @integration
  Scenario: Changes are logged with old and new values
    Given the risk "Stolen session cookie" exists in "Acme Portal"
    When I change the likelihood of "Stolen session cookie" to 2
    Then the "RiskUpdated" entry says "Stolen session cookie: Likelihood: 3 → 2"
