Feature: Upload Unified Process use cases

  # US-034: a project that describes its system with Unified Process use cases uploads its use case document.
  # Each use case becomes a work item of type UseCase with its sections. Its main success scenario and
  # postconditions are its acceptance criteria, so it can be mapped like a user story (R5).
  # Versions, matching on ID, the summary, logging and access work as in US-032.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-034 @iso27001-A.8.26 @integration
  Scenario: Use cases and their sections are read
    When I upload the use case document "use-cases.docx" to "Acme Portal" containing:
      """
      # UC-03 Register patient
      Primary actor: Nurse
      Preconditions: The nurse is signed in
      Main success scenario:
      1. The nurse opens the form
      2. The system checks the CPR number
      Postconditions: The patient is registered
      """
    Then the project has the use case "UC-03" with the name "Register patient"
    And the use case has the primary actor "Nurse" and 2 steps
    And its acceptance criteria contain "The patient is registered"

  @US-034 @integration
  Scenario: Danish section names are accepted
    When I upload the use case document "use-cases.docx" to "Acme Portal" containing:
      """
      # UC-03 Registrer patient
      Primær aktør: Sygeplejerske
      Hovedforløb:
      1. Sygeplejersken åbner formularen
      2. Systemet tjekker CPR-nummeret
      3. Systemet gemmer patienten
      Resultat: Patienten er registreret
      """
    Then the project has the use case "UC-03" with the name "Registrer patient"
    And the use case has the primary actor "Sygeplejerske" and 3 steps
    And its acceptance criteria contain "Patienten er registreret"

  @US-034 @integration
  Scenario: A use case without a main success scenario is skipped
    When I upload the use case document "use-cases.docx" with "UC-07 Print label" that only has a primary actor
    Then I see "UC-07 skipped: no main success scenario"

  @US-034 @iso27001-A.8.32 @integration
  Scenario: A new version updates use cases by ID
    Given "Acme Portal" has version 1 of "use-cases.docx" with "UC-03" and "UC-05"
    When I upload version 2 with only "UC-03"
    Then the use case "UC-05" is marked "Removed in version 2"
