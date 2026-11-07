Feature: All reports in one tab
  US-129: a project's reports are on one "Reports" tab, each with who it is for and what it contains, so the right
  report is easy to find and hand over (ISO/IEC 27001 7.5.3, 9.1, 9.3; CRA Annex VII).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-129 @iso27001-7.5.3 @cra-annex7 @integration
  Scenario: The Reports tab lists the reports
    When I open the "Reports" tab of "Acme Portal"
    Then I see the reports "Traceability" and "Technical documentation (draft)"
    And the "Traceability" report is for "The team, an auditor or the management"
    And the "Traceability" report can be downloaded
    And the "Technical documentation (draft)" report can be downloaded

  @US-129 @iso27001-9.3 @integration
  Scenario: The traceability report is downloaded from the tab
    When I download the traceability report "management" as "xlsx" from the Reports tab of "Acme Portal"
    Then the download is an Excel file

  @US-129 @integration
  Scenario: A report that cannot be made says why
    Given the project "Beta App" exists
    When I open the reports of "Beta App"
    Then the "Traceability" report cannot be made because "Add a framework to the project to make a traceability report"
    And the "Technical documentation (draft)" report cannot be made because "Add the CRA to the project to make the technical documentation draft"

  @US-129 @integration
  Scenario: The Overview links to the tab
    When I open the "Overview" tab of "Acme Portal"
    Then the Reports card links to the "Reports" tab
