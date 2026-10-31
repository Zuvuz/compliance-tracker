Feature: Application structure

  # US-029: the code is built so each concern lives in one place.
  # Everything else about the behaviour is proven by the other features, which did not change.

  @US-029 @iso27001-A.8.9 @integration
  Scenario: The app refuses to start without sign-in settings
    Given the Keycloak setting "ClientId" is missing
    When the application starts
    Then it stops with the message "Authentication:Keycloak:ClientId is required"

  @US-029 @iso27001-A.8.25 @integration
  Scenario: Every service can be created
    When the application starts
    Then the container can create every controller and service

  @US-029 @iso27001-A.8.25
  Scenario: Controllers only talk to the Application layer
    Then no controller depends on a repository

  @US-029 @iso27001-A.8.28 @integration
  Scenario: Use cases act for the signed-in user only
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And "Bo" has created the organisation "Beta ApS"
    And I am signed in as "Bo"
    When I create the project "Beta Portal" with intended purpose "Web portal"
    Then the project "Beta Portal" belongs to "Beta ApS"
