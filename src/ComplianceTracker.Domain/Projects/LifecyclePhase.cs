namespace ComplianceTracker.Domain.Projects
{
    // Where a product is in its life, from idea to end of support (US-008).
    // Matches the CRA way of thinking: the rules apply to a product for as long as it is supported.
    // Stored as text in the database, so new values never shift existing data. The order is the order of the phases.
    // US-047 R1: Design and Verification were added; Released also covers maintenance while the product is supported.
    public enum LifecyclePhase
    {
        // Idea and planning; nothing built yet
        Planning,

        // US-047: requirements, architecture and the threat model
        Design,

        // Being built
        Development,

        // US-047: tested and checked before release
        Verification,

        // On the market, in use and maintained
        Released,

        // No longer maintained
        EndOfSupport
    }
}
