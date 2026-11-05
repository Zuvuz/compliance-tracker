Feature: Limits for the AI's suggestions

  # US-105: a suggestion below a minimum score is not made, so people are not shown noise. Story and evidence
  # suggestions have their own limit, and every suggestion keeps the limit it was made with. The AI is the pretend one.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has the active requirements "SP-1 Users must sign in securely" and "SP-2 Backups"
    And the project "Acme Portal" exists
    And "Acme Portal" uses "Security policy" version 1

  @US-105 @aiact-art14 @integration
  Scenario: A weak evidence suggestion is not shown
    Given the minimum score for evidence suggestions is 0.55
    And the AI rates "Access policy 2026" against "SP-1" with 0.46
    When I upload the evidence "Access policy 2026" with the file "access-policy.pdf" on the Evidence tab of "Acme Portal"
    Then the Evidence tab says "No requirement looks like a match; link it by hand."
    And "Access policy 2026" has no evidence suggestion "SP-1"

  @US-105 @aiact-art14 @integration
  Scenario: A good evidence suggestion is shown with the limit
    Given the minimum score for evidence suggestions is 0.55
    And the AI rates "Access policy 2026" against "SP-1" with 0.81
    When I upload the evidence "Access policy 2026" with the file "access-policy.pdf" on the Evidence tab of "Acme Portal"
    Then "Access policy 2026" has the suggestion "SP-1" with score "0.81" marked "AI-generated"
    And the Evidence tab says "Only suggestions with a score of 0.55 or more are shown."

  @US-105 @aiact-art14 @integration
  Scenario: A weak story suggestion is not made
    Given the project "Acme Portal" has the user story "[US-004] Sign in with OpenID Connect"
    And the minimum score for story suggestions is 0.5
    And the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.86
    And the AI rates "[US-004] Sign in with OpenID Connect" against "SP-2" with 0.30
    When I click "Suggest mappings" for "Acme Portal"
    Then I see the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect" with score "0.86" marked "AI-generated"
    And "[US-004] Sign in with OpenID Connect" has no suggestion "SP-2"

  @US-105 @aiact-art12 @integration
  Scenario: A suggestion keeps the limit it was made with
    Given the minimum score for evidence suggestions is 0.55
    And the AI rates "Access policy 2026" against "SP-1" with 0.81
    When I upload the evidence "Access policy 2026" with the file "access-policy.pdf" on the Evidence tab of "Acme Portal"
    Then the suggestion "SP-1" for "Access policy 2026" was made with the limit 0.55
