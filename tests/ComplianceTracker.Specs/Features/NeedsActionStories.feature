Feature: Needs action suggests the project's own stories first

  # #52 (US-155, US-020): on a requirement marked "Needs action", the AI first suggests stories the project already has.
  # Drafting a new story is under "More".

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in with Keycloak"

  @US-155 @US-020 @integration
  Scenario: The project's own stories are suggested from the needs-action box
    Given "I.2(d)" is marked as needing action
    And the AI suggests "[US-004] Sign in with Keycloak" for "I.2(d)"
    When I ask for stories from the project on the page of "I.2(d)"
    Then the row "I.2(d)" shows the suggestion "[US-004] Sign in with Keycloak"

  @US-155 @integration
  Scenario: Suggesting stories comes first and a new story is under More
    Given "I.2(d)" is marked as needing action
    Then the needs-action box of "I.2(d)" starts with "Suggest stories from the project" and has "Create a new story with the AI" under "More"

  @US-155 @integration
  Scenario: A requirement already covered gets no new suggestions
    Given "I.2(d)" is marked as needing action
    And the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"
    When I post "Suggest stories from the project" for "I.2(d)" anyway
    Then the page says "I.2(d) is already covered by a story"
