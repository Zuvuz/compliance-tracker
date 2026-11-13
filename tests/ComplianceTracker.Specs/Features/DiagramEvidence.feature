Feature: Diagrams as evidence

  # US-149: a diagram in draw.io, Mermaid, PlantUML, BPMN or SVG can be uploaded as evidence. The tracker reads its
  # elements, links and labels (nothing in the file is run) and uses them as the evidence's text for the AI's
  # suggestions, the evidence check and search. A diagram that cannot be read is still kept, with the reason.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-149 @cra-AnnexVII @iso27001-A.8.27 @integration
  Scenario: A draw.io diagram is read
    When I upload the diagram "architecture.drawio" with "Web app", "API" and "Database"
    Then the evidence "architecture.drawio" says "Diagram: 3 elements, 2 links"
    And the evidence "architecture.drawio" lists the link "Web app → API: HTTPS"
    And the activity log has "EvidenceAdded" with "architecture.drawio (diagram: 3 elements, 2 links)"

  @US-149 @integration
  Scenario: A Mermaid diagram is read
    When I upload the diagram "flow.mmd" with the text:
      """
      flowchart LR
        A[Browser] -->|patient data| B(API)
        B --> C[(Database)]
      """
    Then the evidence "flow.mmd" says "Diagram: 3 elements, 2 links"
    And the evidence "flow.mmd" lists the link "Browser → API: patient data"

  @US-149 @asvs-V5 @integration
  Scenario: Nothing in a diagram is run
    When I upload the diagram "flow.svg" with the text:
      """
      <svg xmlns="http://www.w3.org/2000/svg"><script>alert('pwned')</script><text>Login</text><text>Check MFA</text></svg>
      """
    Then the evidence "flow.svg" lists the element "Login"
    And the Evidence tab does not show "alert('pwned')"

  @US-149 @integration
  Scenario: A diagram that cannot be read is still kept
    When I upload the diagram "process.bpmn" with the text:
      """
      <definitions><process><task name="Approve"
      """
    Then the evidence "process.bpmn" says "The diagram could not be read"
    And the evidence "process.bpmn" explains "The diagram could not be read: the XML is not well-formed"
