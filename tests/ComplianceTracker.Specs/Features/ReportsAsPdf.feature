Feature: Every report as PDF

  # US-131: every report on the Reports tab can be downloaded as a PDF, made from the same report model as the other
  # formats. Every page shows the organisation, the project, the report, when it was made and by whom, and "page x of y".

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-131 @iso27001-7.5.2 @integration
  Scenario: The traceability matrix as PDF
    When I download the traceability report for "auditor" as PDF from the Reports tab
    Then the file is a PDF named "traceability-acme-portal-auditor-*.pdf"
    And the PDF contains "Acme ApS · Acme Portal"
    And the PDF contains "Traceability – Acme Portal"
    And every page of the PDF says "Page x of y" and "Made" and "by Anna"
    And the activity log has "ReportDownloaded" saying "PDF for"

  @US-131 @iso27001-7.5.2 @integration
  Scenario: A long table continues on the next page with its header
    When I download the traceability report for "team" as PDF from the Reports tab
    Then the PDF has more than 1 page
    And every page with a table shows the column "Requirement"

  @US-131 @cra-art31 @integration
  Scenario: The technical documentation as PDF
    Given the intended purpose of "Acme Portal" is "Web shop for spare parts"
    When I download the technical documentation as PDF from the Reports tab
    Then the file is a PDF named "*.pdf"
    And the PDF contains "Web shop for spare parts"
    And the activity log has "ReportDownloaded" saying "PDF for Technical documentation (draft)"

  @US-131 @integration
  Scenario: Danish letters are kept
    Given the project "Æblegrød Øst" exists
    And "Æblegrød Øst" uses "CRA – Regulation (EU) 2024/2847"
    When I download the traceability report of "Æblegrød Øst" for "management" as PDF
    Then the PDF contains "Æblegrød Øst"

  @US-131 @integration
  Scenario: The Reports tab offers PDF for every report
    When I open the Reports tab of "Acme Portal"
    Then the traceability report can be downloaded as "PDF"
    And the technical documentation can be downloaded as PDF
