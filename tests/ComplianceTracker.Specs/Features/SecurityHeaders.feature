Feature: Security headers

  # US-087: every response tells the browser what the page may load and do. The pages have no inline
  # scripts or styles, so the Content-Security-Policy needs no 'unsafe-inline'.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-087 @asvs-V14.4 @iso27001-A.8.26 @integration
  Scenario: A page has the security headers
    When I open the address "/Projects"
    Then the response has a Content-Security-Policy with "frame-ancestors 'none'"
    And the Content-Security-Policy has no "unsafe-inline" and no "unsafe-eval"
    And the response has "X-Content-Type-Options" "nosniff"
    And the response has "Referrer-Policy" "strict-origin-when-cross-origin"
    And the response has "Permissions-Policy" "camera=(), microphone=(), geolocation=()"

  @US-087 @asvs-V14.4 @integration
  Scenario: Files and error pages have the headers too
    When I open the address "/css/site.css"
    Then the response has a Content-Security-Policy with "default-src 'self'"
    When I open the address "/Projects/Details/00000000-0000-0000-0000-000000000001"
    Then the response has a Content-Security-Policy with "default-src 'self'"

  @US-087 @integration
  Scenario: Signing in and out may go to Keycloak
    When I open the address "/Projects"
    Then the Content-Security-Policy lets forms go to Keycloak

  @US-087 @asvs-V14.4 @integration
  Scenario: The pages have nothing inline that the policy would block
    When I open every tab of "Acme Portal" and the frameworks page
    Then no page has an inline script, a style attribute or an event handler attribute
