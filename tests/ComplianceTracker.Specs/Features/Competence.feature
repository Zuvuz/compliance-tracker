Feature: Competence and awareness

  # US-213: the organisation keeps the competences it needs, which roles need them and how often they are renewed, records
  # who was trained, and sees who has read the current version of each policy (ISO/IEC 27001 7.2, 7.3). Each member confirms
  # a policy themself. The page is "Competence and awareness" in the ISMS area.

  Background:
    Given an empty database
    And the time is "2026-11-21T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And "Bo" has joined "Acme ApS" as "Developer"

  @US-213 @iso27001-7.3 @integration
  Scenario: A new policy version must be read again
    Given I upload the policy "Access control policy" version "3" for the organisation
    And "Bo" has confirmed the policy "Access control policy"
    When I upload version "4" of "Access control policy" with the content "Version four"
    Then "Bo" must read the policy "Access control policy"

  @US-213 @iso27001-7.3 @integration
  Scenario: A member confirms a policy themself
    Given I upload the policy "Access control policy" version "3" for the organisation
    When "Bo" has confirmed the policy "Access control policy"
    Then "Bo" has read the policy "Access control policy"
    And "Anna" must read the policy "Access control policy"
    And the activity log has "PolicyConfirmed" saying "Bo read Access control policy version 3"

  @US-213 @iso27001-7.2 @integration
  Scenario: An expired training is missing
    Given the competence "Security awareness" must be renewed every year
    And "Bo" was trained in "Security awareness" on "2025-10-01"
    When I open the competence overview
    Then "Bo" is "Expired" in "Security awareness"

  @US-213 @iso27001-7.2 @integration
  Scenario: A valid training counts and a person without it is missing
    Given the competence "Security awareness" must be renewed every year
    And "Bo" was trained in "Security awareness" on "2026-10-01"
    When I open the competence overview
    Then "Bo" is "Valid" in "Security awareness"
    And "Anna" is "NotRequired" in "Security awareness"

  @US-213 @iso27001-7.2 @integration
  Scenario: A person without a user can be trained
    Given the competence "Clean desk" for people without a user
    And I add "Kim" who has no user
    When I open the competence overview
    Then "Kim" is "Missing" in "Clean desk"

  @US-213 @iso27001-7.2 @integration
  Scenario: A member who does not decide compliance reads the overview
    Given the competence "Security awareness" must be renewed every year
    And I am signed in as "Bo"
    When I open the competence overview
    Then the competence overview is read-only
