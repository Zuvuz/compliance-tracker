Feature: All of the ISMS in one place, in a logical order

  # US-226: the organisation-wide parts of the ISMS are under the ISMS item, in the order of ISO/IEC 27001 and the guided
  # start: the documents (policies first), the organisation's risks and the suppliers too. The checklists stay under
  # Compliance.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-226 @iso27001-4.4 @integration
  Scenario: The ISMS row follows the standard's order
    When I open "ISMS" from the top bar
    Then the ISMS row is "Overview", "Scope", "Documents", "Risks", "Assets", "Suppliers", "Objectives", "Competence", "Yearly plan", "Internal audits", "Management review", "Nonconformities"

  @US-226 @iso27001-5.2 @integration
  Scenario: Policies, risks and suppliers are part of the ISMS
    When I open the ISMS page "Documents"
    Then the top bar marks "ISMS"
    When I open the ISMS page "Risks"
    Then the top bar marks "ISMS"
    And the ISMS page "Risks" is marked
    When I open the ISMS page "Suppliers"
    Then the top bar marks "ISMS"

  @US-226 @iso27001-5.2 @integration
  Scenario: The documents show the policies first
    Given I upload the policy "Access control policy" version "1" for the organisation
    When I open the ISMS page "Documents"
    Then the document filter "Policies" is chosen
    And the documents list "Access control policy"
