Feature: Complete requirements from every framework

  # US-137: an EU act is imported from its official structured text on EUR-Lex (Formex), down to each point, and every
  # other part is left out with a reason. A standard is compared with its known list of codes. A version with such a
  # completeness check is activated only after a second person has approved it. EUR-Lex is a pretend one in the tests.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And "Bo" is in "Acme ApS" with the role "ComplianceManager"
    And I am signed in as "Anna"

  @US-137 @iso27001-A.5.31 @cra-13 @integration
  Scenario: An EU act is imported from EUR-Lex down to each point
    When I import "CRA" with the CELEX number "32024R2847" from EUR-Lex
    Then the draft version has the requirement "Annex I Part I (2)(a)"
    And the draft version has the requirement "Annex I Part I (2)(m)"
    And the draft version has the requirement "Art. 13(2)(b)"
    And the draft version has no requirement "Art. 13(2)"
    And the recitals are left out with the reason "Recital"
    And "Art. 3(1)" is left out with the reason "Definition"
    And "Art. 46(1)" is left out with the reason "An authority's duty"
    And "Art. 13(3)" is left out with the reason "Deleted"
    And the activity log has "FrameworkImportedFromEurLex" saying "EUR-Lex 32024R2847: 5 articles, 1 annex"

  @US-137 @iso27001-4.2 @integration
  Scenario: A standard missing a control is shown before activation
    When I upload "ISO/IEC 27001:2022" as a PDF without the control "A.5.23"
    Then the completeness check shows "92 of 93 controls found"
    And it shows "A.5.23 is missing"

  @US-137 @iso27001-A.8.32 @integration
  Scenario: Activation needs a second person
    Given I have imported "GDPR" with the CELEX number "32016R0679" from EUR-Lex
    When I activate the draft version
    Then the draft version waits for approval
    And the activity log has "FrameworkCompletenessChecked" saying "EUR-Lex 32016R0679"
    When "Bo" approves the activation
    Then the version is active
    And the activity log has "CatalogActivated" saying "approved by Bo"

  @US-137 @integration
  Scenario: The person who asked cannot approve their own activation
    Given I have imported "GDPR" with the CELEX number "32016R0679" from EUR-Lex
    When I activate the draft version
    Then I am not offered to approve it

  @US-137 @integration
  Scenario: An unknown CELEX number is refused
    When I import "Nothing" with the CELEX number "32099R9999" from EUR-Lex
    Then I see the error "EUR-Lex has no act with the CELEX number 32099R9999 in Formex"
