Feature: NIS2 gap analysis from the organisation's documents

  # US-188: a consultant or compliance responsible uploads the organisation's documents (a ZIP or several files). Each
  # readable file becomes one of the organisation's documents (stored once, with its SHA-256); the AI suggests the NIS2
  # requirements it covers and quotes the sentence that fits; nothing counts until a person approves it.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And I have done the NIS2 scope check as an important entity
    And the minimum score for evidence suggestions is 0.5

  @US-188 @nis2-art21 @integration
  Scenario: A ZIP is read, and a file the tracker cannot read is listed with the reason
    Given the AI rates "Incident response plan" against "Art. 21(2)(b)" with 0.9
    When I upload to the NIS2 gap analysis the ZIP "acme.zip" with "Incident response plan.txt" saying "Purpose of the plan.\nIncidents are reported to the security team and handled within 24 hours." and "photo.heic"
    Then the gap upload says "1 added, 0 already uploaded, 1 not read"
    And the gap upload lists "photo.heic" as "NotRead" saying "Only PDF, Word, Excel"
    And the gap analysis suggests "Art. 21(2)(b)" for "Incident response plan" quoting "Incidents are reported to the security team"

  @US-188 @nis2-art21 @integration
  Scenario: A suggestion counts only when a person approves it
    Given the AI rates "Incident response plan" against "Art. 21(2)(b)" with 0.9
    And I have uploaded to the NIS2 gap analysis "Incident response plan.txt" saying "Incidents are handled within 24 hours."
    Then the organisation's Compliance tab for "NIS2" shows "Art. 21(2)(b)" as "Not covered"
    When I approve the gap suggestion "Art. 21(2)(b)" for "Incident response plan"
    Then the organisation's Compliance tab for "NIS2" shows "Art. 21(2)(b)" as "Covered"
    And the gap analysis has no suggestions

  @US-188 @nis2-art21 @integration
  Scenario: A Word document is read
    Given the AI rates "Backup procedure" against "Art. 21(2)(c)" with 0.9
    When I upload to the NIS2 gap analysis the Word document "Backup procedure.docx" saying "A daily backup is copied to a second site and restored once a year."
    Then the gap analysis suggests "Art. 21(2)(c)" for "Backup procedure" quoting "A daily backup is copied to a second site"

  @US-188 @iso27001-A.5.33 @integration
  Scenario: The same file is recognised and stored once
    Given I have uploaded to the NIS2 gap analysis "Incident response plan.txt" saying "Incidents are handled within 24 hours."
    When I upload to the NIS2 gap analysis "Copy of the plan.txt" saying "Incidents are handled within 24 hours."
    Then the gap upload lists "Copy of the plan.txt" as "AlreadyUploaded" saying "The same file as \"Incident response plan\""
    And the organisation has 1 document

  @US-188 @integration
  Scenario: Without the AI the documents are still added
    Given the local AI is not available
    When I upload to the NIS2 gap analysis "Incident response plan.txt" saying "Incidents are handled within 24 hours."
    Then the gap upload lists "Incident response plan.txt" as "Added" saying "the AI could not be reached"

  @US-188 @iso27001-A.5.15 @integration
  Scenario: A developer cannot upload
    Given "Carl" has joined "Acme ApS" as "Developer"
    And I am signed in as "Carl"
    When I open the NIS2 gap analysis
    Then the gap analysis has no upload form
