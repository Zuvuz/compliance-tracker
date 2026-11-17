Feature: One organisation profile

  # US-163: Organisation -> Profile has the industry, NIS2 sector, country and "Our products use AI" together, each with
  # what it changes. Choosing a sector suggests an industry; nothing changes until the admin saves. Settings says it is
  # for the admins and holds only settings.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-163 @iso27001-4.3 @integration
  Scenario: Industry and sector are chosen together
    When I open the organisation's profile
    And I choose the NIS2 sector "Health"
    Then the profile suggests the industry "Medtech"
    And the organisation's sector is still "Manufacturing"
    And the profile has the country, the industry and "Our products use AI", each saying what it changes

  @US-163 @integration
  Scenario: Every industry can be chosen, with a note when its own suggestions are coming
    When I open the organisation's profile
    Then the industry list has "Software", "Medtech", "Fintech" and "Other"
    And "Medtech" says its own suggestions are coming

  @US-163 @integration
  Scenario: Settings says who it is for and holds only settings
    When I open the Settings page
    Then the Settings page says it is for the organisation's admins
    And the Settings page has no industry or country choice
    And the Settings page links to the organisation's profile

  @US-163 @iso27001-A.5.15 @integration
  Scenario: Only admins change the profile
    Given "Bo" has joined "Acme ApS" as "Developer"
    And I am signed in as "Bo"
    When I open the organisation's profile
    Then the profile has no form
    And the profile shows the industry "Not chosen"
