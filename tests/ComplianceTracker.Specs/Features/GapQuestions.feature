Feature: NIS2 questions for what the documents do not show

  # US-189: each NIS2 measure has a question with a maturity scale (0–4). A question whose requirement approved evidence
  # already covers is folded away; an answer of 3 or more without evidence "needs evidence"; earlier answers are kept.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And I have done the NIS2 scope check as an important entity
    And the minimum score for evidence suggestions is 0.5

  @US-189 @nis2-art21 @integration
  Scenario: A measure covered by approved evidence is not asked about
    Given the AI rates "Backup procedure" against "Art. 21(2)(c)" with 0.9
    And I have uploaded to the NIS2 gap analysis "Backup procedure.txt" saying "A daily backup is copied to a second site."
    And I have approved the gap suggestion "Art. 21(2)(c)" for "Backup procedure"
    When I open the NIS2 gap analysis
    Then the question "Art. 21(2)(c)" is covered by evidence
    And the question "Art. 21(2)(d)" is open

  @US-189 @nis2-art21 @integration
  Scenario: An answer sets the maturity and is logged
    When I answer "Art. 21(2)(d)" with "AdHoc" and "We know the main suppliers"
    Then the question "Art. 21(2)(d)" has the answer "1 – Ad hoc"
    And the activity log has "GapQuestionAnswered" saying "Art. 21(2)(d): 1 – Ad hoc"

  @US-189 @nis2-art21 @integration
  Scenario: An answer of 3 or more without evidence needs evidence
    When I answer "Art. 21(2)(d)" with "Implemented" and "Contracts cover security"
    Then the question "Art. 21(2)(d)" needs evidence

  @US-189 @iso27001-A.5.33 @integration
  Scenario: A changed answer keeps the earlier one
    Given I have answered "Art. 21(2)(h)" with "None" and ""
    When I answer "Art. 21(2)(h)" with "Defined" and "Encryption rules written"
    Then the question "Art. 21(2)(h)" has the answer "2 – Defined"
    And the question "Art. 21(2)(h)" lists 1 earlier answer

  @US-189 @iso27001-A.5.15 @integration
  Scenario: A developer sees the questions but cannot answer
    Given "Carl" has joined "Acme ApS" as "Developer"
    And I am signed in as "Carl"
    When I open the NIS2 gap analysis
    Then the question "Art. 21(2)(d)" cannot be answered
