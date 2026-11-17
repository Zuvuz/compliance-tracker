Feature: Messages: success fades, errors stay

  # US-171: the message of the last action is shown in one place above the page. A success is a status that fades after
  # 6 seconds; a warning or an error is an alert that stays.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the risk "Account takeover" exists in "Acme Portal"

  @US-171 @integration
  Scenario: A success is shown in the shared place, as a status that fades
    When I set the residual risk of "Account takeover" to 2 × 3
    Then the page shows the success "Residual risk set: 2 × 3" that fades

  @US-171 @integration
  Scenario: An error stays as an alert
    When I set the residual risk of "Account takeover" to 5 × 5
    Then the risk page says "The residual risk cannot be higher than the risk before measures"
    And the error is an alert
