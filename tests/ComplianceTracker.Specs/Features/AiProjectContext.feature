Feature: The AI uses the project's purpose, scope, risks and classification
  US-060: the AI's rationales take the product's intended purpose, scope and open risks into account, requirements
  tied to a High or Critical risk come first, and the AI can suggest which requirements do not apply. A person decides.
  US-061: the AI also uses the project's classification under the requirement's regulation.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in with Keycloak"

  @US-060 @US-061 @ai-act-art13 @cra-art13 @integration
  Scenario: The rationale names the context it used
    Given "Acme Portal" is classified under "CRA – Regulation (EU) 2024/2847" as "Important, class I"
    And the risk "Stolen sessions" exists in "Acme Portal"
    And the risk "Stolen sessions" has the requirement "I.2(d)" as a measure
    And the row "I.2(d)" on the "CRA" sub-tab has the suggestion "[US-004] Sign in with Keycloak"
    Then the context used for the suggestion on the row "I.2(d)" becomes "purpose, classification, risk 'Stolen sessions'"
    And the AI was told "Important, class I"
    And the AI was told "Stolen sessions (High)"

  @US-060 @iso27001-6.1.3 @integration
  Scenario: Requirements tied to a High risk are marked
    Given the risk "Stolen sessions" exists in "Acme Portal"
    And the risk "Stolen sessions" has the requirement "I.2(d)" as a measure
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" is marked "High risk"
    And the row "I.2(e)" is not marked "High risk"

  @US-060 @ai-act-art14 @cra-art13 @integration
  Scenario: The AI suggests a requirement does not apply, and a person accepts it
    Given the AI says "I.2(k)" does not apply because "The product has no network interface"
    When I ask for "not applicable" suggestions on the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(k)" suggests "not applicable" because "The product has no network interface"
    When I accept the "not applicable" suggestion on the row "I.2(k)" with the reason "No network interface (intended purpose)"
    Then the row "I.2(k)" is "Not applicable" with the reason "No network interface (intended purpose)"

  @US-060 @ai-act-art14 @integration
  Scenario: Rejecting keeps the requirement applicable
    Given the AI says "I.2(k)" does not apply because "The product has no network interface"
    When I ask for "not applicable" suggestions on the "CRA" sub-tab of "Acme Portal"
    And I reject the "not applicable" suggestion on the row "I.2(k)"
    Then the row "I.2(k)" is "Not covered"
    And the row "I.2(k)" has no "not applicable" suggestion

  @US-060 @cra-art13 @integration
  Scenario: A requirement tied to an open risk is never suggested as not applicable
    Given the AI says "I.2(k)" does not apply because "The product has no network interface"
    And the risk "Network attack" exists in "Acme Portal"
    And the risk "Network attack" has the requirement "I.2(k)" as a measure
    When I ask for "not applicable" suggestions on the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(k)" has no "not applicable" suggestion

  @US-061 @cra-art7 @integration
  Scenario: A new classification replaces pending suggestions and asks to check decisions based on the old one
    Given "Acme Portal" is classified under "CRA – Regulation (EU) 2024/2847" as "Important, class I"
    And the AI says "I.2(k)" does not apply because "No network interface for a class I product"
    And the AI says "I.2(i)" does not apply because "No effect on other networks"
    And I have asked for "not applicable" suggestions on the "CRA" sub-tab of "Acme Portal"
    And I have accepted the "not applicable" suggestion on the row "I.2(k)"
    When I classify "Acme Portal" under "CRA – Regulation (EU) 2024/2847" as "Critical" because "Annex IV: a smart meter gateway"
    And I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(k)" says it is based on an earlier classification
    And the row "I.2(i)" has no "not applicable" suggestion
    When "Anna" opens the "Overview" tab of "Acme Portal"
    Then "Needs attention" says "1 not applicable requirement to check after a new classification"

  @US-061 @integration
  Scenario: Keeping a decision after a new classification
    Given "Acme Portal" is classified under "CRA – Regulation (EU) 2024/2847" as "Important, class I"
    And the AI says "I.2(k)" does not apply because "No network interface"
    And I have asked for "not applicable" suggestions on the "CRA" sub-tab of "Acme Portal"
    And I have accepted the "not applicable" suggestion on the row "I.2(k)"
    And I classify "Acme Portal" under "CRA – Regulation (EU) 2024/2847" as "Critical" because "Annex IV"
    When I keep "I.2(k)" not applicable on the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(k)" is "Not applicable" with the reason "No network interface"
    And the row "I.2(k)" does not say it is based on an earlier classification

  @US-060 @integration
  Scenario: Without the AI nothing is suggested
    Given the local AI is not available
    When I ask for "not applicable" suggestions on the "CRA" sub-tab of "Acme Portal"
    Then I see the error "The local AI is not available. Start Ollama (or the configured model server) and try again"
