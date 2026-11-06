Feature: A model for each AI task
  US-117: each AI task (search, ranking, rationale, evidence check, drafts …) can have its own model in the settings,
  so the many small calls stay fast while the tasks that write text for people can use a larger model. A task without
  its own model uses the default one, and a task whose model cannot be reached says so.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has the active requirements "SP-1 Users must sign in securely" and "SP-2 Backups"
    And the project "Acme Portal" exists
    And "Acme Portal" uses "Security policy" version 1
    And the project "Acme Portal" has the user story "[US-004] Sign in with OpenID Connect"
    And the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.86

  @US-117 @ai-act-art12 @integration
  Scenario: A task uses its own model
    Given the AI task "Rationale" has its own model "large-chat"
    When I click "Suggest mappings" for "Acme Portal"
    Then the rationale of "SP-1" for "[US-004] Sign in with OpenID Connect" becomes "Fake rationale for SP-1"
    And the rationale of "SP-1" was written by "large-chat"

  @US-117 @integration
  Scenario: A task without its own model uses the default
    When I click "Suggest mappings" for "Acme Portal"
    Then the rationale of "SP-1" for "[US-004] Sign in with OpenID Connect" becomes "Fake rationale for SP-1"
    And the rationale of "SP-1" was written by "fake-chat"

  @US-117 @integration
  Scenario: A task whose model cannot be reached says so
    Given the AI task "Embedding" has its own model "big-embed" that cannot be reached
    When I click "Suggest mappings" for "Acme Portal"
    Then I see the error "Embedding is unavailable: the model big-embed cannot be reached"

  @US-117 @ai-act-art12
  Scenario: The settings choose the model of each task
    Given the settings give the AI task "Rationale" the model "qwen2.5:14b"
    Then the AI task "Rationale" uses "qwen2.5:14b" from its own setting
    And the AI task "EvidenceCheck" uses "fake-chat" by default
    And the AI task "Embedding" uses "fake-embed" by default

  @US-117 @integration
  Scenario: The system admin sees the model of each task
    Given the AI task "GapDrafts" has its own model "large-chat"
    And I am signed in as the system admin "Sys"
    When I open "/System"
    Then the AI models list "Gap drafts" with "large-chat" from its own setting
    And the AI models list "Rationale" with "fake-chat" by default
