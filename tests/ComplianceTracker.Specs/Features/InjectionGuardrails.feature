Feature: Guardrails against injection

  # US-094: the tracker has no raw SQL, no unencoded output, no processes and no redirects from input today.
  # These checks read the source code on every build, so it stays that way. An exception must be on the
  # reviewed list in InjectionGuardrailsStepDefinitions, with its reason (docs/sikker-kode.md).

  Background:
    When the app's source code is read

  @US-094 @asvs-V5.3.4 @iso27001-A.8.28
  Scenario: No raw SQL in the app
    Then no code builds SQL as text

  @US-094 @asvs-V5.3.3 @iso27001-A.8.28
  Scenario: No unencoded output in the views
    Then no view writes unencoded HTML

  @US-094 @asvs-V5.3.8 @iso27001-A.8.28
  Scenario: No processes are started
    Then no code starts a process

  @US-094 @asvs-V5.1.5 @iso27001-A.8.28
  Scenario: Redirects only go to the app's own pages
    Then no code redirects to an address from input

  @US-094 @asvs-V12.3.1 @iso27001-A.8.28
  Scenario: Uploaded file names never become paths
    Then no code reads or writes files on the server

  @US-094 @asvs-V5.3.3 @iso27001-A.8.28
  Scenario: The page scripts do not run text as code
    Then no script uses eval, new Function or HTML from input
