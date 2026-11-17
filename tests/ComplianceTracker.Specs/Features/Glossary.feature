Feature: Short explanations of the terms

  # US-173: a "?" after a term opens a one-line explanation with its source; without JavaScript it links to the
  # glossary on the Help page.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the risk "Account takeover" exists in "Acme Portal"

  @US-173 @integration
  Scenario: A term on the risk page explains itself and links to the glossary
    When I open the risk "Account takeover"
    Then "Residual risk" has a "?" that says "The risk that is left after the measures" and names "ISO/IEC 27001 6.1.3 f"
    When I follow the "?" of "Residual risk"
    Then the Help page explains "Residual risk" with its source
