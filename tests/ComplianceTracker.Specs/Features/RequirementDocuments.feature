Feature: Upload a requirement specification as Excel or CSV

  # US-032: a project that follows a waterfall model uploads its requirement specification.
  # Each requirement becomes a work item of type Requirement, next to the user stories from GitHub.
  # A new upload is a new version, matched on ID; nothing is deleted.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-032 @iso27001-A.8.26 @integration
  Scenario: Requirements are read from an Excel file
    When I upload the requirement specification "kravspec.xlsx" to "Acme Portal" with these rows:
      | ID      | Title         | Acceptance criteria      |
      | REQ-001 | Log in        | The user can log in      |
      | REQ-002 | Export as PDF | A report can be exported |
      |         | Missing ID    |                          |
    Then the project has the requirements "REQ-001" and "REQ-002"
    And I see "Row 4 skipped: no ID"

  @US-032 @integration
  Scenario: A CSV file with semicolons is read
    When I upload the CSV file "kravspec.csv" with the separator ";" and the requirement "REQ-001;Log in"
    Then the project has the requirement "REQ-001"

  @US-032 @integration
  Scenario: Danish column names are accepted
    When I upload the requirement specification "kravspec.xlsx" to "Acme Portal" with these rows:
      | Krav-ID | Titel   | Acceptkriterier        |
      | REQ-001 | Log ind | Brugeren kan logge ind |
    Then the project has the requirement "REQ-001"

  @US-032 @integration
  Scenario: Several specifications are kept apart
    Given "Acme Portal" has version 1 of "kravspec-web.xlsx" with "WEB-1 Log in"
    And "Acme Portal" has version 1 of "kravspec-app.xlsx" with "APP-1 Push"
    When I upload version 2 of "kravspec-web.xlsx" with only "WEB-2 Export"
    Then the requirement "APP-1" is still active
    And the requirement "WEB-1" is marked "Removed in version 2"

  @US-032 @integration
  Scenario: Duplicate IDs refuse the whole file
    When I upload a requirement specification where "REQ-012" is in rows 4 and 9
    Then I see the error "REQ-012 appears twice (rows 4 and 9)"
    And the project has no requirements

  @US-032 @iso27001-A.8.32 @integration
  Scenario: A new version updates requirements by ID
    Given "Acme Portal" has version 1 of "kravspec.xlsx" with "REQ-003 Log in" and "REQ-005 Print"
    When I upload version 2 with "REQ-003 Log in with MitID" and "REQ-020 Export"
    Then I see "Version 2: 1 added, 1 changed, 1 removed, 0 skipped"
    And the requirement "REQ-005" is marked "Removed in version 2"
    And version 1 can still be downloaded

  @US-032 @integration
  Scenario: Other file types are refused
    When I upload the requirement specification "kravspec.pdf" to "Acme Portal"
    Then I see the error "Only .xlsx, .csv and .docx files can be uploaded"

  @US-032 @iso27001-A.5.15 @integration
  Scenario: Other organisations cannot see the requirements
    Given "Acme Portal" has version 1 of "kravspec.xlsx" with "REQ-001 Log in"
    And "Bo" has created the organisation "Beta ApS"
    When "Bo" opens the work items of "Acme Portal"
    Then the page is not found

  @US-032 @iso27001-A.5.15 @integration
  Scenario: Other organisations cannot download the file
    Given "Acme Portal" has version 1 of "kravspec.xlsx" with "REQ-001 Log in"
    And "Bo" has created the organisation "Beta ApS"
    When "Bo" downloads version 1 of "kravspec.xlsx"
    Then the page is not found

  @US-032 @iso27001-A.8.15 @integration
  Scenario: Uploads are logged
    When I upload the requirement specification "kravspec.xlsx" to "Acme Portal" with the requirement "REQ-001 Log in"
    Then the activity log for "Acme ApS" has "RequirementDocumentUploaded" for "Anna"
