Feature: Security events suggested from GitHub alerts and monitoring
  US-160: serious alerts GitHub already has (Dependabot, code scanning, secret scanning) and alerts from the
  organisation's own monitoring become suggested security events. Nothing becomes an event by itself: a person records
  or dismisses each suggestion, and recording starts the CRA Art. 14 clock.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "Acme Portal" is linked to "acme/portal"

  @US-160 @cra-art14 @integration
  Scenario: A high Dependabot alert is suggested with its package
    Given GitHub has the open Dependabot alert 3 "CVE-2026-1234" with the severity "high" for "acme/portal"
    When I fetch the security alerts of "Acme Portal" from GitHub
    Then the Security events tab suggests "Alert in Acme Portal: Vulnerability in left-pad (CVE-2026-1234)"
    And the Security events tab suggests "CVE-2026-1234" from "Dependabot"

  @US-160 @cra-art14 @integration
  Scenario: A medium alert is not suggested
    Given GitHub has the open Dependabot alert 3 "CVE-2026-1234" with the severity "medium" for "acme/portal"
    When I fetch the security alerts of "Acme Portal" from GitHub
    Then nothing is suggested for "Acme Portal"

  @US-160 @iso27001-A.5.24 @integration
  Scenario: A leaked secret is suggested as an incident
    Given GitHub has the open secret scanning alert 5 "github_personal_access_token" named "GitHub Personal Access Token" for "acme/portal"
    When I fetch the security alerts of "Acme Portal" from GitHub
    Then the fetch says "Secret scanning: 1 new, 0 fixed, 1 open (1 critical)"
    And the Security events tab suggests "Alert in Acme Portal: Leaked secret: GitHub Personal Access Token"

  @US-160 @iso27001-A.8.16 @integration
  Scenario: A monitoring alert is suggested while it fires and closed when it is resolved
    Given I have created the upload token "Monitoring" for "Acme Portal"
    When the monitoring of "Acme Portal" sends
      """
      {"alerts":[{"id":"abc","name":"HighErrorRate","status":"firing","severity":"critical","summary":"Errors above 5%"}]}
      """
    Then the answer is 200
    And the Security events tab suggests "Alert in Acme Portal: HighErrorRate: Errors above 5%"
    When the monitoring of "Acme Portal" sends
      """
      {"alerts":[{"id":"abc","name":"HighErrorRate","status":"resolved","severity":"critical"}]}
      """
    Then the suggestion "monitoring:abc" was closed with "Resolved at the source"
    And nothing is suggested for "Acme Portal"

  @US-160 @iso27001-A.8.16 @integration
  Scenario: Alertmanager's webhook is understood
    Given I have created the upload token "Monitoring" for "Acme Portal"
    When the monitoring of "Acme Portal" sends
      """
      {"version":"4","status":"firing","alerts":[{"status":"firing","labels":{"alertname":"DiskFull","severity":"high"},"annotations":{"summary":"Disk 98% full"},"fingerprint":"f00d"}]}
      """
    Then the answer is 200
    And the Security events tab suggests "monitoring:f00d" from "Monitoring"

  @US-160 @iso27001-A.5.15 @integration
  Scenario: A revoked token is refused and nothing is suggested
    Given I have created the upload token "Monitoring" for "Acme Portal"
    And I have revoked the upload token "Monitoring"
    When the monitoring of "Acme Portal" sends
      """
      {"alerts":[{"id":"abc","name":"HighErrorRate","status":"firing","severity":"critical"}]}
      """
    Then the answer is 401
    And nothing is suggested for "Acme Portal"

  @US-160 @iso27001-A.8.16 @integration
  Scenario: An alert in an unknown shape is refused and logged without its content
    Given I have created the upload token "Monitoring" for "Acme Portal"
    When the monitoring of "Acme Portal" sends
      """
      {"message":"secret-looking content"}
      """
    Then the answer is 400
    And the activity log has "SecurityAlertRefused" with "not a known shape"
    And no activity entry contains "secret-looking content"

  @US-160 @iso27001-A.8.16 @integration
  Scenario: An alert larger than 256 KB is refused
    Given I have created the upload token "Monitoring" for "Acme Portal"
    When the monitoring of "Acme Portal" sends 300 KB
    Then the answer is 400
    And the activity log has "SecurityAlertRefused" with "larger than 256 KB"
