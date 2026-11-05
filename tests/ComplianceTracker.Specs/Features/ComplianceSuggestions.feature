Feature: AI suggestions and coverage on the Compliance tab

  # US-044: the coverage is summed up per regulation, the requirements can be filtered, and the AI's
  # suggestions are decided next to the requirements. The AI is a pretend one (FakeAi).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in with Keycloak"

  @US-044 @iso27001-9.1 @integration
  Scenario: The coverage is summed up at the top
    Given the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"
    And "I.2(k)" is not applicable for "Acme Portal"
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the summary shows 1 covered of 71, 70 not covered and 1 not applicable

  @US-044 @integration
  Scenario: Filter on not covered
    Given the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"
    When I open the "CRA" sub-tab of "Acme Portal" showing "Not covered"
    Then the row "I.2(d)" is not listed
    And the row "I.2(a)" is listed

  @US-044 @integration
  Scenario: Ask for suggestions for the not-covered requirements
    Given the AI suggests "[US-004] Sign in with Keycloak" for "I.2(d)"
    When I ask for suggestions on the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" shows the suggestion "[US-004] Sign in with Keycloak"
    And the summary shows AI suggestions

  @US-044 @integration
  Scenario: Covered requirements get no new suggestions
    Given the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"
    And the AI suggests "[US-004] Sign in with Keycloak" for "I.2(d)"
    When I ask for suggestions on the "CRA" sub-tab of "Acme Portal"
    Then no suggestion is saved for "I.2(d)"

  @US-044 @cra-art13 @integration
  Scenario: Approve a suggestion on the Compliance tab
    Given the row "I.2(d)" on the "CRA" sub-tab has the suggestion "[US-004] Sign in with Keycloak"
    When I approve the suggestion on the row "I.2(d)"
    Then the row "I.2(d)" is "Covered" by "[US-004] Sign in with Keycloak"
    And the activity log for "Acme ApS" has "MappingApproved" for "Anna" concerning "Acme Portal"

  @US-044 @integration
  Scenario: An auditor sees suggestions but cannot decide
    Given the row "I.2(d)" on the "CRA" sub-tab has the suggestion "[US-004] Sign in with Keycloak"
    And "Bo" has joined "Acme ApS" as "Auditor"
    When "Bo" opens the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" shows the suggestion "[US-004] Sign in with Keycloak"
    And there is no "Approve" button

  @US-044 @integration
  Scenario: The AI is not reachable
    Given the local AI is not available
    When I ask for suggestions on the "CRA" sub-tab of "Acme Portal"
    Then I see the error "The local AI is not available. Start Ollama (or the configured model server) and try again"
    And no suggestions are saved

  @US-044 @integration
  Scenario: The Product requirements tab links to the suggestions
    Given the row "I.2(d)" on the "CRA" sub-tab has the suggestion "[US-004] Sign in with Keycloak"
    When I open the "Product requirements" tab of "Acme Portal"
    Then the user story "[US-004] Sign in with Keycloak" links to its AI suggestions on the "CRA" sub-tab
