Feature: Persist organisations

  Background:
    Given an empty database

  @US-002 @iso27001-7.5.3 @integration
  Scenario: A created organisation still exists after restart
    Given I have created the organisation "Acme ApS"
    When the application restarts
    Then the organisation "Acme ApS" exists

  @US-002 @iso27001-7.5.3 @integration
  Scenario: Organisation data is stored unchanged
    Given I have created an organisation with name "Acme ApS"
    When I retrieve the organisation
    Then all its data matches what was created

  @US-002 @iso27001-7.5.3 @integration
  Scenario: The activity log entry is stored with the organisation
    Given I have created the organisation "Acme ApS"
    When the application restarts
    Then an activity log entry exists for the creation of "Acme ApS"

  @US-002 @US-016 @iso27001-7.5.3 @integration
  Scenario: Nothing is saved if saving fails
    Given saving the activity log will fail
    When I try to create the organisation "Acme ApS"
    Then the creation fails
    And the organisation "Acme ApS" does not exist
