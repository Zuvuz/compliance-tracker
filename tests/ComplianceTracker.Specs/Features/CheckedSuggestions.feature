Feature: AI suggestions that have read the document

  # US-205: a document is suggested for a requirement only after the checking model has read it and quoted the passage
  # that meets the requirement, fully or partly. A quote that is not in the document, or a document only on the same topic,
  # is not suggested. Without the checking model the suggestions are made as before, marked "not checked". The AI is the
  # pretend one.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And the project "Acme Portal" exists
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the minimum score for evidence suggestions is 0.5
    And the AI rates "Incident handling procedure" against "I.2(d)" with 0.82

  @US-205 @aiact-art14 @iso27001-7.5.3 @integration
  Scenario: A suggestion shows the passage that partly meets the requirement
    Given the AI check finds that "I.2(d)" is partly met by "Incidents are reported to CFCS within 24 hours" and misses "who reports to ENISA"
    When I upload the policy "Incident handling procedure" version "v1" with the content "Incidents are reported to CFCS within 24 hours. The team meets every week."
    Then the document suggestion "I.2(d)" says "Partly" with the passage "Incidents are reported to CFCS within 24 hours"
    And the document suggestion "I.2(d)" says what is missing: "who reports to ENISA"
    And the document suggestion "I.2(d)" is checked by "fake-chat"

  @US-205 @integration
  Scenario: A document only on the same topic is not suggested
    Given the AI rates "Incident handling procedure" against "I.2(e)" with 0.8
    And the AI check finds that "I.2(d)" is met by "Incidents are reported to CFCS within 24 hours"
    When I upload the policy "Incident handling procedure" version "v1" with the content "Incidents are reported to CFCS within 24 hours."
    Then the document suggestion "I.2(d)" says "Meets" with the passage "Incidents are reported to CFCS within 24 hours"
    And "Incident handling procedure" is not suggested for "I.2(e)"

  @US-205 @aiact-art15 @integration
  Scenario: A passage that is not in the document is dropped
    Given the AI check finds that "I.2(d)" is met by "We report every incident to ENISA"
    When I upload the policy "Incident handling procedure" version "v1" with the content "Incidents are reported to CFCS within 24 hours."
    Then "Incident handling procedure" is not suggested for "I.2(d)"

  @US-205 @integration
  Scenario: Without the checking model a suggestion is marked not checked
    When I upload the policy "Incident handling procedure" version "v1" with the content "Incidents are reported to CFCS within 24 hours."
    Then the document suggestion "I.2(d)" is not checked

  @US-205 @aiact-art15 @integration
  Scenario: The system owner measures the checked suggestions on people's decisions
    Given the AI check finds that "I.2(d)" is met by "Incidents are reported to CFCS within 24 hours"
    And I have uploaded the policy "Incident handling procedure" version "v1" with the content "Incidents are reported to CFCS within 24 hours."
    And I have approved the AI suggestion "I.2(d)" for "Incident handling procedure"
    And "Acme ApS" shares its decisions to improve the AI
    And I am signed in as the system admin "Sys"
    When I measure the document suggestions
    Then the newest document suggestion run has 1 decision, "100 %" right when checked, "100 %" found, and the model "fake-chat"

  @US-205 @integration
  Scenario: Evidence shows the requirements it reaches through its stories
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    And I upload the evidence "Login test report" with the file "login.pdf" on the Evidence tab of "Acme Portal"
    And the evidence "Login test report" of "Acme Portal" shows the story "[US-004] Sign in" is done
    When I link "Login test report" of "Acme Portal" directly to what it reaches through "[US-004] Sign in"
    Then "Acme Portal" has the evidence "Login test report" linked to "I.2(d)"
