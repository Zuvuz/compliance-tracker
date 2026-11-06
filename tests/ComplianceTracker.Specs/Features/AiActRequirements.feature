Feature: The AI Act's requirements come with the tracker
  US-062: the built-in AI Act version 1 has the requirements that matter to teams building or deploying AI systems:
  AI literacy, the prohibited practices, the high-risk requirements and provider obligations, and transparency.
  Titles and descriptions are summaries in our own words; each links to the binding text on EUR-Lex (B58).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "AI Act – Regulation (EU) 2024/1689" is switched on
    And "Acme Portal" uses "AI Act – Regulation (EU) 2024/1689"

  @US-062 @ai-act-art9 @integration
  Scenario: The AI Act's requirements are grouped by chapter
    When I open the "AI Act" sub-tab of "Acme Portal"
    Then I see the group "AI literacy" with 1 requirements, starting with "Art. 4"
    And I see the group "Prohibited AI practices" with 8 requirements, starting with "Art. 5(1)(a)"
    And I see the group "High-risk AI systems – requirements" with 7 requirements, starting with "Art. 9"
    And I see the group "High-risk AI systems – provider obligations" with 11 requirements, starting with "Art. 16"
    And I see the group "Transparency" with 4 requirements, starting with "Art. 50(1)"

  @US-062 @ai-act-art14 @integration
  Scenario: Each requirement has a title, says when it applies and links to the official text
    When I open the "AI Act" sub-tab of "Acme Portal"
    Then the requirement "Art. 14" says "Human oversight"
    And the requirement "Art. 5(1)(a)" says "Applies from 2 February 2025"
    And the requirement "Art. 50(2)" says "Applies from 2 August 2026"
    And each requirement links to the official text

  @US-062 @integration
  Scenario: The obligations for general-purpose AI models are left out
    When I open the "AI Act" sub-tab of "Acme Portal"
    Then the table does not list "Art. 53"

  @US-062 @ai-act-art14 @integration
  Scenario: The AI Act's requirements can be covered like any other
    Given the project "Acme Portal" has the user story "[US-020] A person approves every AI suggestion"
    When I open the "AI Act" sub-tab of "Acme Portal"
    And I link "[US-020] A person approves every AI suggestion" to the requirement "Art. 14"
    Then the row "Art. 14" is "Covered" by "[US-020] A person approves every AI suggestion"
