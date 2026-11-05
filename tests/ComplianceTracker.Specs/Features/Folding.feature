Feature: Fold evidence cards and requirement rows

  # Issue #52: an evidence card on the Evidence tab and a requirement row on the Compliance tab can be folded and
  # opened again. The choice is remembered in the browser (site.js); these scenarios check what the page gives the script:
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

  @integration
  Scenario: A requirement row can be folded and says what it has
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" can be folded, with the summary "1 story · 0 evidence"
    And the row "I.2(d)" does not need attention

  @integration
  Scenario: A requirement row with failing tests is never folded by itself
    When I upload test results where 2 scenarios tagged "@cra-I.2(d)" passed and 1 failed
    And I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" needs attention
