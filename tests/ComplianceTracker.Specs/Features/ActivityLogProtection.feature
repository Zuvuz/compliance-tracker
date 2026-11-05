Feature: The activity log cannot be changed

  # US-091: the database refuses to change or delete log entries, and every entry holds a fingerprint of itself and of
  # the entry before it. /LogCheck goes through the chain, so a change made around the tracker is found.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-091 @iso27001-A.8.15 @integration
  Scenario: The database refuses to change a log entry
    When someone changes a log entry directly in the database
    Then the database refuses with "The activity log cannot be changed"

  @US-091 @iso27001-A.8.15 @integration
  Scenario: The database refuses to delete a log entry
    When someone deletes a log entry directly in the database
    Then the database refuses with "The activity log cannot be changed"

  @US-091 @iso27001-A.8.15 @integration
  Scenario: New entries are chained
    When I check the activity log
    Then the log is intact
    And every entry has a fingerprint

  @US-091 @iso27001-A.8.15 @integration
  Scenario: A changed entry is found
    Given the second log entry has been changed around the tracker
    When I check the activity log
    Then the log is broken at the second entry

  @US-091 @iso27001-A.8.15 @integration
  Scenario: A removed entry is found
    Given I have created an invitation link with the role "Developer"
    And the second log entry has been removed around the tracker
    When I check the activity log
    Then the log is broken at the second entry

  @US-091 @iso27001-A.5.15 @integration
  Scenario: Only Admins and Auditors can check the log
    Given "Carl" has joined "Acme ApS" as "Developer"
    And "Ida" has joined "Acme ApS" as "Auditor"
    When "Carl" checks the activity log
    Then access is denied
    When "Ida" checks the activity log
    Then the log is intact
