Feature: Read user stories from GitHub

  # US-018: a project reads its user stories from a public GitHub repository.
  # The tracker only reads; the team keeps writing the stories on GitHub (B12).
  # GitHub is a pretend GitHub inside the test (FakeGitHub), so no network is used.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the GitHub repository "Zuvuz/compliance-tracker" has these issues:
      | number | title                                   | labels     | follows template | state |
      | 19     | [US-003] Create organisation via web UI | user-story | yes              | open  |
      | 40     | Fix typo                                | bug        | no               | open  |
      | 41     | [US-099] Draft without rules            | user-story | no               | open  |

  @US-018 @iso27001-A.8.26 @integration
  Scenario: Link a repository and read its user stories
    When I link "Acme Portal" to the repository "Zuvuz/compliance-tracker"
    And I click "Sync now"
    Then the project has the work item "[US-003] Create organisation via web UI"
    And the work item shows its rules, examples and Gherkin
    And the work item links to "https://github.com/Zuvuz/compliance-tracker/issues/19"
    And the page says when it was last synced

  @US-018 @integration
  Scenario: The repository must be written as owner/name
    When I link "Acme Portal" to the repository "compliance-tracker"
    Then I see the error "Enter the repository as owner/name, e.g. Zuvuz/compliance-tracker"

  @US-018 @iso27001-A.8.26 @integration
  Scenario: Issues without acceptance criteria are skipped
    Given "Acme Portal" is linked to "Zuvuz/compliance-tracker"
    When I click "Sync now"
    Then issue 41 is listed as skipped with "no acceptance criteria"
    And issue 40 is not read

  @US-018 @integration
  Scenario: The tracker only reads from GitHub
    Given "Acme Portal" is linked to "Zuvuz/compliance-tracker"
    When I click "Sync now"
    Then the tracker only sent read requests to GitHub

  @US-018 @integration
  Scenario: Closed issues are updated by themselves
    Given "Acme Portal" is linked to "Zuvuz/compliance-tracker" and synced
    When issue 19 is closed on GitHub
    And the scheduled check runs
    Then the work item "[US-003] Create organisation via web UI" is marked "Closed"

  @US-018 @integration
  Scenario: An issue that loses its label is kept and marked removed
    Given "Acme Portal" is linked to "Zuvuz/compliance-tracker" and synced
    When issue 19 no longer has the label "user-story"
    And I click "Sync now"
    Then the work item "[US-003] Create organisation via web UI" is marked "Removed"

  @US-018 @integration
  Scenario: A failed sync keeps the work items
    Given "Acme Portal" is linked to "Zuvuz/compliance-tracker" and synced
    When GitHub refuses requests because of its rate limit
    And I click "Sync now"
    Then I see "Sync failed: GitHub's limit for requests was reached. Try again later"
    And the project has the work item "[US-003] Create organisation via web UI"

  @US-018 @integration
  Scenario: A repository that does not exist
    Given "Acme Portal" is linked to "Zuvuz/does-not-exist"
    When I click "Sync now"
    Then I see "Sync failed: Repository not found or private: add a token"

  @US-018 @integration
  Scenario: An open page updates when a sync finds changes
    Given "Acme Portal" is linked to "Zuvuz/compliance-tracker" and synced
    And I have the project page open
    When a new issue with label "user-story" is added on GitHub and the scheduled check runs
    Then the new work item appears on the page without reloading

  @US-018 @iso27001-A.5.15 @integration
  Scenario: Other organisations cannot see the work items
    Given "Acme Portal" is linked to "Zuvuz/compliance-tracker" and synced
    And "Bo" has created the organisation "Beta ApS"
    When "Bo" opens the work items of "Acme Portal"
    Then the page is not found
    And "Bo" cannot follow the live updates of "Acme Portal"

  @US-018 @iso27001-A.8.15 @integration
  Scenario: Linking and syncing are logged
    When I link "Acme Portal" to the repository "Zuvuz/compliance-tracker"
    And I click "Sync now"
    Then the activity log for "Acme ApS" has "RepositoryConnected" and "WorkItemsSynced" for "Anna"

  @US-018 @iso27001-A.8.15 @integration
  Scenario: A scheduled sync is logged as the system
    Given "Acme Portal" is linked to "Zuvuz/compliance-tracker" and synced
    When issue 19 is closed on GitHub
    And the scheduled check runs
    Then the activity log for "Acme ApS" has a "WorkItemsSynced" entry by the system
