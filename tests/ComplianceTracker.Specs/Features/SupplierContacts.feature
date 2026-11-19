Feature: Contact details for suppliers

  # US-207: a supplier has the company's details and the people the organisation can write to, including whom to
  # contact about security. "Write" opens the member's own e-mail program; the tracker sends nothing.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Demo ApS"
    And I am signed in as "Anna"

  @US-207 @nis2-art21 @iso27001-A.5.19 @integration
  Scenario: A supplier gets company details and a security contact
    When I add the supplier "CloudHost ApS" with the CVR "12345678" and the country "Denmark"
    And I add the contact person "Mette Lund" to "CloudHost ApS" as security contact with the e-mail "mette@cloudhost.example"
    Then the supplier "CloudHost ApS" shows the CVR "12345678" and the country "Denmark"
    And the supplier "CloudHost ApS" shows "Mette Lund" as security contact
    And "Write" to "Mette Lund" opens an e-mail to "mette@cloudhost.example" with the subject "Security – Demo ApS"
    And the activity log has "SupplierContactAdded" saying "CloudHost ApS: Mette Lund (Security contact), primary, security contact"
    And no activity log entry contains "mette@cloudhost.example"

  @US-207 @nis2-art21 @integration
  Scenario: A critical supplier without a security contact is marked
    Given I have added the supplier "CloudHost ApS" delivering "Hosting" with criticality "High" and the rule "NIS2 Art. 21(2)(d)"
    And I add the contact person "Jens Hansen" to "CloudHost ApS" as "Account manager" with the e-mail "jens@cloudhost.example"
    When I open the suppliers
    Then "CloudHost ApS" is marked "No security contact"
    And the supplier "CloudHost ApS" has "Jens Hansen" as primary contact

  @US-207 @integration
  Scenario: A new primary contact takes over, and the primary is asked for evidence that is missing
    Given I have added the supplier "CloudHost ApS" delivering "Hosting" with criticality "High" and the rule "NIS2 Art. 21(2)(d)"
    And I add the contact person "Jens Hansen" to "CloudHost ApS" as "Account manager" with the e-mail "jens@cloudhost.example"
    When I add the contact person "Mette Lund" to "CloudHost ApS" as primary with the e-mail "mette@cloudhost.example"
    Then the supplier "CloudHost ApS" has "Mette Lund" as primary contact
    And "CloudHost ApS" offers to ask "Mette Lund" for updated evidence

  @US-207 @integration
  Scenario: An e-mail address that is not valid is refused
    Given I have added the supplier "CloudHost ApS" delivering "Hosting" with criticality "High" and the rule "NIS2 Art. 21(2)(d)"
    When I add the contact person "Jens Hansen" to "CloudHost ApS" as "Account manager" with the e-mail "jens at cloudhost"
    Then I see the error "Not a valid e-mail address: jens at cloudhost"
