Feature: The local AI suggests mappings, and every decision is kept

  # US-020: the local AI suggests which requirements a user story covers; a person approves or rejects each one.
  # US-036: a few suggestions at a time, more on request, and every suggestion and decision is kept to improve the AI.
  # The AI is a pretend one (FakeAi) that gives the scores the scenario sets.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has the active requirements "SP-1 Users must sign in securely" and "SP-2 Backups"
    And the project "Acme Portal" exists
    And "Acme Portal" uses "Security policy" version 1
    And the project "Acme Portal" has the user story "[US-004] Sign in with OpenID Connect"

  @US-020 @iso27001-A.8.26 @integration
  Scenario: The AI suggests a mapping
    Given the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.86
    When I click "Suggest mappings" for "Acme Portal"
    Then I see the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect" with score "0.86" marked "AI-generated"
    And the rationale of "SP-1" for "[US-004] Sign in with OpenID Connect" becomes "Fake rationale for SP-1"

  @US-036 @integration
  Scenario: A few suggestions at a time, more on request, never the same twice
    Given the framework "Big policy" has 6 active requirements
    And "Acme Portal" uses "Big policy" version 1
    And the AI rates "[US-004] Sign in with OpenID Connect" against every requirement of "Big policy"
    When I click "Suggest mappings" for "Acme Portal"
    Then "[US-004] Sign in with OpenID Connect" has 4 suggestions, best first
    When I ask for more suggestions for "[US-004] Sign in with OpenID Connect"
    Then "[US-004] Sign in with OpenID Connect" has 8 suggestions with no requirement twice

  @US-020 @aiact-art14 @integration
  Scenario: Approving a suggestion creates a link
    Given the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.86
    And I have clicked "Suggest mappings" for "Acme Portal"
    When I approve the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    Then "[US-004] Sign in with OpenID Connect" is linked to "SP-1" with the source "AiApproved"
    And the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect" is no longer shown

  @US-020 @aiact-art14 @integration
  Scenario: Rejecting a suggestion needs a reason
    Given the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.86
    And I have clicked "Suggest mappings" for "Acme Portal"
    When I reject the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect" with the reason "" and the comment ""
    Then I see the error "Choose a reason"

  @US-020 @US-036 @iso27001-A.8.15 @integration
  Scenario: A rejection is kept with who, the reason and the comment
    Given the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.86
    And I have clicked "Suggest mappings" for "Acme Portal"
    When I reject the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect" with the reason "TooBroad" and the comment "Only about sign-in"
    Then the rejection of "SP-1" for "[US-004] Sign in with OpenID Connect" is kept with "Anna", "TooBroad" and "Only about sign-in"
    And "[US-004] Sign in with OpenID Connect" is not linked to "SP-1"

  @US-020 @integration
  Scenario: Decided pairs are not suggested again
    Given the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.9
    And I have clicked "Suggest mappings" for "Acme Portal"
    And I reject the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect" with the reason "NotRelevant" and the comment ""
    When I click "Suggest mappings" for "Acme Portal"
    Then "[US-004] Sign in with OpenID Connect" has no suggestion "SP-1"

  @US-036 @aiact-art12 @integration
  Scenario: Every suggestion keeps where it came from
    Given the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.86
    When I click "Suggest mappings" for "Acme Portal"
    Then each suggestion keeps its score, rank, model "fake-embed" and prompt version "mapping-v3"

  @US-043 @integration
  Scenario: Requirements that do not apply are not suggested
    Given the AI rates "[US-004] Sign in with OpenID Connect" against "SP-2" with 0.9
    And I mark "SP-2" as not applicable for "Acme Portal" with the reason "No backups in scope"
    When I click "Suggest mappings" for "Acme Portal"
    Then "[US-004] Sign in with OpenID Connect" has no suggestion "SP-2"

  @US-020 @integration
  Scenario: Without the local AI the page says so
    Given the local AI is not available
    When I click "Suggest mappings" for "Acme Portal"
    Then I see the error "The local AI is not available. Start Ollama (or the configured model server) and try again"

  @US-036 @iso27001-A.5.34 @integration
  Scenario: Nothing is exported while sharing is off
    When I export the AI feedback
    Then I see the error "Sharing is off"

  @US-036 @iso27001-A.5.34 @iso27001-A.8.15 @integration
  Scenario: The export has the decisions and no names
    Given the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.86
    And I have clicked "Suggest mappings" for "Acme Portal"
    And I reject the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect" with the reason "TooBroad" and the comment "Only about sign-in"
    When I switch on "Share our decisions to improve the AI"
    And I export the AI feedback
    Then the export has 1 line with the requirement, the user story, the score and the decision "Rejected"
    And the export does not say who decided
    And the activity log for "Acme ApS" has "AiFeedbackSharingChanged" and "AiFeedbackExported" for "Anna"

  @US-020 @iso27001-A.8.15 @integration
  Scenario: Decisions are logged
    Given the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.86
    And I have clicked "Suggest mappings" for "Acme Portal"
    When I approve the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    Then the activity log for "Acme ApS" has "MappingApproved" for "Anna" concerning "Acme Portal"
    And the "MappingApproved" entry says "SP-1 ↔ [US-004] Sign in with OpenID Connect"
