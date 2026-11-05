Feature: Evidence suggestions from tests and pull requests

  # US-058: passing tests tagged with a requirement (e.g. @cra-I.2(d)) and the merged pull requests that closed a linked
  # story are suggested as evidence. Tags and links decide it, not an AI. Nothing is evidence until a person accepts it.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in with Keycloak"
    And the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"

  @US-058 @iso27001-8.29 @integration
  Scenario: Passing tests are suggested as evidence
    When I upload test results where 3 scenarios tagged "@cra-I.2(d)" passed
    Then the row "I.2(d)" has the suggested evidence "Tests passed: 3 scenarios"

  @US-058 @iso27001-8.29 @integration
  Scenario: A failing test blocks the suggestion
    When I upload test results where 2 scenarios tagged "@cra-I.2(d)" passed and 1 failed
    Then the row "I.2(d)" has no suggested evidence
    And the row "I.2(d)" shows "1 failing test"

  @US-058 @iso27001-8.29 @integration
  Scenario: JUnit XML with tags as properties works too
    When I upload JUnit results where 2 scenarios tagged "cra-I.2(e)" passed
    Then the row "I.2(e)" has the suggested evidence "Tests passed: 2 scenarios"

  @US-058 @integration
  Scenario: Unknown tags are listed and ignored
    When I upload test results where 1 scenarios tagged "@cra-I.9(z)" passed
    Then the upload says "unknown tags ignored: @cra-I.9(z)"

  @US-058 @cra-annex7 @integration
  Scenario: The pull request that closed a story is suggested
    Given "[US-004] Sign in with Keycloak" is closed in GitHub by the pull request "PR 42 – Keycloak sign-in"
    When the project is synced with GitHub
    Then the row "I.2(d)" has the suggested evidence "PR 42 – Keycloak sign-in"

  @US-058 @aiact-art14 @integration
  Scenario: Accepting a suggestion makes it evidence
    Given the row "I.2(d)" has the suggested evidence "PR 42 – Keycloak sign-in"
    When I accept the suggested evidence "PR 42 – Keycloak sign-in"
    Then the row "I.2(d)" shows the evidence "PR 42 – Keycloak sign-in"
    And the activity log for "Acme ApS" has "EvidenceSuggestionAccepted" for "Anna" concerning "Acme Portal"

  @US-058 @iso27001-7.5 @integration
  Scenario: Accepted test results become a file with the passing scenarios
    Given I have uploaded test results where 3 scenarios tagged "@cra-I.2(d)" passed
    When I accept the suggested evidence "Tests passed: 3 scenarios"
    Then the row "I.2(d)" shows the evidence "Tests passed: 3 scenarios (test run" as a file listing the 3 scenarios

  @US-058 @integration
  Scenario: A rejected suggestion is not made again
    Given the row "I.2(d)" has the suggested evidence "PR 42 – Keycloak sign-in"
    And I reject the suggested evidence "PR 42 – Keycloak sign-in" with the reason "Only a refactoring"
    When the project is synced with GitHub
    Then the row "I.2(d)" has no suggested evidence

  @US-058 @integration
  Scenario: A requirement that does not apply gets no suggestions
    Given "I.2(d)" is not applicable for "Acme Portal"
    When I upload test results where 3 scenarios tagged "@cra-I.2(d)" passed
    Then the row "I.2(d)" has no suggested evidence

  @US-058 @iso27001-A.5.15 @integration
  Scenario: An auditor sees suggestions but cannot accept them
    Given the row "I.2(d)" has the suggested evidence "PR 42 – Keycloak sign-in"
    And "Bo" has joined "Acme ApS" as "Auditor"
    When "Bo" opens the "CRA" sub-tab of "Acme Portal"
    Then there is no "Accept" button
