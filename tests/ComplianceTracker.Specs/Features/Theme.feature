Feature: A calm, professional look

  # US-064: one theme in navy and teal on every page, the font served by the tracker itself,
  # and the product name set in one place until the final name is chosen.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-064 @integration
  Scenario: Every page uses the theme
    When I open the front page
    Then the page uses the stylesheet "site.css"
    And the top bar has the theme's class

  @US-064 @iso27001-A.5.34 @integration
  Scenario: Nothing is loaded from other hosts
    When I open the front page
    Then every stylesheet, script and font comes from the tracker itself

  @US-064 @integration
  Scenario: The product name is set in one place
    When I open the front page with the product name set to "Acme Comply"
    Then the page title ends with "Acme Comply"
    And the top bar shows "Acme Comply"

  @US-064 @integration
  Scenario: Without a setting the name stays as today
    When I open the front page
    Then the top bar shows "Compliance Tracker"
