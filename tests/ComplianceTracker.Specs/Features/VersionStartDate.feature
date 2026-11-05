Feature: Framework versions with a start date

  # US-035: a new version can apply from a later date. Until then the previous version stays current;
  # from the date the new one is current, and projects move with "Use newest" (US-011 R6).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has been uploaded and activated
    And the project "Acme Portal" exists
    And "Acme Portal" uses "Security policy" version 1

  @US-035 @iso27001-5.31 @integration
  Scenario: A version applies from its date
    Given the time is "2027-01-15T09:00:00Z"
    When I upload version 2 of "Security policy" that applies from "01-02-2027" and activate it
    Then the current version of "Security policy" is version 1
    And version 2 of "Security policy" is shown as "Upcoming (applies from 01-02-2027)"

  @US-035 @iso27001-5.31 @integration
  Scenario: On the date the new version becomes current
    Given the time is "2027-01-15T09:00:00Z"
    And version 2 of "Security policy" applies from "01-02-2027" and is active
    When the time is "2027-02-01T08:00:00Z"
    Then the current version of "Security policy" is version 2
    And the page of "Acme Portal" says "Security policy" has a newer version
    And the project "Acme Portal" uses "Security policy" version 1

  @US-035 @integration
  Scenario: A project can prepare for an upcoming version
    Given the time is "2027-01-15T09:00:00Z"
    And version 2 of "Security policy" applies from "01-02-2027" and is active
    When I move "Acme Portal" to version 2 of "Security policy"
    Then the Frameworks tab of "Acme Portal" shows "Security policy" version 2 as "Upcoming"

  @US-035 @iso27001-7.5.3 @integration
  Scenario: The date is locked when the version is activated
    Given the time is "2027-01-15T09:00:00Z"
    And version 2 of "Security policy" applies from "01-02-2027" and is active
    When I try to change the start date of version 2 of "Security policy" to "01-03-2027"
    Then I see the error "An active version cannot be changed"

  @US-035 @integration
  Scenario: A built-in regulation that has not started yet can be used
    Given the time is "2026-11-01T09:00:00Z"
    And "CRA – Regulation (EU) 2024/2847" is switched on
    When I add "CRA – Regulation (EU) 2024/2847" to the project "Acme Portal"
    Then the Frameworks tab of "Acme Portal" shows "CRA – Regulation (EU) 2024/2847" as "Applies from 11-12-2027"

  @US-035 @iso27001-A.8.15 @integration
  Scenario: Activating a version with a date is logged
    When I upload version 2 of "Security policy" that applies from "01-02-2027" and activate it
    Then the "CatalogActivated" entry says "Applies from 01-02-2027"
