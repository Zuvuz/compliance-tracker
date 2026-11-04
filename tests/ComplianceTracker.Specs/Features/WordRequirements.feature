Feature: Upload a requirement specification as a Word document

  # US-033: a requirement specification written in Word is uploaded as it is.
  # A requirement starts at a heading or paragraph that begins with an ID matching the pattern.
  # Versions, matching on ID, the summary, logging and access work as in US-032.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-033 @iso27001-A.8.26 @integration
  Scenario: Requirements are read from headings with an ID
    When I upload the Word document "kravspec.docx" to "Acme Portal" containing:
      """
      # REQ-012 The user can log in
      Login uses MitID. This replaces REQ-004.
      Acceptance criteria:
      A citizen with MitID is signed in.
      """
    Then the project has the requirement "REQ-012" with the title "The user can log in"
    And its acceptance criteria are "A citizen with MitID is signed in."
    And the project has no requirement "REQ-004"

  @US-033 @integration
  Scenario: A different ID pattern can be chosen
    When I upload the Word document "kravspec.docx" with the heading "K-1.2 Export data"
    Then I see the error "No requirements found. Check the ID pattern"
    When I upload it again with the ID pattern "K-\d+\.\d+"
    Then the project has the requirement "K-1.2"

  @US-033 @iso27001-A.8.32 @integration
  Scenario: A new version of the document updates by ID
    Given "Acme Portal" has version 1 of "kravspec.docx" with "REQ-004" and "REQ-012"
    When I upload version 2 with only "REQ-012"
    Then the requirement "REQ-004" is marked "Removed in version 2"

  @US-033 @integration
  Scenario: Old Word files are refused
    When I upload the requirement specification "kravspec.doc" to "Acme Portal"
    Then I see the error "Only .xlsx, .csv and .docx files can be uploaded"

  @US-033 @integration
  Scenario: An ID pattern that is not a regular expression is refused
    When I upload the Word document "kravspec.docx" with the heading "K-1 Log in"
    And I upload it again with the ID pattern "K-(\d+"
    Then I see the error "The ID pattern is not a valid regular expression"
    And the project has no requirements
