Feature: The traceability matrix for each reader
  US-120: the traceability matrix can be downloaded as a formatted Excel file or as CSV, made for the team, an auditor
  or the management. Each reader gets a file that is easy to read and has the content they need.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in with Keycloak"

  @US-120 @iso27001-7.5.3 @integration
  Scenario: The Excel file is formatted
    When I download the traceability matrix of "Acme Portal" as Excel for the team
    Then the workbook has the sheets "Summary" and "CRA"
    And the header of "CRA" is bold, frozen and filtered
    And on "CRA" the status of "I.2(a)" is "Not covered" in red

  @US-120 @iso27001-9.1 @integration
  Scenario: The team sees what to do first
    Given the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"
    When I download the traceability matrix of "Acme Portal" as Excel for the team
    Then the first row of "CRA" is a requirement that is "Not covered"
    And on "CRA" the row "I.2(d)" lists the story "[US-004] Sign in with Keycloak"

  @US-120 @iso27001-9.2 @cra-annexVII @integration
  Scenario: The auditor sees who decided
    Given the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"
    And "I.2(k)" is not applicable for "Acme Portal"
    When I download the traceability matrix of "Acme Portal" as Excel for an auditor
    Then on "CRA" the row "I.2(d)" says "Decided by" "Anna" and "Role" "Organisation admin"
    And on "CRA" the status of "I.2(k)" is "Not applicable" in grey

  @US-120 @iso27001-9.3 @integration
  Scenario: The management gets the overview only
    When I download the traceability matrix of "Acme Portal" as Excel for the management
    Then the workbook has only the sheet "Summary"
    And the summary says "Coverage – CRA" is "0 % (0 of 72)"
    And the summary says "Open High and Critical risks" is "0"

  @US-120 @integration
  Scenario: The download is logged
    When I download the traceability matrix of "Acme Portal" as Excel for an auditor
    Then the download is logged as "Excel for Auditor: CRA"
