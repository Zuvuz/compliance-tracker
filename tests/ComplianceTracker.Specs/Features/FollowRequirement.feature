Feature: Follow one requirement to the end

  # US-200: the requirement's page lists every step it still needs (each linked story done and with evidence, the tests,
  # the requirement's own evidence), says how many are done, and makes the next open step the main action.
  # "Next requirement" becomes the main action only when every step is done.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in"
    And the project "Acme Portal" has the user story "[US-012] Lock account"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    And the user story "[US-012] Lock account" is linked to the requirement "I.2(d)"

  @US-200 @iso27001-A.8.25 @integration
  Scenario: The page lists every step and what is left
    When I open the requirement "I.2(d)" from the overview
    Then the steps of "[US-004] Sign in" and "[US-012] Lock account" are listed
    And the page says "0 of 6 steps done"
    And the next step is "[US-004] Sign in: evidence" with the action "Link evidence to the story"
    And "Next requirement" is there but does not stand out

  @US-200 @integration
  Scenario: Evidence on the requirement ticks its step
    Given I add the evidence "Access policy" with the link "https://example.com/policy" to "I.2(d)"
    When I open the requirement "I.2(d)" from the overview
    Then the step "Evidence on the requirement" is done
    And the page says "1 of 6 steps done"

  @US-200 @integration
  Scenario: A requirement that does not apply has no steps
    Given "I.2(d)" is not applicable for "Acme Portal"
    When I open the requirement "I.2(d)" from the overview
    Then the page has no steps
