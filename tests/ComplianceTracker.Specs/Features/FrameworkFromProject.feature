Feature: Add a framework from the project

  # US-164: the project's Frameworks tab offers the switched-on frameworks and, apart, the built-in ones not switched on
  # yet. Adding one of those switches it on for the organisation and adds it to the project in one step.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-164 @iso27001-A.5.31 @integration
  Scenario: A framework not switched on is switched on and added
    When I add "AI Act – Regulation (EU) 2024/1689" to "Acme Portal" from its Frameworks tab
    Then "AI Act – Regulation (EU) 2024/1689" is switched on for the organisation
    And the Frameworks tab of "Acme Portal" lists "AI Act – Regulation (EU) 2024/1689"
    And the activity log for "Acme ApS" has "FrameworkEnabled" and "ProjectFrameworkAdded" for "Anna"

  @US-164 @integration
  Scenario: A developer is only offered the switched-on frameworks
    Given "Bo" has joined "Acme ApS" as "Developer"
    And I am signed in as "Bo"
    When I open the Frameworks tab of "Acme Portal"
    Then I am not offered "AI Act – Regulation (EU) 2024/1689"
    And adding "AI Act – Regulation (EU) 2024/1689" anyway is refused
