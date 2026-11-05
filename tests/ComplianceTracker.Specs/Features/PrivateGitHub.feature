Feature: Read user stories from a private GitHub repository

  # US-031: a private repository is read with a read-only token.
  # The token is checked with GitHub, stored encrypted and never shown again.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the private GitHub repository "acme/portal" accepts the token "valid-token" and has the issue "[US-001] Sign in"

  @US-031 @iso27001-A.8.26 @integration
  Scenario: A private repository is read with a token
    Given "Acme Portal" is linked to "acme/portal"
    When I add the GitHub token "valid-token" to "Acme Portal"
    And I click "Sync now"
    Then the project has the work item "[US-001] Sign in"

  @US-031 @integration
  Scenario: A private repository without a token asks for one
    Given "Acme Portal" is linked to "acme/portal"
    When I click "Sync now"
    Then I see "Sync failed: Repository not found or private: add a token"

  @US-031 @iso27001-A.5.17 @integration
  Scenario: A refused token is not saved
    Given "Acme Portal" is linked to "acme/portal"
    When I add the GitHub token "wrong-token" to "Acme Portal"
    Then I see the error "The token was refused by GitHub"
    And "Acme Portal" has no GitHub token

  @US-031 @iso27001-A.8.24 @integration
  Scenario: The token is stored encrypted and never shown again
    Given "Acme Portal" is linked to "acme/portal"
    When I add the GitHub token "valid-token" to "Acme Portal"
    Then I see "Token set" on the page of "Acme Portal"
    And the page does not contain "valid-token"
    And the database does not contain "valid-token" in plain text

  @US-031 @iso27001-A.5.17 @integration
  Scenario: A revoked token stops the sync and keeps the work items
    Given "Acme Portal" is linked to "acme/portal"
    And I add the GitHub token "valid-token" to "Acme Portal"
    And I click "Sync now"
    When the token is revoked on GitHub
    And I click "Sync now"
    Then I see "Sync failed: The token was refused by GitHub"
    And the project has the work item "[US-001] Sign in"

  @US-031 @iso27001-A.5.17 @integration
  Scenario: The token can be removed
    Given "Acme Portal" is linked to "acme/portal"
    And I add the GitHub token "valid-token" to "Acme Portal"
    When I remove the GitHub token of "Acme Portal"
    Then "Acme Portal" has no GitHub token

  @US-031 @iso27001-A.8.15 @integration
  Scenario: Token changes are logged without the token
    Given "Acme Portal" is linked to "acme/portal"
    When I add the GitHub token "valid-token" to "Acme Portal"
    Then the activity log for "Acme ApS" has "AccessTokenChanged" for "Anna" concerning "Acme Portal"
    And no activity log entry contains "valid-token"
