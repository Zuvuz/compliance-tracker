namespace ComplianceTracker.Domain.Frameworks
{
    // US-143 R5: what an item of a standard is
    public enum RequirementKind
    {
        // "shall" (also "shall not"); the default, and what every requirement was before US-143
        Requirement,

        // "should": shown as "Recommended", never lowers the coverage (R6)
        Recommendation,

        // A characteristic of a quality model, e.g. ISO/IEC 25010's "Maintainability"
        Characteristic
    }
}
