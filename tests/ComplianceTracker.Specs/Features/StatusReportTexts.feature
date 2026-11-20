Feature: AI-drafted texts in the management report

  # US-040: the AI writes a summary and a short text per regulation for the management report (US-141). The figures and
  # tables always come from the tracker. An AI text is marked AI-generated until a person accepts or edits it, and the
  # report can only be locked (US-050) when every text is accepted or edited and still fits today's figures.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the first 3 CRA requirements of "Acme Portal" are covered by the user story "[US-001] Secure defaults"
    And the AI writes the management summary "A few CRA requirements are covered."

  @US-040 @iso27001-9.3 @ai-act-50 @integration
  Scenario: The AI drafts the texts and they are marked
    When I draft the management report texts of "Acme Portal"
    Then the management report text "Summary" is "A few CRA requirements are covered."
    And the management report text "Summary" is marked "AI-generated"
    And the management report text "CRA" is marked "AI-generated"
    When I download the traceability report for "management" as PDF from the Reports tab
    Then the PDF contains "Summary (AI-generated)"
    And the PDF contains "3 of 72"

  @US-040 @ai-act-50 @integration
  Scenario: A report with AI texts that are not accepted cannot be locked
    Given I have drafted the management report texts of "Acme Portal"
    When I lock the traceability report of "Acme Portal" for "management"
    Then I see the error "Accept or edit every AI-generated text first"
    And "Acme Portal" has 0 locked reports

  @US-040 @iso27001-7.5.3 @integration
  Scenario: Accepted texts are locked with the report
    Given I have drafted the management report texts of "Acme Portal"
    And I have accepted every management report text
    When I lock the traceability report of "Acme Portal" for "management"
    Then "Acme Portal" has 1 locked reports
    And the activity log has "ReportDrafted" with "Summary, CRA"
    And the activity log has "ReportTextChanged" with "Accepted: Summary"

  @US-040 @ai-act-50 @integration
  Scenario: An edited text is no longer AI-generated
    Given I have drafted the management report texts of "Acme Portal"
    When I edit the management report text "Summary" to "Our own words about the status."
    Then the management report text "Summary" is "Our own words about the status."
    And the management report text "Summary" is not marked "AI-generated"
    And the management report text "CRA" is marked "AI-generated"

  @US-040 @iso27001-9.3 @integration
  Scenario: A text the figures have moved away from must be drafted or accepted again
    Given I have drafted the management report texts of "Acme Portal"
    And I have accepted every management report text
    And the first 5 CRA requirements of "Acme Portal" are covered by the user story "[US-001] Secure defaults"
    Then the management report text "CRA" is marked "Outdated: the figures changed"
    When I lock the traceability report of "Acme Portal" for "management"
    Then I see the error "The figures changed since a text was written: draft again or accept it again first"

  @US-040 @integration
  Scenario: Without the AI nothing is drafted and the report still works
    Given the local AI is not available
    When I draft the management report texts of "Acme Portal"
    Then the management report texts say "No texts:"
    And there are no management report texts
    When I lock the traceability report of "Acme Portal" for "management"
    Then "Acme Portal" has 1 locked reports

  @US-040 @iso27001-A.5.15 @integration
  Scenario: An auditor reads the texts but cannot draft or accept them
    Given I have drafted the management report texts of "Acme Portal"
    And "Bo" has joined "Acme ApS" as "Auditor"
    When "Bo" opens the Reports tab of "Acme Portal"
    Then there is no "Draft again with AI" button
    And there is no "Accept text" button
    And "Bo" cannot accept the management report text "Summary" of "Acme Portal"
