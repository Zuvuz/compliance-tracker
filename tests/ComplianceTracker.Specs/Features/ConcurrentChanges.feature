Feature: Changes by two people at the same time

  # US-092: a form carries the version of what it shows. If someone else saved a change after the form was
  # opened, the save is refused with a clear message, the other person's change is kept, and what was typed
  # is shown again. "Someone else" changes the row directly in the database, as another member's save would.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-092 @iso27001-A.8.32 @integration
  Scenario: Saving a risk that someone else changed is refused
    Given the risk "Phishing" exists in "Acme Portal"
    And I have opened the risk "Phishing" for editing
    And someone else changes the impact of "Phishing" to 5
    When I save the description "Mails with fake links"
    Then I see the error "Someone else changed this while you were editing. Reload to see their change"
    And the impact of "Phishing" is still 5
    And the field "Description" still contains "Mails with fake links"

  @US-092 @iso27001-A.5.33 @integration
  Scenario: Saving a project that someone else changed is refused
    Given I have opened the project "Acme Portal" for editing
    And someone else renames the project "Acme Portal" to "Acme Web"
    When I save the scope "EU customers only"
    Then I see the error "Someone else changed this while you were editing. Reload to see their change"
    And the project is still called "Acme Web"

  @US-092 @iso27001-A.8.32 @integration
  Scenario: Correcting a requirement that someone else changed is refused
    Given the framework "Security policy" has a draft with "A.5.15 Access control"
    And I have the draft's page open
    And someone else changes the title of requirement "A.5.15" to "Access control rules"
    When I save the title "Access control policy" for "A.5.15" from the page I opened
    Then I see the error "Someone else changed this while you were editing. Reload to see their change"
    And the requirement "A.5.15" is still called "Access control rules"

  @US-092 @integration
  Scenario: Without a change by someone else the save works
    Given the risk "Phishing" exists in "Acme Portal"
    And I have opened the risk "Phishing" for editing
    When I save the description "Mails with fake links"
    Then the description of "Phishing" is "Mails with fake links"
