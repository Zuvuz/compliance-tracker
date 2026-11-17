Feature: Edit the organisation's details

  # US-027: the Admin corrects the organisation's name, sector and size on Organisation → Profile, with the same rules as
  # when creating it. The CVR cannot be changed. Every change is logged with the old and new value.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-027 @iso27001-4.1 @integration
  Scenario: Change the size
    When I change the size of the organisation to "Medium"
    Then the profile shows the size "Medium"
    And the CVR is shown but cannot be edited

  @US-027 @integration
  Scenario: The name is still required
    When I change the name of the organisation to ""
    Then I see the error "Name is required"
    And the profile shows the name "Acme ApS"

  @US-027 @iso27001-A.8.15 @integration
  Scenario: Changes are logged with the old and new value
    When I change the size of the organisation to "Medium"
    Then the activity log for "Acme ApS" has "OrganizationUpdated" with "Size: Small → Medium"

  @US-027 @iso27001-A.5.15 @integration
  Scenario: Only an Admin can edit the organisation
    Given "Dan" is in "Acme ApS" with the role "Developer"
    And I am signed in as "Dan"
    When I open the organisation's profile
    Then the profile has no form
    And changing the size anyway is refused
