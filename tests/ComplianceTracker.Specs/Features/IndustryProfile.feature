Feature: Industry profile

  # US-080: an Admin chooses the industry the organisation makes products for, and the tracker suggests the
  # regulations, scans and reports that usually fit, each with a reason. Nothing is switched on by itself. Software is
  # ready first; Medtech and Fintech say their own suggestions are coming (B105).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-080 @iso27001-A.5.31 @integration
  Scenario: Software suggests the CRA with a reason
    When I choose the industry "Software"
    Then the industry suggests "CRA – Regulation (EU) 2024/2847" because "You make products with digital elements that are sold in the EU"
    And the industry suggests "NIS2 – Directive (EU) 2022/2555" because "Do the scope check to see whether NIS2 covers the organisation"
    And the activity log has "IndustryProfileChosen" with "Software"

  @US-080 @integration
  Scenario: Suggestions are not switched on by themselves
    When I choose the industry "Software"
    Then the suggestion "CRA – Regulation (EU) 2024/2847" is "Off"
    When I switch on the suggestion "CRA – Regulation (EU) 2024/2847"
    Then the suggestion "CRA – Regulation (EU) 2024/2847" is "On"

  @US-080 @integration
  Scenario: The AI Act is suggested only for products with AI
    When I choose the industry "Software"
    Then the industry does not suggest "AI Act – Regulation (EU) 2024/1689"
    When I choose the industry "Software" with AI in the products
    Then the industry suggests "AI Act – Regulation (EU) 2024/1689" because "Your products use AI, so the AI Act may apply (high-risk, transparency)"

  @US-080 @integration
  Scenario: Changing the industry switches nothing off
    Given I have chosen the industry "Software"
    And "CRA – Regulation (EU) 2024/2847" is switched on
    When I choose the industry "Other"
    Then the industry makes no suggestions
    And I see "CRA – Regulation (EU) 2024/2847" switched on on the built-in frameworks page

  @US-080 @integration
  Scenario: Medtech says its suggestions are coming
    When I choose the industry "Medtech"
    Then I am told the suggestions for "Medtech" are coming
    And the industry suggests "CRA – Regulation (EU) 2024/2847" because "You make products with digital elements that are sold in the EU"

  @US-080 @integration
  Scenario: Choosing the industry is a first step
    When I open the projects page
    Then the "Get started" card shows "Choose your industry" as "To do"
    When I choose the industry "Software"
    And I open the projects page
    Then the "Get started" card shows "Choose your industry" as "Done"
