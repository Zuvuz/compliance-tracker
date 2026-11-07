Feature: The system owner exports training data
  US-123: the system owner exports the AI decisions of the organisations that share them (US-130) as JSON Lines, to
  train or tune a model outside the tracker. The models never change by themselves (AI Act Art. 10, 12, 15). No names
  or e-mails leave; organisations and projects are numbered (GDPR Art. 5(1)(c), 25; ISO/IEC 27001 A.5.34, A.8.11).

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

  @US-123 @ai-act-art10 @gdpr-art25 @integration
  Scenario: Only shared decisions are exported, without names
    Given I have approved the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    And "Acme ApS" shares its decisions to improve the AI
    And I am signed in as the system admin "Sys"
    When I export the training data
    Then the file has 1 line
    And the line is a "story" decision "approved" from "org-1" and "org-1/project-1"
    And the line has the story "[US-004] Sign in with OpenID Connect" and the model "fake-embed"
    And no line contains "Anna" or an e-mail
    And the log of "Acme ApS" says the system owner exported 1 decision

  @US-123 @iso27001-A.5.34 @integration
  Scenario: An organisation that does not share is left out
    Given I have approved the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    And "Acme ApS" does not share its decisions
    And I am signed in as the system admin "Sys"
    When I export the training data
    Then the file has 0 lines

  @US-123 @integration
  Scenario: The export can be limited to a period and to kinds
    Given I have approved the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    And "Acme ApS" shares its decisions to improve the AI
    And I am signed in as the system admin "Sys"
    When I export the training data until yesterday
    Then the file has 0 lines
    When I export the training data of the kind "not-applicable"
    Then the file has 0 lines
    When I export the training data of the kind "story"
    Then the file has 1 line

  @US-123 @ai-act-art14 @integration
  Scenario: An organisation admin cannot export
    When I post the training data export directly
    Then access is denied
