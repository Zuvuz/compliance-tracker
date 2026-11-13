Feature: Personal data: privacy notice, access and erasure

  # US-095: the privacy page says which personal data the tracker keeps and why; a member can download their own data;
  # an Admin can erase a removed member's personal data, which becomes a pseudonym while the history is kept.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And "Carl" has joined "Acme ApS" as "Developer"

  @US-095 @gdpr-art13 @integration
  Scenario: The privacy page says what is kept
    When I open the privacy page
    Then the privacy page lists "name", "keycloak-id", "role", "activity" and "content"
    And the privacy page links to "my data"

  @US-095 @gdpr-art15 @integration
  Scenario: A member downloads their own data
    Given the project "Acme Portal" exists
    When I download my data
    Then my data has the name "Anna" and an entry "ProjectCreated"
    And the activity log has "PersonalDataExported" without the name "Anna"

  @US-095 @gdpr-art17 @integration
  Scenario: An Admin erases a removed member's personal data
    Given I have removed "Carl"
    When I erase the personal data of "Carl"
    Then the removed members list a "Former member" with the personal data erased
    And no member is called "Carl" any more
    And the activity log has "PersonalDataErased" without the name "Carl"

  @US-095 @gdpr-art17 @integration
  Scenario: An active member cannot be erased
    When I send an erasure of "Carl" anyway
    Then I see the error "Remove the member first"
    And "Carl" is still a member

  @US-095 @iso27001-A.5.15 @integration
  Scenario: Only who manages members can erase
    Given I have removed "Carl"
    And "Dan" has joined "Acme ApS" as "Developer"
    When "Dan" sends an erasure of "Carl"
    Then no personal data was erased
