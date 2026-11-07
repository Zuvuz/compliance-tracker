Feature: AI decisions are shared by default
  US-130: organisations share their decisions on AI suggestions by default, so the system owner can train better models
  (US-123). They are told when they create the organisation and on the Settings page, can switch it off, and can leave a
  confidential project out. Only decisions made while sharing is on are used. Organisations cannot export for their own models.

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
    And I have approved the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"

  @US-130 @gdpr-art13 @integration
  Scenario: A new organisation shares and is told so before it is created
    Given I am signed in as "Bo"
    When I open the page to create an organisation
    Then I am told that AI decisions are shared by default
    And "Acme ApS" shares its AI decisions

  @US-130 @ai-act-art10 @integration
  Scenario: The Settings page shows how many decisions are shared, and there is no export
    When I open the Settings page
    Then the Settings page says "1 decisions shared"
    And there is no "Export AI feedback" button

  @US-130 @gdpr-art25 @iso27001-A.5.34 @integration
  Scenario: A confidential project is left out
    When I leave "Acme Portal" out of AI improvement
    Then the Edit page of "Acme Portal" says AI improvement is "Left out"
    And the Settings page says "0 decisions shared"

  @US-130 @gdpr-art21 @integration
  Scenario: Decisions made before sharing was switched on again are not used
    Given "Acme ApS" does not share its decisions
    When I switch on "Share our decisions to improve the AI"
    Then the Settings page says "0 decisions shared"
