Feature: Read user stories from Azure DevOps

  # US-030: a project reads its user stories from Azure Boards (dev.azure.com) with a read-only token, next to or instead
  # of GitHub. The token is stored encrypted and never shown again; the tracker never writes to Azure DevOps.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the Azure DevOps project "acme/Portal" accepts the token "valid-token" and has these work items:
      | id  | title              | type       | acceptance criteria                  | state  |
      | 120 | Sign in with MitID | User Story | <div>Given a citizen with MitID</div> | Active |
      | 121 | Crash on save      | Bug        |                                      | New    |
      | 122 | Export data        | User Story |                                      | New    |

  @US-030 @iso27001-A.8.26 @integration
  Scenario: Link Azure DevOps and read its user stories
    When I link "Acme Portal" to the Azure DevOps project "acme/Portal" with the token "valid-token"
    And I click "Sync now"
    Then the project has the work item "Sign in with MitID"
    And the Azure work item "Sign in with MitID" links to "https://dev.azure.com/acme/Portal/_workitems/edit/120"
    And the Azure work item "Sign in with MitID" has the acceptance criteria "Given a citizen with MitID"
    And Azure item 122 is listed as skipped with "no acceptance criteria"
    And Azure item 121 is not read
    And the tracker only sent read requests to Azure DevOps

  @US-030 @iso27001-A.5.17 @integration
  Scenario: A refused token is not saved
    When I link "Acme Portal" to the Azure DevOps project "acme/Portal" with the token "wrong-token"
    Then I see the error "The token was refused by Azure DevOps"
    And "Acme Portal" is not linked to Azure DevOps

  @US-030 @iso27001-A.8.24 @integration
  Scenario: The token is never shown again
    Given I have linked "Acme Portal" to the Azure DevOps project "acme/Portal" with the token "valid-token"
    When I open the Product requirements of "Acme Portal"
    Then the Azure DevOps link says "Token set"
    And the page does not contain "valid-token"
    And the stored Azure DevOps token is encrypted, without "valid-token"

  @US-030 @integration
  Scenario: A refused token during sync keeps the work items
    Given I have linked "Acme Portal" to the Azure DevOps project "acme/Portal" with the token "valid-token"
    And I click "Sync now"
    When Azure DevOps stops accepting the token for "acme/Portal"
    And I click "Sync now"
    Then the Azure sync says "Sync failed: The token was refused by Azure DevOps"
    And the project has the work item "Sign in with MitID"

  @US-030 @iso27001-A.5.15 @integration
  Scenario: Other organisations cannot see the Azure work items
    Given I have linked "Acme Portal" to the Azure DevOps project "acme/Portal" with the token "valid-token"
    And I click "Sync now"
    And "Bo" has created the organisation "Beta ApS"
    When "Bo" opens the work items of "Acme Portal"
    Then the page is not found

  @US-030 @iso27001-A.8.15 @integration
  Scenario: Linking, token changes and syncs are logged without the token
    When I link "Acme Portal" to the Azure DevOps project "acme/Portal" with the token "valid-token"
    And I click "Sync now"
    Then the activity log has "AzureProjectConnected" saying "acme/Portal"
    And the activity log has "AccessTokenChanged" saying "Azure DevOps token set"
    And the activity log has "WorkItemsSynced" saying "Azure DevOps: 1 added"
    And no activity log entry contains "valid-token"

  @US-030 @integration
  Scenario: A project can use GitHub and Azure DevOps at the same time
    Given the GitHub repository "Zuvuz/compliance-tracker" has these issues:
      | number | title                                   | labels     | follows template | state |
      | 3      | [US-003] Create organisation via web UI | user-story | yes              | open  |
    And "Acme Portal" is linked to "Zuvuz/compliance-tracker"
    When I link "Acme Portal" to the Azure DevOps project "acme/Portal" with the token "valid-token"
    And I click "Sync now"
    Then the work item "[US-003] Create organisation via web UI" has the source "GitHub"
    And the work item "Sign in with MitID" has the source "AzureDevOps"
