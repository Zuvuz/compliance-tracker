Feature: Classify a project under each regulation

  # US-026: each built-in regulation a project follows can be classified, e.g. "CRA: Important, class I" or
  # "AI Act: High-risk", or marked "Not applicable", always with a reason. A new classification replaces the old one,
  # and the history is kept. MDR comes with US-023; the issue's MDR examples are shown here with the CRA and the AI Act.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "AI Act – Regulation (EU) 2024/1689" is switched on
    And the project "Billede analyse af CT" exists
    And "Billede analyse af CT" uses "CRA – Regulation (EU) 2024/2847"
    And "Billede analyse af CT" uses "AI Act – Regulation (EU) 2024/1689"

  @US-026 @aiact-art6 @iso27001-A.5.31 @integration
  Scenario: Classify a project under a regulation
    When I classify "Billede analyse af CT" under "AI Act – Regulation (EU) 2024/1689" as "High-risk" because "Art. 6(1), Annex I: safety component of a medical device"
    Then "Billede analyse af CT" shows "AI Act – Regulation (EU) 2024/1689" as "High-risk"

  @US-026 @iso27001-A.5.31 @integration
  Scenario: Every followed regulation is listed
    Then "Billede analyse af CT" shows "AI Act – Regulation (EU) 2024/1689" as "Not classified"
    And "Billede analyse af CT" shows "CRA – Regulation (EU) 2024/2847" as "Not classified"

  @US-026 @cra-art2 @integration
  Scenario: A regulation can be marked as not applicable
    When I classify "Billede analyse af CT" under "CRA – Regulation (EU) 2024/2847" as "Not applicable" because "Art. 2(2): covered by the MDR"
    Then "Billede analyse af CT" shows "CRA – Regulation (EU) 2024/2847" as "Not applicable"

  @US-026 @integration
  Scenario: A reason is required
    When I classify "Billede analyse af CT" under "CRA – Regulation (EU) 2024/2847" as "Important, class I" without a reason
    Then I see the error "A reason is required"

  @US-026 @integration
  Scenario: The form cannot be sent without a reason
    When I open the "Overview" tab of "Billede analyse af CT"
    Then the classify form for "CRA – Regulation (EU) 2024/2847" needs a reason

  @US-026 @cra-annexIII @integration
  Scenario: Each regulation offers its own classes and a guide
    When I open the "Overview" tab of "Billede analyse af CT"
    Then the classes for "CRA – Regulation (EU) 2024/2847" are "Default (not important or critical)", "Important, class I", "Important, class II", "Critical" and "Not applicable"
    And the guide for "CRA – Regulation (EU) 2024/2847" names "Annex III"

  @US-026 @iso27001-7.5.3 @integration
  Scenario: Earlier classifications are kept
    Given "Billede analyse af CT" is classified under "CRA – Regulation (EU) 2024/2847" as "Important, class I"
    When I classify "Billede analyse af CT" under "CRA – Regulation (EU) 2024/2847" as "Important, class II" because "Annex III, class II: a firewall"
    Then the classification history of "Billede analyse af CT" under "CRA – Regulation (EU) 2024/2847" shows "Important, class I" followed by "Important, class II"

  @US-026 @iso27001-A.8.15 @integration
  Scenario: Classifications are logged
    When I classify "Billede analyse af CT" under "AI Act – Regulation (EU) 2024/1689" as "High-risk" because "Annex III"
    Then the activity log for "Acme ApS" has "ProjectClassified" for "Anna" concerning "Billede analyse af CT"

  @US-026 @iso27001-A.5.15 @integration
  Scenario: A developer sees the classification but cannot change it
    Given "Carl" has joined "Acme ApS" as "Developer"
    When "Carl" opens the "Overview" tab of "Billede analyse af CT"
    Then there is no "Save" button
