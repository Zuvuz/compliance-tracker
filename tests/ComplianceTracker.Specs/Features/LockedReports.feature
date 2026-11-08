Feature: Locked reports

  # US-050: a report is locked as PDF with the date, who locked it with their role, and the SHA-256 fingerprint of the
  # file. A locked report is never changed or deleted, and anyone in the organisation can check a file against it.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-050 @iso27001-7.5.3 @integration
  Scenario: Lock a report
    When I lock the traceability report of "Acme Portal" for "auditor"
    Then the locked reports of "Acme Portal" list "Traceability – Acme Portal (Auditor)" locked by "Anna (Organisation admin)" with a fingerprint
    And the activity log for "Acme ApS" has "ReportLocked" for "Anna" concerning "Acme Portal"

  @US-050 @iso27001-7.5.3 @integration
  Scenario: A downloaded locked report matches its fingerprint
    Given I have locked the traceability report of "Acme Portal" for "team"
    When I check the downloaded file of that report
    Then the check says "Matches the report locked on"

  @US-050 @iso27001-7.5.3 @integration
  Scenario: A changed file does not match
    Given I have locked the traceability report of "Acme Portal" for "team"
    When I check a changed copy of that report
    Then the check says "Does not match any locked report"

  @US-050 @cra-annex7 @integration
  Scenario: The technical documentation is locked too
    When I lock the technical documentation of "Acme Portal"
    Then the locked reports of "Acme Portal" list "Technical documentation (draft) – Acme Portal" locked by "Anna (Organisation admin)" with a fingerprint

  @US-050 @iso27001-A.5.15 @integration
  Scenario: An auditor can download but not lock
    Given I have locked the traceability report of "Acme Portal" for "team"
    And "Bo" has joined "Acme ApS" as "Auditor"
    When "Bo" opens the Reports tab of "Acme Portal"
    Then "Bo" can download the locked report "Traceability – Acme Portal (Team)"
    And there is no "Lock as PDF" button
    And "Bo" cannot lock a report of "Acme Portal"

  @US-050 @iso27001-A.5.33 @integration
  Scenario: The database refuses to change or delete a locked report
    Given I have locked the traceability report of "Acme Portal" for "team"
    Then the database refuses to change the locked report
    And the database refuses to delete the locked report

  @US-050 @iso27001-7.5.3 @integration
  Scenario: Locking again makes a new snapshot
    Given I have locked the traceability report of "Acme Portal" for "team"
    When I lock the traceability report of "Acme Portal" for "team"
    Then "Acme Portal" has 2 locked reports
