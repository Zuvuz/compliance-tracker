Feature: To review

  # US-169: the top bar has Projects, Frameworks, Organisation (with its pages) and "To review"; the project's tabs are in
  # at most 6 groups. "To review" lists everything that waits for the member's decision, per project.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-169 @aiact-art14 @integration
  Scenario: The inbox lists what waits for me, with where to decide it
    Given "I.2(d)" is marked as needing action
    When I open the inbox "To review"
    Then "To review" lists "1 requirement marked as needing action" for "Acme Portal"
    And it opens the "Requirements" tab of "Acme Portal"

  @US-169 @integration
  Scenario: Nothing waits
    When I open the inbox "To review"
    Then "To review" says that nothing waits

  @US-169 @integration
  Scenario: The top bar has at most five items
    When I open the projects page
    Then the top bar has at most 5 items
