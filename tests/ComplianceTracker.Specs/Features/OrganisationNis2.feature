Feature: NIS2 at organisation level

  # US-142: an organisation area in the top bar, where the whole company follows NIS2 (with the Danish NIS2 law and,
  # for some digital providers, Implementing Regulation (EU) 2024/2690) with the same structure as a project.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-142 @nis2-art3 @integration
  Scenario: The organisation area is in the top bar and is not a project
    When I open the front page
    Then the top bar has the links "Organisation" and "Frameworks"
    When I open "Organisation" from the top bar
    Then I see the organisation area of "Acme ApS" with the tabs "Overview", "NIS2 scope", "Compliance" and "Reports"
    And the Projects page does not list "Organisation"

  @US-142 @nis2-art3 @integration
  Scenario: The scope check finds an important entity and switches NIS2 on with the Danish law
    When I do the NIS2 scope check with sector "Manufacturing", 60 employees and a turnover of 12 million euro
    Then the NIS2 result is "Important" with the reason "Annex II, medium-sized"
    And the organisation follows "NIS2 – Directive (EU) 2022/2555" and "NIS 2-loven – Lov nr. 434 af 6. maj 2025 (Denmark)"
    And the organisation does not follow "NIS2 technical measures – Implementing Regulation (EU) 2024/2690"
    And the activity log has "Nis2ScopeChecked" saying "Important: Annex II, medium-sized"

  @US-142 @nis2-art3 @integration
  Scenario: A small company is not covered, and NIS2 is not switched on
    When I do the NIS2 scope check with sector "Manufacturing", 20 employees and a turnover of 3 million euro
    Then the NIS2 result is "NotCovered" with the reason "Annex II, but small: under the size limit (Art. 2(1))"
    And the organisation follows no framework
    And the page says "You can still follow NIS2 by choice"

  @US-142 @nis2-art3 @integration
  Scenario: A medium-sized cloud provider is important and gets the technical measures too
    When I do the NIS2 scope check with sector "DigitalInfrastructure" and service "CloudComputing", 120 employees and a turnover of 30 million euro
    Then the NIS2 result is "Important" with the reason "Annex I, medium-sized"
    And the organisation follows "NIS2 technical measures – Implementing Regulation (EU) 2024/2690"

  @US-142 @nis2-art3 @integration
  Scenario: Outside Denmark the Danish law is not switched on
    Given the organisation's country is set to "DE" on the Settings page
    When I do the NIS2 scope check with sector "Manufacturing", 60 employees and a turnover of 12 million euro
    Then the organisation follows "NIS2 – Directive (EU) 2022/2555"
    And the organisation does not follow "NIS 2-loven – Lov nr. 434 af 6. maj 2025 (Denmark)"
    And the activity log has "OrganisationCountryChanged" saying "DE"

  @US-142 @nis2-art21 @integration
  Scenario: A NIS2 measure is covered by an organisation document, and the Danish law through it
    Given I have done the NIS2 scope check as an important entity
    And I have uploaded the policy "Incident response plan" version "v1" with the content "How we handle incidents"
    When I link the document "Incident response plan" to "Art. 21(2)(b)"
    Then the organisation's Compliance tab for "NIS2" shows "Art. 21(2)(b)" as "Covered"
    And the organisation's Compliance tab for "NIS 2-loven" shows "§ 6, stk. 1, nr. 2" as "Covered" through "NIS2 Art. 21(2)(b)"
    And the organisation's Compliance tab for "NIS 2-loven" shows that "§ 6, stk. 1, nr. 2" implements "NIS2 Art. 21(2)(b)"

  @US-142 @integration
  Scenario: An organisation-level framework cannot be used on a project
    Given I have done the NIS2 scope check as an important entity
    And the project "Acme Portal" exists
    When I try to add "NIS2 – Directive (EU) 2022/2555" to "Acme Portal"
    Then I see the error "NIS2 applies to the organisation, not to a project"
    And the Frameworks tab of "Acme Portal" does not offer "NIS2 – Directive (EU) 2022/2555"

  @US-142 @integration
  Scenario: Texts follow the page language with English as fallback
    Given I have done the NIS2 scope check as an important entity
    When I open the organisation's Compliance tab for "NIS2" in "da"
    Then the requirement "Art. 21(2)(b)" is titled "Håndtering af hændelser"
    When I open the organisation's Compliance tab for "NIS2" in "en"
    Then the requirement "Art. 21(2)(b)" is titled "Incident handling"
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    And the project "Acme Portal" exists
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    When I open the Compliance tab of "Acme Portal" in "da"
    Then the page says "Not translated yet: the requirements are shown in English."

  @US-142 @integration
  Scenario: Only members who decide on compliance can do the scope check
    Given "Bo" is in "Acme ApS" with the role "Developer"
    And I am signed in as "Bo"
    When I open the NIS2 scope tab
    Then there is no NIS2 scope check form
    When I post the NIS2 scope check anyway
    Then the NIS2 result is "none"
