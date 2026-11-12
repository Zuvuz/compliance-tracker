Feature: Statement of Applicability without ISO/IEC 27001

  # US-053 R6: an organisation that has not uploaded ISO/IEC 27001 is told what is needed

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-053 @integration
  Scenario: Without ISO/IEC 27001 the report says so
    When I open the organisation's Reports tab
    Then the "Statement of Applicability" report cannot be made because "Upload ISO/IEC 27001 as a framework and follow it to make the Statement of Applicability"
