Feature: Standards in every form and structure

  # US-143: the organisation uploads its own licensed copy of a standard as PDF, Word, Excel, CSV or HTML. The structure is
  # kept (clauses, lettered points, annexes, quality models), each item gets a kind, and recommendations ("should") are
  # shown apart and never lower the coverage. The test files are made up and copy only the layout of ISO standards.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-143 @iso27001-4.2 @iso27001-7.5.3 @integration
  Scenario: A licensed ISO PDF keeps its clauses and Annex A
    When I upload the framework "ISO/IEC 27001" from a PDF with clauses 4 to 10 and the controls A.5.1 to A.8.34
    Then the draft has the requirement "6.1.2 a)" under "6.1.2"
    And the draft has 93 requirements under "Annex A"
    And no requirement contains "Licensed to"
    And the draft has no requirement "3.1"
    And the draft has no requirement "B.1"
    And the activity log has "CatalogImported" saying "Pdf: "

  @US-143 @iso27001-A.5.31 @integration
  Scenario: Recommendations are flagged and do not lower the coverage
    Given the framework "ISO/IEC 27002" from a PDF where "5.1" says "should" is active
    And the project "Acme Portal" exists
    And "Acme Portal" uses "ISO/IEC 27002"
    When I open the Compliance tab of "Acme Portal"
    Then the requirement "5.1" shows "Recommended"
    And the coverage says "0 of 1 requirements covered (0 %) · 0 of 1 recommendations followed"

  @US-143 @integration
  Scenario: A quality model keeps characteristic and subcharacteristic
    When I upload the framework "ISO/IEC 25010" from a Word file
    Then the draft has the characteristic "Testability" under "Maintainability"

  @US-143 @iso27001-A.5.31 @integration
  Scenario: A control list from Excel
    When I upload the framework "ISO/IEC 27001" from an Excel file with the columns "Control", "Name" and "Description"
    And I choose "Control" as the code, "Name" as the title and "Description" as the text
    Then the draft has 93 requirements
    And the next version of "ISO/IEC 27001" from Excel is read with the same columns

  @US-143 @integration
  Scenario: A scanned PDF is refused
    When I upload the framework "ISO/IEC 27001" from a PDF without text
    Then I see the error "This PDF has no text. Upload a PDF with text, or the standard as Word or Excel"
