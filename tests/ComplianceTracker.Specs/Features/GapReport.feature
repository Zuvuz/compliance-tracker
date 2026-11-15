Feature: NIS2 gap report with an action plan

  # US-190: each NIS2 measure is met, partly met or missing, from the approved evidence (US-188) and the answers
  # (US-189). The gaps form an action plan ordered by priority; the report is a locked PDF for management to approve, and
  # the next report shows what changed.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And I have done the NIS2 scope check as an important entity
    And the minimum score for evidence suggestions is 0.5

  @US-190 @nis2-art20 @integration
  Scenario: The page shows each measure and the actions
    Given the AI rates "Backup procedure" against "Art. 21(2)(c)" with 0.9
    And I have uploaded to the NIS2 gap analysis "Backup procedure.txt" saying "A daily backup is copied to a second site."
    And I have approved the gap suggestion "Art. 21(2)(c)" for "Backup procedure"
    And I have answered "Art. 21(2)(d)" with "AdHoc" and ""
    And I have answered "Art. 21(2)(h)" with "Defined" and ""
    When I open the NIS2 gap analysis
    Then the measure "Art. 21(2)(c)" is "Met"
    And the measure "Art. 21(2)(h)" is "Partly met"
    And the measure "Art. 21(2)(d)" is "Missing"
    And the action plan lists "Art. 21(2)(d)" with the priority "High"
    And the action plan lists "Art. 21(2)(h)" with the priority "Medium"
    And the action plan does not list "Art. 21(2)(c)"

  @US-190 @nis2-art20 @iso27001-A.5.33 @integration
  Scenario: The report is locked with its fingerprint and lists the measures
    Given I have answered "Art. 21(2)(d)" with "AdHoc" and ""
    When I make the NIS2 gap report
    Then the gap report is listed with a fingerprint
    And the gap report's PDF says "Approved by the management"
    And the gap report's PDF says "List the critical suppliers"

  @US-190 @iso27001-9.3 @integration
  Scenario: A new report shows what improved
    Given I have answered "Art. 21(2)(d)" with "AdHoc" and ""
    And I have made the NIS2 gap report
    When I answer "Art. 21(2)(d)" with "Defined" and "Supplier list made"
    Then the measure "Art. 21(2)(d)" changed "Better" since the last report

  @US-190 @nis2-art21 @integration
  Scenario: A gap becomes a risk in the organisation area
    When I open the NIS2 gap analysis
    And I make the gap "Art. 21(2)(d)" a risk
    Then the risk form is filled in with "NIS2 Art. 21(2)(d): List the critical suppliers"
