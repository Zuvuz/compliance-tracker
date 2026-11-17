Feature: Every page usable with a screen reader and keyboard

  # US-185: every field says what it is, every button and link has a name, every image has alt text, and the shared
  # dialog works with the keyboard (WCAG 2.1 AA 1.3.1, 2.1.1, 2.4.7, 4.1.2). A new page that breaks a rule fails the build.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And the project "Acme Portal" exists
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And "Acme Portal" has 30 user stories, 12 risks and 20 pieces of evidence on its requirements
    And the framework "Security policy" has been uploaded and activated

  @US-185 @integration
  Scenario: Every field on every page has a label
    When I open every page of "Acme ApS"
    Then every field has a label a screen reader can read
