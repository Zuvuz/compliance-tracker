Feature: AI buttons show that they work

  # US-170: a form that asks the AI (or uploads a file) says "Working… this can take a minute" and cannot be sent twice;
  # wwwroot/js/site.js handles data-busy. Without JavaScript the forms work as before.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-170 @integration
  Scenario: The risk agent's button shows progress
    When I open the "Risks" tab of "Acme Portal"
    Then the form "form#draft-risks" says "Working… this can take a minute" while it works

  @US-170 @integration
  Scenario: The Compliance tab's AI buttons show progress
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the form "form#find-needs-action" says "Working… this can take a minute" while it works
    And the form "form#suggest-not-applicable" says "Working… this can take a minute" while it works
