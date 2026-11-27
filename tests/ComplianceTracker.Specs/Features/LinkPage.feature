Feature: One Link page for requirements, product requirements and evidence

  # US-220: a "Link" tab in each project. A compliance requirement is chosen on the left; the middle column shows its
  # product requirements and the right column its evidence: what is linked first, then the AI's suggestions, then the
  # rest. Link and Unlink are one click; an AI suggestion goes through the double confirmation (US-221). The page opens on
  # the filter "Not covered". The AI is the pretend one.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has the active requirements "SP-1 Users must sign in securely" and "SP-2 Backups"
    And the project "Acme Portal" exists
    And "Acme Portal" uses "Security policy" version 1
    And the project "Acme Portal" has the user story "[US-004] Sign in with OpenID Connect"
    And the project "Acme Portal" has the user story "[US-007] Lock the account after 5 attempts"

  @US-220 @iso27001-5.31 @integration
  Scenario: Choosing a requirement shows what is linked and what is suggested
    Given the user story "[US-007] Lock the account after 5 attempts" is linked to the requirement "SP-1"
    And the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.86
    And I have clicked "Suggest mappings" for "Acme Portal"
    When I open the Link page of "Acme Portal" and choose "SP-1"
    Then the product requirements column shows "[US-007] Lock the account after 5 attempts" as "linked"
    And the product requirements column shows "[US-004] Sign in with OpenID Connect" as "suggested"

  @US-220 @iso27001-5.31 @integration
  Scenario: Linking a product requirement from the middle column changes the requirement's mark
    When I open the Link page of "Acme Portal" and choose "SP-2"
    And I link the product requirement "[US-004] Sign in with OpenID Connect" on the Link page
    Then "[US-004] Sign in with OpenID Connect" is linked to "SP-2" with the source "Manual"
    And the Link page still has "SP-2" chosen
    And the product requirements column shows "[US-004] Sign in with OpenID Connect" as "linked"
    And the requirement "SP-2" is marked "partly" on the Link page

  @US-220 @cra-annex7 @integration
  Scenario: Linking evidence from the Link page
    Given I have added the evidence "Backup report" with the link "https://example.org/backup" to "SP-2"
    When I open the Link page of "Acme Portal" and choose "SP-1"
    And I link the evidence "Backup report" on the Link page
    Then the requirement "SP-1" has the evidence "Backup report"
    And the evidence column shows "Backup report" as "linked"

  @US-220 @aiact-art14 @integration
  Scenario: An AI suggestion is linked only after the double confirmation, and the page comes back
    Given the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.86
    And I have clicked "Suggest mappings" for "Acme Portal"
    When I open the Link page of "Acme Portal" and choose "SP-1"
    And I click "Link" on the suggested product requirement "[US-004] Sign in with OpenID Connect"
    Then "[US-004] Sign in with OpenID Connect" is not linked to "SP-1"
    When I confirm that I have read the argument
    Then "[US-004] Sign in with OpenID Connect" is linked to "SP-1" with the source "AiApproved"
    And the Link page still has "SP-1" chosen

  @US-228 @usability @integration
  Scenario: A rule broken on the Link page shows its message on the Link page
    Given the AI rates "[US-004] Sign in with OpenID Connect" against "SP-1" with 0.86
    And I have clicked "Suggest mappings" for "Acme Portal"
    When I open the Link page of "Acme Portal" and choose "SP-1"
    And I reject the suggested product requirement "[US-004] Sign in with OpenID Connect" without a reason
    Then I see the error "Choose a reason"
    And the Link page still has "SP-1" chosen

  @US-220 @usability @integration
  Scenario: The page opens with only the requirements that are not covered
    Given the user story "[US-007] Lock the account after 5 attempts" is linked to the requirement "SP-1"
    When I open the Link page of "Acme Portal"
    Then the Link page's filter "not-covered" is chosen
    And every requirement shown on the Link page is not covered
    And the requirement "SP-1" is not shown on the Link page
