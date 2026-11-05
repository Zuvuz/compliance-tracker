Feature: Sign in with OpenID Connect

  Background:
    Given an empty database

  @US-004 @iso27001-A.5.15 @integration
  Scenario: Only the front page is public
    Given I am not signed in
    When I open the front page
    Then I see the front page
    When I open the create organisation page
    Then I am sent to sign in

  @US-004 @iso27001-A.5.16 @asvs-v10.5.2 @integration
  Scenario: First sign-in creates my organisation with me as Admin
    Given I am signed in as "Anna" with e-mail "anna@acme.dk" for the first time
    When I open the application
    Then I am sent to create my organisation
    When I create the organisation "Acme ApS" with CVR "12345678", sector "Manufacturing" and size "Small"
    Then I see the details of the organisation "Acme ApS"
    And "Anna" with e-mail "anna@acme.dk" is shown as Admin

  @US-004 @integration
  Scenario: A user with an organisation is sent to it
    Given "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    When I open the create organisation page
    Then I see the details of the organisation "Acme ApS"

  @US-004 @integration
  Scenario: A user cannot create a second organisation
    Given "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    When I post the create form for "Beta ApS" with CVR "87654321"
    Then I see the error "User already has an organisation"

  @US-004 @iso27001-A.5.15 @integration
  Scenario: Other organisations are hidden
    Given "Bo" has created the organisation "Beta ApS"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    When I open the page of "Beta ApS"
    Then the page is not found

  @US-004 @iso27001-A.8.15 @integration
  Scenario: Sign-in and sign-out are logged
    Given "Anna" has created the organisation "Acme ApS"
    When "Anna" signs in
    And "Anna" signs out
    Then the activity log for "Acme ApS" has "UserSignedIn" and "UserSignedOut" for "Anna"

  @US-004 @iso27001-A.8.5 @asvs-v10.2.1 @integration @keycloak
  Scenario: Sign in through Keycloak
    Given Keycloak has a user "anna" with e-mail "anna@acme.dk"
    When I sign in through Keycloak as "anna"
    Then I am signed in to the application as "anna"

  @US-004 @US-016 @iso27001-A.8.15 @integration
  Scenario: Sign-in is logged with its time
    Given "Anna" has created the organisation "Acme ApS"
    And the time is "2026-10-24T14:02:00Z"
    When "Anna" signs in
    Then the activity log for "Acme ApS" has "UserSignedIn" at "2026-10-24T14:02:00Z"
