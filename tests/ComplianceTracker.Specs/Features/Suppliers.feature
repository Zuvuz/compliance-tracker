Feature: Register suppliers and follow up that they meet NIS2 and other rules

  # US-191: the organisation's suppliers with what they deliver, how critical they are and which rules they must meet for
  # the organisation. High and medium criticality need valid evidence (certificates, data processing agreements, …) with
  # end dates; the list is part of the NIS2 gap report.

  Background:
    Given an empty database
    And the time is "2026-11-15T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-191 @nis2-art21 @iso27001-A.5.19 @integration
  Scenario: A critical supplier without valid evidence is shown as missing
    Given I have added the supplier "Hosting ApS" delivering "Hosting" with criticality "High" and the rule "NIS2 Art. 21(2)(d)"
    And "Hosting ApS" has the evidence "ISO/IEC 27001 certificate" valid until "2026-10-01"
    When I open the suppliers
    Then the supplier "Hosting ApS" is "Missing" saying "ISO/IEC 27001 certificate expired"
    And the activity log has "SupplierAdded" saying "Hosting ApS (High)"

  @US-191 @iso27001-A.5.20 @gdpr-28 @integration
  Scenario: Valid evidence makes the supplier OK, and one that ends soon is shown
    Given I have added the supplier "Mail ApS" delivering "E-mail" with criticality "Medium" and the rule "GDPR Art. 28"
    And "Mail ApS" has the evidence "Data processing agreement" valid until "2027-11-15"
    And I have added the supplier "Backup ApS" delivering "Off-site backup" with criticality "High" and the rule "NIS2 Art. 21(2)(d)"
    And "Backup ApS" has the evidence "SOC 2 report" valid until "2026-12-01"
    When I open the suppliers
    Then the supplier "Mail ApS" is "Ok" saying "1 valid"
    And the supplier "Backup ApS" is "ExpiresSoon" saying "Evidence ends 01-12-2026"

  @US-191 @integration
  Scenario: A supplier with low criticality needs no evidence
    Given I have added the supplier "Coffee ApS" delivering "Coffee" with criticality "Low" and the rule "GDPR Art. 28"
    When I open the suppliers
    Then the supplier "Coffee ApS" is "Ok" saying "Low criticality: no evidence needed"

  @US-191 @nis2-art21 @integration
  Scenario: The suppliers are part of the NIS2 gap report
    Given I have done the NIS2 scope check as an important entity
    And I have added the supplier "Hosting ApS" delivering "Hosting" with criticality "High" and the rule "NIS2 Art. 21(2)(d)"
    When I make the NIS2 gap report
    Then the gap report's PDF says "Hosting ApS"

  @US-191 @iso27001-A.5.15 @integration
  Scenario: Another organisation cannot see or download a supplier's evidence
    Given I have added the supplier "Hosting ApS" delivering "Hosting" with criticality "High" and the rule "NIS2 Art. 21(2)(d)"
    And "Hosting ApS" has the evidence "ISO/IEC 27001 certificate" valid until "2027-10-01"
    And "Bo" has created the organisation "Beta A/S"
    When "Bo" tries to download the evidence "ISO/IEC 27001 certificate" of "Hosting ApS"
    Then "Bo" gets "not found"

  @US-191 @iso27001-A.5.15 @integration
  Scenario: A developer sees the suppliers but cannot add them
    Given "Carl" has joined "Acme ApS" as "Developer"
    And I am signed in as "Carl"
    When I open the suppliers
    Then there is no form to add a supplier
