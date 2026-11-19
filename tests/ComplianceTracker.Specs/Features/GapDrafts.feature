Feature: The AI drafts work for the gaps

  # US-108: for the requirements of one regulation that nothing covers yet, the AI drafts what the team could build, in the
  # project's own form (user stories, use cases or requirements), fitted to the intended purpose. A requirement that does not
  # fit gets a "not applicable" suggestion instead. Nothing is work until a person creates it. The AI is the pretend one.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the intended purpose of "Acme Portal" is "A customer portal where customers manage their orders"
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-108 @cra-annex1 @aiact-art50 @integration
  Scenario: Drafts are user stories for a project that uses GitHub user stories
    Given I have chosen the methods "GitHub user stories" for "Acme Portal"
    When I ask the AI to draft for the gaps in the CRA of "Acme Portal"
    Then there is a draft "UserStory" covering "I.2(a)" marked "AI-generated"
    And the draft covering "I.2(a)" says how it fits the product
    And the activity log has "GapDraftsMade" with "10 user story drafts and 0"

  @US-108 @cra-annex1 @integration
  Scenario: Drafts are use cases for a project that uses Unified Process
    Given I have chosen the methods "Use cases" for "Acme Portal"
    When I ask the AI to draft for the gaps in the CRA of "Acme Portal"
    Then there is a draft "UseCase" covering "I.2(d)" marked "AI-generated"
    And the draft covering "I.2(d)" has a primary actor and a main success scenario

  @US-108 @cra-art13 @integration
  Scenario: A requirement that does not fit the purpose gets a "not applicable" suggestion instead
    Given I have chosen the methods "GitHub user stories" for "Acme Portal"
    And the AI finds that "I.2(i)" does not fit the intended purpose
    When I ask the AI to draft for the gaps in the CRA of "Acme Portal"
    Then there is no draft covering "I.2(i)"
    And "I.2(i)" of "Acme Portal" has a "not applicable" suggestion saying "The portal has no network of its own"

  @US-108 @aiact-art14 @integration
  Scenario: A use case becomes work only when a person creates it, and is linked when the team adds it
    Given I have chosen the methods "Use cases" for "Acme Portal"
    And the AI puts "I.2(d)" and "I.2(e)" in one draft
    And I have asked the AI to draft for the gaps in the CRA of "Acme Portal"
    When I change the extensions of the draft covering "I.2(d)" to "3a. The one-time code is wrong" and create it
    Then the created draft covering "I.2(d)" waits for the team's document with "3a. The one-time code is wrong"
    And the activity log has "GapDraftAccepted" with "I.2(d), I.2(e): Meet I.2(d), I.2(e) (edited)"
    When I upload the use case document "use-cases.docx" to "Acme Portal" containing:
      """
      # UC-12 Meet I.2(d), I.2(e)
      Primary actor: Customer
      Main success scenario:
      1. The customer signs in
      2. The system checks the one-time code
      """
    Then "I.2(d)" and "I.2(e)" of "Acme Portal" are covered by "Meet I.2(d), I.2(e)"

  @US-108 @iso27001-A.5.15 @integration
  Scenario: An auditor cannot make drafts
    Given "Bo" has joined "Acme ApS" as "Auditor"
    And I am signed in as "Bo"
    When I open the CRA of "Acme Portal"
    Then there is no "Draft for the gaps" button
