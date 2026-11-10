Feature: The management report shows the most important first

  # US-141: the management report starts with the status per regulation, what management must know now, the key figures,
  # the biggest gaps and the open high risks, and ends with the details. PDF and Excel have the same sections in the same
  # order. "Since last" compares with the newest locked management report of the same regulations.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-141 @iso27001-9.3 @integration
  Scenario: The sections come in order of importance
    When I download the traceability report for "management" as PDF from the Reports tab
    Then the PDF has the sections in this order:
      | Section                        |
      | Status                         |
      | What management must know now  |
      | Key figures                    |
      | Biggest gaps                   |
      | Open high risks                |
      | Details                        |
    And the PDF contains "Support period not set; CRA requires one (Art. 13(8))"
    And the PDF contains "No locked report to compare with"

  @US-141 @iso27001-9.3 @integration
  Scenario: The status shows colour and change since the last locked report
    Given the first 3 CRA requirements of "Acme Portal" are covered by the user story "[US-001] Secure defaults"
    And I have locked the traceability report of "Acme Portal" for "management"
    And the first 5 CRA requirements of "Acme Portal" are covered by the user story "[US-001] Secure defaults"
    When I download the traceability report for "management" as PDF from the Reports tab
    Then the PDF contains "7 % (5 of 72)"
    And the PDF contains "Up +3 since"

  @US-141 @cra-13 @integration
  Scenario: A requirement linked to a high risk is the biggest gap
    Given the risk "No secure default config" exists in "Acme Portal"
    And the risk "No secure default config" has the requirement "I.2(k)" as a measure
    When I download the traceability matrix of "Acme Portal" as Excel for the management
    Then on "Biggest gaps" the first row says "Code" "I.2(k)" and "Why" "Linked to a High risk"
    And on "Open high risks" the first row says "Risk" "No secure default config" and "Level" "High"

  @US-141 @iso27001-9.3 @integration
  Scenario: Excel has the same sections as sheets
    When I download the traceability matrix of "Acme Portal" as Excel for the management
    Then the workbook has these sheets in order:
      | Sheet                          |
      | Summary                        |
      | Status                         |
      | What management must know now  |
      | Key figures                    |
      | Biggest gaps                   |
      | Open high risks                |
      | Details                        |
    And on "Status" the first row says "Regulation" "CRA" and "Coverage" "0 % (0 of 72)"
