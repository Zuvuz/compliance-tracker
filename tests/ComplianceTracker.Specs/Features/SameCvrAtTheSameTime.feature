Feature: Same CVR saved at the same time

  # US-015: when two people save the same CVR at the same moment, both pass the "CVR already in use" check,
  # and the database's unique index stops the second. That person sees the same message, not an error page.

  Background:
    Given an empty database
    And I am signed in as "Anna"
    And I am on the create organisation page

  @US-015 @iso27001-A.8.28 @integration
  Scenario: A CVR saved by someone else at the same moment
    Given another organisation with CVR "12345678" is saved while I am creating mine
    When I submit the form with name "Acme ApS", CVR "12345678", sector "Manufacturing" and size "Small"
    Then I see the error "CVR already in use"
    And the name field still contains "Acme ApS"
    And there is exactly one organisation with CVR "12345678"
    And there is exactly one "OrganizationCreated" log entry
