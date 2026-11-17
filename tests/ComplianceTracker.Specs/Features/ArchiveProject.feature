Feature: Archive a project

  # US-028: a project is archived instead of deleted. It is hidden from the lists until "Show archived", can be read
  # but not changed, can be restored, and does not stop a built-in framework from being switched off.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the projects "Acme Portal" and "Old App" exist

  @US-028 @iso27001-7.5.3 @integration
  Scenario: An archived project is hidden from the list
    When I archive "Old App"
    Then the organisation page lists the project "Acme Portal"
    And the organisation page does not list the project "Old App"
    And there is no Delete button on "Acme Portal"

  @US-028 @iso27001-7.5.3 @integration
  Scenario: Archived projects can still be shown
    Given I archive "Old App"
    When I show archived projects
    Then the organisation page lists the project "Old App" marked "Archived"

  @US-028 @integration
  Scenario: An archived project can be read but not changed
    Given I archive "Old App"
    And the framework "Security policy" has been uploaded and activated
    When I add "Security policy" to the project "Old App"
    Then I see the error "An archived project cannot be changed"
    And the page of "Old App" says it was archived by "Anna"

  @US-028 @iso27001-A.8.15 @integration
  Scenario: Restore an archived project
    Given I archive "Old App"
    When I restore "Old App"
    Then the organisation page lists the project "Old App"
    And the activity log for "Acme ApS" has "ProjectArchived" with "Old App"
    And the activity log for "Acme ApS" has "ProjectRestored" with "Old App"

  @US-028 @cra-art13 @integration
  Scenario: An archived project does not stop switching a framework off
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    And "Old App" uses "CRA – Regulation (EU) 2024/2847"
    And I archive "Old App"
    When I switch "CRA – Regulation (EU) 2024/2847" off
    Then I see "CRA – Regulation (EU) 2024/2847" switched off
