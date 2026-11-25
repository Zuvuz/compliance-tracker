Feature: Double confirmation and a clearer argument for AI suggestions

  # US-221: approving an AI suggestion that makes a link (requirement and story, evidence and requirement, evidence and
  # story, risk measure) first shows the AI's argument in five parts; the link is made only when the person ticks "I have
  # read the argument and agree" and confirms. A missing quote is said plainly. The AI is the pretend one.

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

  @US-221 @aiact-art14 @integration
  Scenario: The argument is shown before anything is linked
    When I click "Approve" on the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    Then the argument says the requirement asks "SP-1: Users must sign in securely."
    And the argument says "No quote found: the AI's judgement only"
    And the argument's confidence is "Low"
    And "[US-004] Sign in with OpenID Connect" is not linked to "SP-1"

  @US-221 @aiact-art14 @integration
  Scenario: The link is made after the second confirmation
    When I click "Approve" on the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    And I confirm that I have read the argument
    Then "[US-004] Sign in with OpenID Connect" is linked to "SP-1" with the source "AiApproved"
    And the confirmation of the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect" is kept

  @US-221 @aiact-art14 @integration
  Scenario: Cancelling changes nothing
    When I click "Approve" on the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    And I cancel the confirmation
    Then "[US-004] Sign in with OpenID Connect" is not linked to "SP-1"
    And the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect" still waits

  @US-221 @aiact-art14 @integration
  Scenario: Approving without the tick is refused
    When I send the approval of "SP-1" for "[US-004] Sign in with OpenID Connect" without the tick
    Then I am shown the argument instead
    And "[US-004] Sign in with OpenID Connect" is not linked to "SP-1"

  @US-221 @integration
  Scenario: A suggestion decided by someone else says who
    Given I reject the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect" with the reason "NotRelevant" and the comment ""
    When I open the confirmation of the suggestion "SP-1" for "[US-004] Sign in with OpenID Connect"
    Then the confirmation says it was rejected by "Anna"
