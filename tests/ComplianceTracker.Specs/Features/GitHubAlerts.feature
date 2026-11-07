Feature: Fetch security alerts from GitHub
  US-121: the tracker fetches the code scanning and Dependabot alerts GitHub already has for the project's repository,
  read-only, so the Scans tab is up to date without uploading SARIF files. Fixed alerts become fixed findings, and
  alerts dismissed in GitHub become accepted with GitHub's reason.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "Acme Portal" is linked to "acme/portal"

  @US-121 @iso27001-A.8.8 @integration
  Scenario: Code scanning alerts become findings
    Given GitHub has the open "CodeQL" alert 7 "js/xss" with the severity "high" for "acme/portal"
    And GitHub has the open "CodeQL" alert 8 "cs/sql-injection" with the severity "critical" for "acme/portal"
    When I fetch the security alerts of "Acme Portal" from GitHub
    Then the fetch says "CodeQL: 2 new, 0 fixed, 2 open (1 critical)"
    And the finding "cs/sql-injection" is open with the severity "Critical"
    And the latest "CodeQL" scan comes from "GitHub code scanning"

  @US-121 @cra-annexI @integration
  Scenario: An alert fixed in GitHub is fixed here too
    Given GitHub has the open "CodeQL" alert 7 "js/xss" with the severity "high" for "acme/portal"
    And I have fetched the security alerts of "Acme Portal" from GitHub
    And the alert 7 of "acme/portal" is fixed in GitHub
    When I fetch the security alerts of "Acme Portal" from GitHub
    Then the fetch says "CodeQL: 0 new, 1 fixed, 0 open"
    And the finding "js/xss" is marked "Fixed in scan of" today

  @US-121 @iso27001-A.8.8 @integration
  Scenario: An alert dismissed in GitHub is accepted with GitHub's reason
    Given GitHub has the open "CodeQL" alert 7 "js/xss" with the severity "high" for "acme/portal"
    And I have fetched the security alerts of "Acme Portal" from GitHub
    And the alert 7 of "acme/portal" is dismissed in GitHub as "false positive"
    When I fetch the security alerts of "Acme Portal" from GitHub
    Then the finding "js/xss" is marked "Accepted: Dismissed in GitHub: false positive"

  @US-121 @cra-annexI @integration
  Scenario: Dependabot alerts become findings
    Given GitHub has the open Dependabot alert 3 "CVE-2026-5555" with the severity "high" for "acme/portal"
    When I fetch the security alerts of "Acme Portal" from GitHub
    Then the fetch says "Dependabot: 1 new, 0 fixed, 1 open"
    And the finding "CVE-2026-5555" is open with the severity "High"

  @US-121 @integration
  Scenario: Dependabot switched off does not stop code scanning
    Given GitHub has the open "CodeQL" alert 7 "js/xss" with the severity "high" for "acme/portal"
    And Dependabot alerts are switched off for "acme/portal"
    When I fetch the security alerts of "Acme Portal" from GitHub
    Then the fetch says "Dependabot alerts are switched off for this repository. Switch them on under Settings → Code security."
    And the fetch says "CodeQL: 1 new, 0 fixed, 1 open"

  @US-121 @integration
  Scenario: A project without a repository has nothing to fetch
    Given the project "Beta App" exists
    When I open the Scans tab of "Beta App"
    Then there is no "Fetch from GitHub" button
