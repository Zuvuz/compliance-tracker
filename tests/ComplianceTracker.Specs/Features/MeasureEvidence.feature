Feature: Approve a measure with its evidence

  # US-158: when the AI suggests a measure for a risk, a person can approve it with a link or a file as evidence in one
  # step. The evidence goes on the measure's requirement. Refused evidence approves nothing, and an approved measure
  # whose requirement has no evidence offers "Add evidence".

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the risk "No secure default config" exists in "Acme Portal"
    And the AI rates "No secure default config" against "I.2(b)" with 0.74

  @US-158 @iso27001-6.1.3 @integration
  Scenario: The measure and its evidence in one step
    When I ask the AI to suggest measures for the risk "No secure default config"
    And I approve the suggested measure "I.2(b)" with the evidence "Hardening guide" and the link "https://example.com/hardening"
    Then the risk "No secure default config" has the measure "I.2(b)"
    And the measure "I.2(b)" of the risk "No secure default config" does not offer "Add evidence"
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(b)" shows the evidence "Hardening guide"

  @US-158 @integration
  Scenario: The evidence can be a file
    When I ask the AI to suggest measures for the risk "No secure default config"
    And I approve the suggested measure "I.2(b)" with the evidence file "hardening.pdf"
    And I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(b)" shows the evidence "hardening.pdf"

  @US-158 @integration
  Scenario: Refused evidence approves nothing
    When I ask the AI to suggest measures for the risk "No secure default config"
    And I approve the suggested measure "I.2(b)" with both a link and a file
    Then I see the error "Give a link or a file, not both"
    And the risk "No secure default config" has no measure "I.2(b)"

  @US-158 @integration
  Scenario: Without evidence the approval works as before and offers to add it
    When I ask the AI to suggest measures for the risk "No secure default config"
    And I approve the suggested measure "I.2(b)"
    Then the risk "No secure default config" has the measure "I.2(b)"
    And the measure "I.2(b)" of the risk "No secure default config" offers "Add evidence" on the page of "I.2(b)"
