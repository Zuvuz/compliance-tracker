Feature: Measure the AI's quality on the organisations' own decisions
  US-116: the operator measures each AI step against the suggestions people approved and rejected, in the
  organisations that share their decisions (US-036), and compares runs before changing a model (US-117).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has the active requirements "SP-1 Users must sign in securely" and "SP-2 Backups"
    And the project "Acme Portal" exists
    And "Acme Portal" uses "Security policy" version 1
    And the project "Acme Portal" has the user story "[US-004] Sign in with OpenID Connect"
    And the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.86
    And I have clicked "Suggest mappings" for "Acme Portal"
    And the rationale of "SP-1" for "[US-004] Sign in with OpenID Connect" has been written

  @US-116 @ai-act-art15 @integration
  Scenario: Measure story matching
    Given I have approved the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    And "Acme ApS" shares its decisions to improve the AI
    And I am signed in as the system admin "Sys"
    When I measure story matching
    Then the newest story matching run has 1 decision, top 1 "100 %" and the model "fake-embed"

  @US-116 @ai-act-art15 @integration
  Scenario: Too few decisions give no measurement
    Given I have approved the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    And "Acme ApS" shares its decisions to improve the AI
    And a measurement needs at least 5 decisions
    And I am signed in as the system admin "Sys"
    When I measure story matching
    Then I see the error "Too few decisions to measure: 1 of at least 5"

  @US-116 @iso27001-A.5.34 @integration
  Scenario: Decisions that are not shared are not used
    Given I have approved the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    And I am signed in as the system admin "Sys"
    When I measure story matching
    Then I see the error "Too few decisions to measure: 0 of at least 1"

  @US-116 @iso27001-A.8.32 @integration
  Scenario: Compare two models
    Given I have approved the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    And "Acme ApS" shares its decisions to improve the AI
    And I am signed in as the system admin "Sys"
    And I have measured story matching
    And the AI task "Embedding" has its own model "big-embed"
    When I measure story matching
    Then the comparison says "fake-embed → big-embed"
    And the comparison says "Top 1: 100 → 100 (the same)"

  @US-116 @ai-act-art15 @integration
  Scenario: A person grades the rationales
    Given I have approved the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    And "Acme ApS" shares its decisions to improve the AI
    And I am signed in as the system admin "Sys"
    When I grade the rationale of "SP-1" with 4
    Then the newest rationale run has the grade "4.0 of 5 (1)" and the model "fake-chat"
