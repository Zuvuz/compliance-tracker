Feature: Create organisation via web UI

  # Since US-004 the creator is the signed-in user, so the form no longer has a "Your name" field

  Background:
    Given an empty database
    And I am signed in as "Anna"
    And I am on the create organisation page

  @US-003 @iso27001-A.8.28 @integration
  Scenario: Create an organisation through the form
    When I submit the form with name "Acme ApS", CVR "12345678", sector "Manufacturing" and size "Small"
    Then I see the details of the organisation "Acme ApS"
    And "Anna" is shown as Admin

  @US-003 @iso27001-A.8.28 @integration
  Scenario: Validation errors are shown and input is kept
    When I submit the form with name "Acme ApS", CVR "1234", sector "Manufacturing" and size "Small"
    Then I see the error "CVR must be 8 digits"
    And the name field still contains "Acme ApS"

  @US-003 @integration
  Scenario: A used CVR is rejected
    Given an organisation with CVR "12345678" exists
    When I submit the form with name "Beta ApS", CVR "12345678", sector "Health" and size "Medium"
    Then I see the error "CVR already in use"

  @US-003 @asvs-v3.5.1 @integration
  Scenario: The form rejects requests without an antiforgery token
    When I post the form without an antiforgery token
    Then the request is rejected with status 400
    And no organisation is saved
