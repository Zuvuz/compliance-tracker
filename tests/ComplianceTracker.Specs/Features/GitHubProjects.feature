Feature: Link a project to one GitHub Project of an organisation

  # US-107: a tracker project can be linked to exactly one GitHub Project (board) of an organisation, directly or by
  # choosing it from the organisation's list. Only that project's issues are read, so projects are never mixed.
  # The pretend GitHub answers GitHub's GraphQL API for the organisation "acme".

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the GitHub organisation "acme" has the projects "3 Customer portal" and "4 Mobile app"
    And "Customer portal" has the user story "[US-004] Sign in"
    And "Mobile app" has the user story "[US-020] Push notifications"

  @US-107 @iso27001-A.5.15 @integration
  Scenario: Only the linked GitHub Project's stories are read
    When I link "Acme Portal" to the GitHub Project "github.com/orgs/acme/projects/3" with a token
    Then "Acme Portal" has the user story "[US-004] Sign in"
    And "Acme Portal" does not have the user story "[US-020] Push notifications"
    And the page says the project reads "Customer portal"

  @US-107 @integration
  Scenario: Choose the project from the organisation
    When I enter the GitHub address "github.com/acme" for "Acme Portal" with a token
    Then I can choose between "3 Customer portal" and "4 Mobile app"
    When I choose "3 Customer portal"
    Then the page says the project reads "Customer portal"
    And "Acme Portal" has the user story "[US-004] Sign in"

  @US-107 @iso27001-A.5.15 @integration
  Scenario: Two tracker projects cannot share a GitHub Project
    Given "Acme Portal" is linked to the GitHub Project "github.com/orgs/acme/projects/3"
    And the project "Acme Mobile" exists
    When I link "Acme Mobile" to the GitHub Project "github.com/orgs/acme/projects/3" with a token
    Then I see the error "Already linked to Acme Portal"

  @US-107 @integration
  Scenario: Changing the GitHub Project does not mix the stories
    Given "Acme Portal" is linked to the GitHub Project "github.com/orgs/acme/projects/3"
    When I link "Acme Portal" to the GitHub Project "github.com/orgs/acme/projects/4" with a token
    Then the user story "[US-004] Sign in" of "Acme Portal" is "Removed"
    And "Acme Portal" has the user story "[US-020] Push notifications"

  @US-107 @asvs-V4.1.3 @integration
  Scenario: A GitHub Project needs a token
    When I link "Acme Portal" to the GitHub Project "github.com/orgs/acme/projects/3" without a token
    Then I see the error "Reading a GitHub Project needs a token with read access to projects"

  @US-107 @integration
  Scenario: A project the token cannot see is refused
    When I link "Acme Portal" to the GitHub Project "github.com/orgs/acme/projects/9" with a token
    Then I see the error "GitHub Project not found, or the token cannot see it"

  @US-107 @integration
  Scenario: A repository can still be linked as before
    Given the repository "acme/portal" has the user story "[US-030] Search"
    When I link "Acme Portal" to the GitHub Project "acme/portal" without a token
    Then "Acme Portal" has the user story "[US-030] Search"
