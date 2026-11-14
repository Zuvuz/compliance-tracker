Feature: Fold evidence cards

  # Issue #52: an evidence card on the Evidence tab can be folded and opened again. (A requirement row no longer folds:
  # the Compliance tab is a light overview since US-153, see RequirementPage.feature.) The choice is remembered in the browser (site.js); these scenarios check what the page gives the script:
  # the button, the part that folds, a short summary, and whether the part needs attention (then it is never folded).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @integration
  Scenario: An evidence card can be folded and says what it proves
    Given the evidence "Access policy 2026" is linked to "I.2(d)" and "I.2(b)"
    When I open the Evidence tab of "Acme Portal"
    Then the evidence card "Access policy 2026" can be folded, with the summary "Proves 2 requirements"

  @integration
  Scenario: An evidence card with AI suggestions waiting is never folded by itself
    Given "Access policy 2026" has the suggestion "I.2(d)"
    When I open the Evidence tab of "Acme Portal"
    Then the evidence card "Access policy 2026" needs attention
