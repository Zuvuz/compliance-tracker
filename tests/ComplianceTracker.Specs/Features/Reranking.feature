Feature: Rerank the AI's candidates

  # US-118: with a model set for the Ranking task, the search's best 20 candidates are read together with the
  # requirement by a reranker, which decides the order and the score; the limit of US-105 applies to its score, and only
  # the kept ones get a rationale. The suggestion shows both scores. Without a reranker nothing changes. The AI and the
  # reranker are the pretend ones.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has the active requirements "SP-1 Users must sign in securely" and "SP-2 Backups"
    And the project "Acme Portal" exists
    And "Acme Portal" uses "Security policy" version 1
    And the project "Acme Portal" has the user story "[US-007] Dark mode"
    And the project "Acme Portal" has the user story "[US-041] Session timeout"
    And the minimum score for story suggestions is 0.5
    And the AI rates "[US-007] Dark mode" against "SP-1" with 0.7
    And the AI rates "[US-041] Session timeout" against "SP-1" with 0.6

  @US-118 @aiact-art15 @aiact-art13 @integration
  Scenario: The reranker decides the order
    Given a reranker is configured
    And the reranker scores "[US-041] Session timeout" against "SP-1" with 0.86
    And the reranker scores "[US-007] Dark mode" against "SP-1" with 0.55
    When I ask for suggestions on the "Security policy" sub-tab of "Acme Portal"
    Then the suggestions on "SP-1" are "[US-041] Session timeout" and then "[US-007] Dark mode"
    And the suggestion "[US-041] Session timeout" on "SP-1" shows the score "0.86" and "(search 0.60)"
    And the activity log has "MappingsSuggested" with "(fake-embed + fake-rerank, "

  @US-118 @aiact-art15 @iso27001-A.5.36 @integration
  Scenario: Weak candidates are dropped
    Given a reranker is configured
    And the reranker scores "[US-007] Dark mode" against "SP-1" with 0.12
    And the reranker scores "[US-041] Session timeout" against "SP-1" with 0.86
    When I click "Suggest mappings" for "Acme Portal"
    Then "[US-007] Dark mode" has no suggestion "SP-1"
    And the suggestion "[US-041] Session timeout" on "SP-1" shows the score "0.86" and "(search 0.60)"

  @US-118 @aiact-art15 @integration
  Scenario: A weak search candidate can still be kept by the reranker
    Given a reranker is configured
    And the AI rates "[US-041] Session timeout" against "SP-1" with 0.3
    And the reranker scores "[US-041] Session timeout" against "SP-1" with 0.8
    When I click "Suggest mappings" for "Acme Portal"
    Then the suggestion "[US-041] Session timeout" on "SP-1" shows the score "0.80" and "(search 0.30)"

  @US-118 @aiact-art15 @integration
  Scenario: Without a reranker nothing changes
    When I ask for suggestions on the "Security policy" sub-tab of "Acme Portal"
    Then the suggestions on "SP-1" are "[US-007] Dark mode" and then "[US-041] Session timeout"
    And the suggestion "[US-007] Dark mode" on "SP-1" shows only the score "0.70"

  @US-118 @aiact-art15 @integration
  Scenario: Evidence suggestions are reranked too
    Given a reranker is configured
    And the minimum score for evidence suggestions is 0.55
    And the AI rates "Access policy 2026" against "SP-1" with 0.61
    And the reranker scores "Access policy 2026" against "SP-1" with 0.82
    When I upload the evidence "Access policy 2026" with the file "access-policy.pdf" on the Evidence tab of "Acme Portal"
    Then "Access policy 2026" has the suggestion "SP-1" with score "0.82" marked "AI-generated"
    And the evidence suggestion "SP-1" of "Access policy 2026" says "(search 0.61)"

