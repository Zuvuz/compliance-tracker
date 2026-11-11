Feature: A clear requirement row on the Compliance tab

  # US-144: each requirement row shows its user stories, its evidence and what waits for a decision as separate groups,
  # each line with where it came from. A row folds to one short line. One requirement has its own address, an "All"
  # sub-tab lists every framework, "not applicable" can name a risk, and unused frameworks can be added from the tab.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-144 @iso27001-7.5.3 @integration
  Scenario: Stories, evidence and pending items are shown apart, with their source
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    And the evidence "Access policy" is linked to "I.2(d)" and "I.2(b)"
    When I upload test results where 2 scenarios tagged "@cra-I.2(d)" passed
    And I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" has the groups "User stories (1)", "Evidence (1)" and "Waiting for your decision (1)"
    And in the row "I.2(d)" the story "[US-004] Sign in" says "Open" and "GitHub · linked by Anna"
    And in the row "I.2(d)" the evidence "Access policy" says "Uploaded by Anna"
    And in the row "I.2(d)" the waiting item can be accepted or rejected
    And the row "I.2(d)" sums up "1 story · 1 evidence · 1 waiting"

  @US-144 @integration
  Scenario: One requirement has its own address
    When I open the address of "I.2(d)" in "Acme Portal"
    Then the "CRA" sub-tab is shown with the row "I.2(d)" open
    And the row "I.2(d)" links to its own address

  @US-144 @iso27001-9.1 @integration
  Scenario: The All tab lists every framework's requirements
    Given "AI Act – Regulation (EU) 2024/1689" is switched on
    And "Acme Portal" uses "AI Act – Regulation (EU) 2024/1689"
    When I open the "All" sub-tab of "Acme Portal"
    Then the requirements are grouped under "AI Act" and "CRA"
    And the coverage adds up the frameworks

  @US-144 @cra-13 @integration
  Scenario: Not applicable can name a risk as its justification
    Given the risk "Remote access abuse" exists in "Acme Portal"
    When I mark "I.2(k)" as not applicable because "No remote access in the product" with the risk "Remote access abuse"
    Then the row "I.2(k)" is not applicable because of the risk "Remote access abuse"
    And the activity log has "RequirementMarkedNotApplicable" saying "(risk: Remote access abuse)"

  @US-144 @integration
  Scenario: A framework the project does not use can be added from the Compliance tab
    Given "AI Act – Regulation (EU) 2024/1689" is switched on
    When I open the "CRA" sub-tab of "Acme Portal"
    And I add "AI Act" to the project from the Compliance tab
    Then the Compliance tab of "Acme Portal" has the sub-tab "AI Act"
