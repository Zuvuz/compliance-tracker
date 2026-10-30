Feature: Switch built-in frameworks on and off

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-006 @iso27001-A.5.31 @integration
  Scenario: Built-in frameworks are available
    When I open the built-in frameworks page
    Then I see "CRA – Regulation (EU) 2024/2847" switched off
    And I see "AI Act – Regulation (EU) 2024/1689" switched off

  @US-006 @iso27001-A.5.31 @integration
  Scenario: A switched-on framework is shown for the organisation
    When I switch "CRA – Regulation (EU) 2024/2847" on
    Then the framework list shows "CRA – Regulation (EU) 2024/2847" marked "Built-in"

  @US-006 @integration
  Scenario: A framework can be switched off and on again
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    When I switch "CRA – Regulation (EU) 2024/2847" off
    Then the framework list does not show "CRA – Regulation (EU) 2024/2847"
    When I switch "CRA – Regulation (EU) 2024/2847" on
    Then the framework list shows "CRA – Regulation (EU) 2024/2847" marked "Built-in"

  @US-006 @integration
  Scenario: Built-in frameworks cannot be changed
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    When I try to upload a new version of "CRA – Regulation (EU) 2024/2847"
    Then the request is rejected

  @US-006 @iso27001-A.8.15 @integration
  Scenario: Switching is logged
    When I switch "CRA – Regulation (EU) 2024/2847" on
    Then the activity log for "Acme ApS" has "FrameworkEnabled" for "Anna" concerning "CRA – Regulation (EU) 2024/2847"
