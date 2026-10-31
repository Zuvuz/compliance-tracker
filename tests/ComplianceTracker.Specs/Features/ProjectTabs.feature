Feature: Project page with tabs

  # US-022: the project page is split into tabs with tables: Overview, Frameworks, User stories and Activity.
  # Each tab has its own address, and the header is the same on all of them.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has been uploaded and activated
    And the project "Acme Portal" exists

  @US-022 @iso27001-A.5.9 @integration
  Scenario: The header is the same on every tab
    When I open the "Frameworks" tab of "Acme Portal"
    Then the header shows "Acme Portal" in phase "Planning" with intended purpose "Test purpose"
    And the "Frameworks" tab is active

  @US-022 @integration
  Scenario Outline: Each tab has its own address
    When I open the address "/Projects/Details/{id}/<address>" of "Acme Portal"
    Then the "<tab>" tab is active

    Examples:
      | address      | tab          |
      | frameworks   | Frameworks   |
      | user-stories | User stories |
      | activity     | Activity     |

  @US-022 @integration
  Scenario: An unknown tab is not found
    When I open the address "/Projects/Details/{id}/nonsense" of "Acme Portal"
    Then the page is not found

  @US-022 @iso27001-A.5.9 @integration
  Scenario: The overview summarises the frameworks
    Given "Acme Portal" uses "Security policy" version 1
    And a new version of "Security policy" has been uploaded and activated
    When I open the "Overview" tab of "Acme Portal"
    Then the summary says "1 framework, 1 not on newest version"

  @US-022 @integration
  Scenario: The frameworks table shows who added each framework
    Given the time is "2026-10-24T09:00:00Z"
    And "Acme Portal" uses "Security policy" version 1
    When I open the "Frameworks" tab of "Acme Portal"
    Then the row for "Security policy" shows source "Uploaded", version "1", status "Newest" and added by "Anna"
    And the row for "Security policy" was added on "24-10-2026"

  @US-022 @integration
  Scenario: The frameworks table says when a newer version exists
    Given "Acme Portal" uses "Security policy" version 1
    And a new version of "Security policy" has been uploaded and activated
    When I open the "Frameworks" tab of "Acme Portal"
    Then the row for "Security policy" shows source "Uploaded", version "1", status "Newer version available" and added by "Anna"

  @US-022 @iso27001-A.8.15 @integration
  Scenario: The activity tab shows the project's log, newest first
    Given "Acme Portal" uses "Security policy" version 1
    And I remove "Security policy" from the project "Acme Portal"
    When I open the "Activity" tab of "Acme Portal"
    Then the first row is "ProjectFrameworkRemoved" by "Anna"
    And the last row is "ProjectCreated" by "Anna"

  @US-022 @iso27001-A.8.15 @integration
  Scenario: The activity tab shows what changed
    Given "Acme Portal" is in phase "Development"
    When I open the "Activity" tab of "Acme Portal"
    Then the first row is "ProjectPhaseChanged" by "Anna" with "Phase: Planning → Development"

  @US-022 @iso27001-A.5.15 @integration
  Scenario: Other organisations cannot see the activity
    Given "Bo" has created the organisation "Beta ApS"
    When "Bo" opens the address "/Projects/Details/{id}/activity" of "Acme Portal"
    Then the page is not found

  @US-022 @integration
  Scenario: Empty tables say so
    When I open the "Frameworks" tab of "Acme Portal"
    Then I see "No frameworks yet"
