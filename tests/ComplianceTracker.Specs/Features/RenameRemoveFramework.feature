Feature: Rename or remove an uploaded framework

  # #52: a framework's name typed wrong at upload can be fixed, and the page shows the test tags the name gives. A framework
  # no project uses can be removed: it is hidden everywhere and kept for the history.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Fake ISO" has been uploaded and activated

  @integration
  Scenario: A framework is renamed and its test tags follow the name
    When I rename the framework "Fake ISO" to "ISO27001 – Demo"
    Then the frameworks page lists "ISO27001 – Demo" with the test tags "@iso27001-…"
    And the frameworks page does not list "Fake ISO"

  @integration
  Scenario: A framework no project uses is removed
    When I remove the framework "Fake ISO"
    Then the frameworks page does not list "Fake ISO"

  @integration
  Scenario: A framework a project uses cannot be removed
    Given the project "Acme Portal" exists
    And "Acme Portal" uses "Fake ISO"
    When I remove the framework "Fake ISO"
    Then I see the error "Remove Fake ISO from these projects first: Acme Portal"
    And the frameworks page lists "Fake ISO" with the test tags "@fake-iso-…"
