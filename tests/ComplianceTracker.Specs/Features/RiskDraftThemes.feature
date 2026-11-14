Feature: Several risk drafts in one run

  # US-157: the risk agent asks one round per threat theme (access, data, updates, availability, and the uncovered
  # requirements and the events and findings when there are any), so even a small local model gives several risks.
  # The drafts are shown grouped by theme; duplicates across themes are left out, and a theme that gave nothing says so.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-157 @cra-Art.13 @integration
  Scenario: Each theme gives its own drafts
    Given the AI drafts "Account takeover" for "Access and sign-in"
    And the AI drafts "Data leak" for "Data and privacy"
    And the AI drafts "Outdated library" for "Updates and supply chain"
    And the AI drafts "Denial of service" for "Availability"
    When I ask the AI to draft risks for "Acme Portal"
    Then the Risks tab of "Acme Portal" shows "Account takeover" under "Access and sign-in"
    And the Risks tab of "Acme Portal" shows "Data leak" under "Data and privacy"
    And the Risks tab of "Acme Portal" shows "Outdated library" under "Updates and supply chain"
    And the Risks tab of "Acme Portal" shows "Denial of service" under "Availability"

  @US-157 @integration
  Scenario: The same risk from two themes is kept once
    Given the AI drafts "Account takeover" for "Access and sign-in"
    And the AI drafts "Account takeover" for "Requirements not met"
    When I ask the AI to draft risks for "Acme Portal"
    Then the Risks tab of "Acme Portal" has the draft "Account takeover" once

  @US-157 @integration
  Scenario: A theme that cannot be reached does not stop the others
    Given the AI drafts "Account takeover" for "Access and sign-in"
    And the AI cannot be reached for "Availability"
    When I ask the AI to draft risks for "Acme Portal"
    Then the page says "Availability: no answer from the AI"
    And the Risks tab of "Acme Portal" shows "Account takeover" under "Access and sign-in"

  @US-157 @integration
  Scenario: Without events or findings that theme is not asked
    When I ask the AI to draft risks for "Acme Portal"
    Then the AI was asked about "Access and sign-in", "Data and privacy", "Updates and supply chain", "Availability" and "Requirements not met"
