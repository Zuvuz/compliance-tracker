Feature: Get started

  # US-096: a short checklist on /Projects leads a new customer from an empty tracker to a requirement that is
  # covered and proven. Each step is ticked by itself when its data exists.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-096 @integration
  Scenario: A new organisation sees the first steps
    When I open the address "/Projects"
    Then the "Get started" card shows "Create the organisation" as "Done"
    And the "Get started" card shows "Create a project" as "To do"
    And the step "Create a project" links to "/Projects/Create"

  @US-096 @integration
  Scenario: A step is done by itself
    Given the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    When I open the address "/Projects"
    Then the "Get started" card shows "Create a project" as "Done"
    And the "Get started" card shows "Switch on a regulation" as "Done"
    And the "Get started" card shows "Add the project's frameworks" as "Done"
    And the "Get started" card shows "Add evidence to a requirement" as "To do"

  @US-096 @integration
  Scenario: The card can be hidden and shown again
    When I hide the "Get started" card
    Then the "Get started" card is not shown
    When I show the "Get started" card again
    Then the "Get started" card shows "Create a project" as "To do"

  @US-096 @integration
  Scenario: The Auditor sees the steps without links
    Given "Ida" has joined "Acme ApS" as "Auditor"
    When "Ida" opens the projects page
    Then her "Get started" card has no links
