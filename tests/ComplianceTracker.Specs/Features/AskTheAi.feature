Feature: Ask the AI about one requirement, evidence, story or risk

  # US-106: "Ask the AI" on a requirement looks only at that requirement: the project's stories and evidence that meet it,
  # and when nothing does, a draft user story a person can change and accept. "Suggest measures" on a risk suggests
  # requirements and stories that could reduce it. The AI only suggests; a person decides.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-106 @aiact-art14 @integration
  Scenario: Matching stories are suggested for one requirement
    Given the project "Acme Portal" has the user story "[US-004] Sign in with Keycloak"
    And the AI rates "[US-004] Sign in with Keycloak" against "I.2(d)" with 0.82
    When I ask the AI for suggestions for "I.2(d)"
    Then the row "I.2(d)" suggests the story "[US-004] Sign in with Keycloak"
    And the activity log has "RequirementSuggestionsAsked" saying "I.2(d): 1 stories, 0 evidence"

  @US-106 @iso27001-A.5.37 @integration
  Scenario: Matching evidence is suggested for one requirement
    Given the evidence "Access policy" is on the Evidence tab of "Acme Portal"
    And the AI rates "Access policy" against "I.2(d)" with 0.77
    When I ask the AI for suggestions for "I.2(d)"
    Then the row "I.2(d)" suggests the evidence "Access policy"
    When I approve the suggested evidence "Access policy" on the row "I.2(d)"
    Then the row "I.2(d)" has the evidence "Access policy"

  @US-106 @cra-annexI @integration
  Scenario: With nothing matching, the AI drafts a user story
    When I ask the AI for suggestions for "I.2(m)"
    Then the row "I.2(m)" has a draft user story "Meet I.2(m)" marked "AI-generated"

  @US-106 @aiact-art14 @integration
  Scenario: A draft becomes a story only when a person creates it
    Given the project "Acme Portal" has the user story "[US-001] Something else"
    And I asked the AI for suggestions for "I.2(m)"
    When I change the draft's rules to "R1: Every account can be deleted" and create the story
    Then the activity log has "StoryDraftAccepted" saying "I.2(m): Meet I.2(m) (edited)"
    And the page offers to create the issue in GitHub
    When the issue "Meet I.2(m)" comes in with the next sync
    Then "I.2(m)" is covered by the story "Meet I.2(m)"

  @US-106 @aiact-art14 @integration
  Scenario: A draft can be discarded with a reason
    Given I asked the AI for suggestions for "I.2(m)"
    When I discard the draft because "We do not store accounts"
    Then the row "I.2(m)" has no draft
    And the activity log has "StoryDraftDiscarded" saying "We do not store accounts"

  @US-106 @iso27001-A.5.15 @integration
  Scenario: An auditor cannot ask the AI
    Given "Bo" is in "Acme ApS" with the role "Auditor"
    And I am signed in as "Bo"
    When I open the "CRA" sub-tab of "Acme Portal"
    Then there is no "Ask the AI" button

  @US-106 @cra-13 @integration
  Scenario: Measures are suggested for one risk
    Given the risk "No secure default config" exists in "Acme Portal"
    And the AI rates "No secure default config" against "I.2(b)" with 0.74
    When I ask the AI to suggest measures for the risk "No secure default config"
    And I approve the suggested measure "I.2(b)"
    Then the risk "No secure default config" has the measure "I.2(b)"
