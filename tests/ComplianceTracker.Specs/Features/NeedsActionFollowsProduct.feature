Feature: Needs action follows the product

  # US-156: when the description, intended purpose, scope, classification, industry profile or frameworks change, the
  # tracker assesses again what needs action (US-155) in the background, a few minutes later, so several changes give one
  # assessment. Pending marks are replaced; decisions a person made are kept and marked "Based on an earlier description".

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-156 @cra-Art.13 @integration
  Scenario: A new intended purpose is assessed again
    Given the AI says "I.2(d)" applies and needs action with "High" because "the product has remote sign-in"
    When I change the intended purpose of "Acme Portal" to "Customer portal with remote sign-in"
    Then the Compliance tab of "Acme Portal" says it is assessing again
    When the background work has run
    Then the overview row "I.2(d)" is "Needs action"
    And the activity log has "NeedsActionReassessed" saying "intended purpose changed: "
    And the Compliance tab of "Acme Portal" says it was assessed again

  @US-156 @integration
  Scenario: A decision a person made is kept
    Given "I.2(k)" is marked as needing action
    And I say "I.2(k)" is not needed because "No remote management"
    When I change the intended purpose of "Acme Portal" to "A portal that can now be managed remotely"
    And the background work has run
    Then the overview row "I.2(k)" is "Not applicable"
    And the requirement page of "I.2(k)" says "Based on an earlier description"

  @US-156 @integration
  Scenario: Several changes give one assessment
    When I change the intended purpose of "Acme Portal" to "Customer portal with remote sign-in"
    And one minute later I change the scope of "Acme Portal" to "Web and mobile"
    And the background work has run
    Then the AI assessed "Acme Portal" again once, because "CRA added; intended purpose changed; scope changed"

  @US-156 @integration
  Scenario: Without an AI the assessment waits
    Given the local AI is not available
    When I change the intended purpose of "Acme Portal" to "Customer portal with remote sign-in"
    And the background work has run
    Then the Compliance tab of "Acme Portal" says the assessment is waiting for the AI
    And the activity log has no "NeedsActionReassessed"
