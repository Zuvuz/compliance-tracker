Feature: The lifecycle phase card leads to the next phase

  # #52 (US-024, US-047, US-071): the card names the next phase on its main button, with what is left before it; another
  # phase is under "Other phase…". The checklist is in the card, and an item can be proven with the project's evidence.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-024 @US-047 @integration
  Scenario: The main button moves to the next phase and says what is left
    Given "Acme Portal" is in the phase "Design"
    When I open the overview of "Acme Portal"
    Then the phase card's main button says "Move to Development" with items left
    And the phase card offers the other phases "Planning, Development, Verification, Released, EndOfSupport"
    And the phase card hides the fields for "Released" until it is chosen

  @US-024 @integration
  Scenario: Released's fields show when Released is next
    Given "Acme Portal" is in the phase "Verification"
    When I open the overview of "Acme Portal"
    Then the phase card shows the fields for "Released"

  @US-047 @cra-art13 @integration
  Scenario: A checklist item is proven with the project's evidence
    Given "Acme Portal" is in the phase "Design"
    And I have added the evidence "Threat model v1" with the link "https://example.com/threat-model" to "I.2(d)"
    When I tick the checklist item "Threat model made" of "Acme Portal" with the project's evidence "Threat model v1"
    Then the checklist item "Threat model made" is "Done"
    And the checklist item "Threat model made" links to the evidence "Threat model v1"
