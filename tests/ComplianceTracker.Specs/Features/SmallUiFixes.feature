Feature: Small fixes to the pages (#52)

  # #52 collects small fixes to the pages, found while using the app. Each scenario is one fix.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @issue-52 @integration
  Scenario: The project's tab groups are links, not menus
    When I open the "Overview" tab of "Acme Portal"
    Then no tab group of the project opens a menu
    And the tab group "Requirements" opens its first tab

  @issue-52 @integration
  Scenario: A risk of the organisation keeps "Organisation" marked in the top bar
    When I open "Organisation" from the top bar
    And I add the risk "Phishing" to "Organisation" with likelihood 3 and impact 4
    And I open the risk "Phishing"
    Then the top bar marks "Organisation"

  @issue-52 @integration
  Scenario: A measure is added above the list of measures
    Given the risk "Account takeover" exists in "Acme Portal"
    When I open the risk "Account takeover"
    Then "#add-measure" comes before "#risk-measures"

  @issue-52 @integration
  Scenario: A document's actions say what they are for, and adding comes first
    Given I upload the policy "Access control policy" version "v1" for the organisation
    When I open the Documents page
    Then "#add-document" comes before "#organisation-documents"
    And the document "Access control policy" has the groups "Upload a new version" and "Mark as reviewed"
    And "Remove" of the document "Access control policy" is under "⋯"

  @issue-52 @integration
  Scenario: The Compliance tab shows the requirements without evidence
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    And I add the evidence "Access policy" with the link "https://example.com/policy" to "I.2(e)"
    When I open the "CRA" sub-tab of "Acme Portal" showing "Without evidence"
    Then the table does not list "I.2(e)"
    And the table lists "I.2(d)" among others

  @issue-52 @integration
  Scenario: "Not applicable" is a button next to "Add evidence"
    When I open the requirement "I.2(d)" from the overview
    Then the page has the primary action "Add evidence" and next to it "Not applicable"
    And the action "Ask the AI" is under "More"

  @issue-52 @integration
  Scenario: A measure leads on to its requirement and back to the risk
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    And the risk "Account takeover" exists in "Acme Portal"
    And the risk "Account takeover" has the requirement "I.2(d)" as a measure
    When I open the risk "Account takeover"
    Then the measure "I.2(d)" says "1 story · 0 evidence" and offers "Add evidence"
    When I follow the measure "I.2(d)"
    Then I am on the page of "I.2(d)"
    And the page leads back to the risk "Account takeover"

  @issue-52 @integration
  Scenario: A story measure shows the requirements it covers
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    And the risk "Account takeover" exists in "Acme Portal"
    And the risk "Account takeover" has the measure "[US-004] Sign in"
    When I open the risk "Account takeover"
    Then the measure "[US-004] Sign in" says "covers I.2(d) · 0 evidence" and leads to "I.2(d)"

  @issue-52 @integration
  Scenario: The organisation area has no "Create story" for a requirement that needs action
    Given the AI says "Art. 21(2)(a)" applies and needs action with "High" because "no risk analysis policy"
    When I do the NIS2 scope check with sector "Manufacturing", 60 employees and a turnover of 12 million euro
    And I click "Find requirements that need action" on the "NIS2" sub-tab of "Organisation"
    And I open the requirement "Art. 21(2)(a)" from the "NIS2" sub-tab of "Organisation"
    Then the needs-action box has "Not needed" and no "Create story"
