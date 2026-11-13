Feature: Export the activity log for a period

  # US-055: the organisation's activity log can be exported as CSV or PDF, with the same filters as the Activity page,
  # for the same people (Admins and Auditors, US-102 R6). Without dates it is the last 90 days. The export is logged.

  Background:
    Given an empty database
    And the time is "2026-11-12T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-055 @iso27001-A.8.15 @integration
  Scenario: The log is exported as CSV
    When I export the activity log as "csv"
    Then the export is a CSV file named "activity-log-2026-08-14-to-2026-11-12.csv"
    And the export has the columns "Time (UTC);Action;Project;Concerning;Person;Role;Details"
    And the export has a row "ProjectCreated" for "Acme Portal" by "Anna" as "Organisation admin"

  @US-055 @iso27001-A.8.15 @integration
  Scenario: The export is logged with its period
    When I export the activity log as "csv"
    Then the activity log has "ActivityLogExported" saying "CSV, 14-08-2026 – 12-11-2026, all projects (2 entries)"

  @US-055 @integration
  Scenario: A period outside the activity gives no rows
    When I export the activity log as "csv" from "2026-01-01" to "2026-01-31"
    Then the export has no rows

  @US-055 @integration
  Scenario: The log is exported as PDF
    When I export the activity log as "pdf"
    Then the export is a PDF file

  @US-055 @iso27001-A.5.35 @integration
  Scenario: An auditor can export
    Given "Bo" has joined "Acme ApS" as "Auditor"
    When "Bo" exports the activity log as "csv"
    Then the export has a row "ProjectCreated" for "Acme Portal" by "Anna" as "Organisation admin"

  @US-055 @iso27001-A.5.15 @integration
  Scenario: A developer cannot export
    Given "Carl" has joined "Acme ApS" as "Developer"
    When "Carl" exports the activity log as "csv"
    Then access is denied
