Feature: Evidence suggested when approving a measure

  # US-161: when a person approves a measure the AI suggested for a risk, the AI suggests which of the project's existing
  # evidence proves it, best first. Picking one links it to the measure's requirement and approves the measure in one step.
  # The AI is the pretend one.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the minimum score for evidence suggestions is 0.5
    And the risk "No secure default config" exists in "Acme Portal"
    And the AI rates "No secure default config" against "I.2(b)" with 0.74
    And the AI rates "Supplier policy v2" against "I.2(b)" with 0.71
    And I upload the evidence "Supplier policy v2" with the file "supplier-policy.pdf" on the Evidence tab of "Acme Portal"

  @US-161 @iso27001-6.1.3 @integration
  Scenario: Existing evidence is suggested for the measure
    When I ask the AI to suggest measures for the risk "No secure default config"
    Then the suggested measure "I.2(b)" suggests the evidence "Supplier policy v2" marked "AI-generated" with the score "0.71"

  @US-161 @iso27001-8.3 @integration
  Scenario: Approving with a suggested evidence links it
    When I ask the AI to suggest measures for the risk "No secure default config"
    And I approve the suggested measure "I.2(b)" with the suggested evidence "Supplier policy v2"
    Then the risk "No secure default config" has the measure "I.2(b)"
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(b)" shows the evidence "Supplier policy v2"

  @US-161 @integration
  Scenario: Evidence already on the requirement is not suggested
    Given I have added the evidence "Hardening guide" with the link "https://example.com/hardening" to "I.2(b)"
    And the AI rates "Hardening guide" against "I.2(b)" with 0.9
    When I ask the AI to suggest measures for the risk "No secure default config"
    Then the suggested measure "I.2(b)" does not suggest the evidence "Hardening guide"
    And the suggested measure "I.2(b)" suggests the evidence "Supplier policy v2" marked "AI-generated" with the score "0.71"

  @US-161 @integration
  Scenario: Without the AI the measure can still be approved with new evidence
    When I ask the AI to suggest measures for the risk "No secure default config"
    And the local AI stops answering
    And I open the risk "No secure default config"
    Then the suggested measure "I.2(b)" says "No suggestions: the AI is not available"
    When I approve the suggested measure "I.2(b)" with the evidence "Hardening guide" and the link "https://example.com/hardening"
    Then the risk "No secure default config" has the measure "I.2(b)"
