Feature: Requirements that need action

  # US-155: the AI marks the requirements that apply to the product but have no user story as "Needs action", with a
  # reason and a priority. A person creates a story for it, or says it is not needed (then it is not applicable).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-155 @cra-Art13 @integration
  Scenario: A relevant requirement without a story is marked
    Given the AI says "I.2(d)" applies and needs action with "High" because "the product has remote sign-in"
    When I click "Find what needs action" on the "CRA" sub-tab of "Acme Portal"
    Then the overview row "I.2(d)" is "Needs action"
    And the overview says "1 requirement needs action"
    And the page of "I.2(d)" shows "High" and "the product has remote sign-in" as AI-generated
    And the activity log has "NeedsActionSuggested" saying "1 need action and 0 do not apply"

  @US-155 @integration
  Scenario: Requirements that do not apply become the usual suggestion
    Given the AI says "I.2(k)" does not apply because "The product has no remote management"
    When I click "Find what needs action" on the "CRA" sub-tab of "Acme Portal"
    Then the overview row "I.2(k)" is "Not covered"
    And the row "I.2(k)" suggests "not applicable" because "The product has no remote management"

  @US-155 @integration
  Scenario: A High risk makes it High priority
    Given the risk "Account takeover" exists in "Acme Portal"
    # likelihood 3 × impact 4 = High
    And the risk "Account takeover" has the requirement "I.2(d)" as a measure
    And the AI says "I.2(d)" applies and needs action with "Low" because "sign-in exists"
    When I click "Find what needs action" on the "CRA" sub-tab of "Acme Portal"
    Then the page of "I.2(d)" shows "High" and "sign-in exists" as AI-generated
    And the page of "I.2(d)" says it was raised by a risk

  @US-155 @integration
  Scenario: Creating the story removes the mark
    Given "I.2(d)" is marked as needing action
    When I create the story from the AI's draft on "I.2(d)"
    And the issue "Meet I.2(d)" comes in with the next sync
    Then the overview row "I.2(d)" is "Covered"
    And the activity log has "NeedsActionConfirmed" saying "I.2(d)"

  @US-155 @integration
  Scenario: Not needed goes on to not applicable
    Given "I.2(d)" is marked as needing action
    When I say "I.2(d)" is not needed because "Sign-in is handled by the customer's own IdP"
    Then the overview row "I.2(d)" is "Not applicable"
    And the activity log has "NeedsActionDismissed" saying "I.2(d): Sign-in is handled by the customer's own IdP"

  @US-155 @integration
  Scenario: Not needed needs a reason
    Given "I.2(d)" is marked as needing action
    When I say "I.2(d)" is not needed because ""
    Then I am on the page of "I.2(d)"
    And the page shows an error
    And the overview row "I.2(d)" is "Needs action"

  @US-155 @integration
  Scenario: Only those who decide compliance can act on a mark
    Given "I.2(d)" is marked as needing action
    And "Bo" has joined "Acme ApS" as "Developer"
    When "Bo" opens the "CRA" sub-tab of "Acme Portal"
    Then the overview row "I.2(d)" is "Needs action"
    And the page of "I.2(d)" has no "Create story" or "Not needed"

  @US-155 @integration
  Scenario: Without an AI nothing is marked
    Given the local AI is not available
    When I click "Find what needs action" on the "CRA" sub-tab of "Acme Portal"
    Then the overview row "I.2(d)" is "Not covered"
    And the page shows an error
