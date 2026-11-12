Feature: Statement of Applicability for ISO/IEC 27001 Annex A

  # US-053: the organisation's Statement of Applicability, on the organisation's Reports tab. It uses the organisation's
  # own uploaded ISO/IEC 27001 (ISO's text is not built in). A control is not applicable only when every project that
  # follows the standard marks it so, and how it is implemented is worked out from coverage and evidence.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the framework "ISO/IEC 27001:2022" has the active requirements "A.5.15 Access control" and "A.8.24 Use of cryptography"
    And "Acme Portal" uses "ISO/IEC 27001:2022"

  @US-053 @iso27001-6.1.3 @integration
  Scenario: Every control is listed in the standard's order
    When I download the Statement of Applicability as CSV
    Then the Statement of Applicability lists "A.5.15" and "A.8.24" in that order
    And the control "A.5.15" is "Yes" and "Not implemented"

  @US-053 @iso27001-6.1.3 @integration
  Scenario: A control covered by a story without evidence is partly implemented
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "A.5.15"
    When I download the Statement of Applicability as CSV
    Then the control "A.5.15" is "Yes" and "Partly implemented"
    And the control "A.5.15" is met by "Acme Portal: [US-004] Sign in"

  @US-053 @iso27001-6.1.3 @integration
  Scenario: A covered control with evidence is implemented
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "A.5.15"
    And I have added the evidence "Access review" with the file "review.pdf" to "A.5.15"
    When I download the Statement of Applicability as CSV
    Then the control "A.5.15" is "Yes" and "Implemented"

  @US-053 @iso27001-6.1.3 @integration
  Scenario: A control is not applicable only when every project says so
    Given the project "Acme App" exists
    And "Acme App" uses "ISO/IEC 27001:2022"
    And I mark "A.8.24" as not applicable for "Acme Portal" with the reason "No cryptography in the portal"
    When I download the Statement of Applicability as CSV
    Then the control "A.8.24" is "Yes" and "Not implemented"
    And the control "A.8.24" has the reason "Acme Portal: No cryptography in the portal"
    When I mark "A.8.24" as not applicable for "Acme App" with the reason "The app stores nothing"
    And I download the Statement of Applicability as CSV
    Then the control "A.8.24" is "No" and "Not applicable"

  @US-053 @iso27001-6.1.3 @integration
  Scenario: The Statement of Applicability as PDF
    When I download the Statement of Applicability as PDF
    Then the PDF contains "Statement of Applicability"
    And the PDF contains "ISO/IEC 27001:2022"
