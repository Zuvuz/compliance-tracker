Feature: Each lifecycle phase has a checklist

  # US-047: six phases, each with a checklist of what must be in place before the next, for every regulation the project
  # follows and marked with the regulation. Some items are done from the project's data, others by a person. A project can
  # turn an item off with a reason and choose optional tests. Moving on with items missing is allowed, warned and logged.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-047 @cra-art13 @integration
  Scenario: The checklist shows each item with its regulation
    Given "Acme Portal" is in the phase "Verification"
    When I open the overview of "Acme Portal"
    Then the checklist lists "SBOM uploaded" for "CRA Annex I Part II(1)"
    And the checklist item "SBOM uploaded" is "Missing"

  @US-047 @cra-art13 @integration
  Scenario: An item is done from the project's data
    Given "Acme Portal" is in the phase "Verification"
    And I upload the CycloneDX SBOM "sbom.cdx.json" with 3 components for version "1.0.0"
    When I open the overview of "Acme Portal"
    Then the checklist item "SBOM uploaded" is "Done"

  @US-047 @cra-art13 @integration
  Scenario: An item is ticked by hand with a comment
    Given "Acme Portal" is in the phase "Design"
    When I tick the checklist item "Threat model made" of "Acme Portal" with the comment "STRIDE workshop 10-11"
    Then the checklist item "Threat model made" is "Done"
    And the activity log has "ChecklistItemChanged" saying "Threat model made (CRA Art. 13(3)): done: STRIDE workshop 10-11"

  @US-047 @iso27001-8.2 @integration
  Scenario: A phase change with items missing is warned about and logged
    Given "Acme Portal" is in the phase "Design"
    When I open the overview of "Acme Portal"
    Then the phase form warns that "Threat model made" is missing
    When I change the phase of "Acme Portal" to "Development"
    Then the activity log has "PhaseChangedWithItemsMissing" saying "Design → Development, missing: Essential requirements designed in"

  @US-047 @integration
  Scenario: An item can be turned off with a reason, and an optional test chosen
    Given "Acme Portal" is in the phase "Verification"
    When I turn off the checklist item "Coordinated vulnerability disclosure policy published" of "Acme Portal" because "Handled by the customer"
    And I add the optional test "Penetration test done" to the checklist of "Acme Portal"
    Then the checklist item "Coordinated vulnerability disclosure policy published" is "TurnedOff"
    And the checklist item "Penetration test done" is "Missing"

  @US-047 @integration
  Scenario: Items of a regulation the project does not follow are not shown
    Given "Acme Portal" is in the phase "Design"
    When I open the overview of "Acme Portal"
    Then the checklist does not list "Human oversight designed"
