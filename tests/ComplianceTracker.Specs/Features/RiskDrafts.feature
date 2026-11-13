Feature: An AI agent drafts a risk assessment for the project

  # US-112: "Draft risks with AI" on the Risks tab lets an AI agent read the project (its purpose, classification,
  # uncovered requirements, open events and findings) and propose risks with why. A draft never becomes a risk until a
  # person accepts it, after editing it if needed; a discarded draft is not proposed again.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-112 @cra-Art.13 @aiact-art14 @integration
  Scenario: The agent drafts risks with why, and none is a risk yet
    When I ask the AI to draft risks for "Acme Portal"
    Then the Risks tab of "Acme Portal" has the drafts "Stolen session cookie" and "Unpatched open-source component"
    And the draft "Stolen session cookie" says it is based on "I.2(d) not covered"
    And "Acme Portal" has no risks
    And the agent was told the intended purpose and the uncovered requirements

  @US-112 @iso27001-6.1.2 @integration
  Scenario: Accepting a draft makes an AI-drafted risk with the person's changes
    Given I ask the AI to draft risks for "Acme Portal"
    When I accept the draft "Stolen session cookie" as "Session cookie stolen via XSS" with likelihood 2 and impact 5
    Then the risk "Session cookie stolen via XSS" is marked AI-drafted
    And the risk "Session cookie stolen via XSS" has the level "High"

  @US-112 @integration
  Scenario: A draft that an existing risk covers is left out
    Given the risk "Stolen session cookie" exists in "Acme Portal"
    When I ask the AI to draft risks for "Acme Portal"
    Then the AI says it left out "Stolen session cookie" because the risk "Stolen session cookie" covers it
    And the Risks tab of "Acme Portal" has only the draft "Unpatched open-source component"

  @US-112 @integration
  Scenario: A discarded draft is not proposed again
    Given I ask the AI to draft risks for "Acme Portal"
    And I discard the draft "Unpatched open-source component" because "We have an SBOM and Dependabot"
    When I ask the AI to draft risks for "Acme Portal"
    Then the Risks tab of "Acme Portal" has only the draft "Stolen session cookie"

  @US-112 @integration
  Scenario: Without the AI nothing is drafted
    Given the local AI is not available
    When I ask the AI to draft risks for "Acme Portal"
    Then I see the error "The local AI cannot be reached right now, so no risks were drafted"
