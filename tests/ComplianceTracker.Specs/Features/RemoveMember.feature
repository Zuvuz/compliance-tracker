Feature: Remove a member

  # US-090: an Admin removes a member who leaves. Their access ends at once; what they did keeps their name.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And "Carl" has joined "Acme ApS" as "Developer"

  @US-090 @iso27001-A.5.18 @integration
  Scenario: A removed member loses access at once
    When I remove "Carl"
    And "Carl" opens "/Projects"
    Then "Carl" sees "You are no longer a member of Acme ApS"
    And "Carl" is not listed among the members

  @US-090 @integration
  Scenario: The last Admin cannot be removed
    When I remove "Anna"
    Then I see the error "Give another member the Admin role first"

  @US-090 @iso27001-A.8.15 @integration
  Scenario: The removal is logged and the history keeps the name
    Given the project "Acme Portal" exists
    And the risk "Phishing" exists in "Acme Portal"
    And the risk "Phishing" is owned by "Carl"
    When I remove "Carl"
    Then the activity log for "Acme ApS" has "MemberRemoved" for "Anna"
    And the risk "Phishing" shows the owner "Owner removed (Carl)"

  @US-090 @iso27001-A.5.18 @integration
  Scenario: A removed member cannot be chosen as a risk's owner
    Given the project "Acme Portal" exists
    When I remove "Carl"
    Then "Carl" cannot be chosen as a risk's owner in "Acme Portal"

  @US-090 @integration
  Scenario: A removed member can start their own organisation
    Given I have removed "Carl"
    When "Carl" creates the organisation "Carl ApS"
    Then "Carl" is a member of "Carl ApS" with the role "Admin"

  @US-090 @integration
  Scenario: A removed member who is invited again comes back
    Given I have removed "Carl"
    And I have created an invitation link with the role "Auditor"
    When "Carl" opens the invitation link and joins
    Then "Carl" is a member of "Acme ApS" with the role "Auditor"
