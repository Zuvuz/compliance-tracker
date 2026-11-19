using ComplianceTracker.Application.Frameworks;

namespace ComplianceTracker.UnitTests.Frameworks;

// #52: ISO/IEC 27001's management-system clauses (4–10) are not asked of a product as "Needs action"; Annex A controls are
public class ManagementClauseTests
{
    [Theory]
    [InlineData("ISO27001 – Demo", "5.1", true)]
    [InlineData("ISO/IEC 27001:2022", "4.1", true)]
    [InlineData("ISO/IEC 27001:2022", "6.1.2", true)]
    [InlineData("ISO/IEC 27001:2022", "9.3.1", true)]
    [InlineData("ISO/IEC 27001:2022", "10.2", true)]
    [InlineData("ISO/IEC 27001:2022", " 7.5.1 ", true)]
    [InlineData("ISO/IEC 27001:2022", "A.5.1", false)] // an Annex A control
    [InlineData("ISO/IEC 27001:2022", "A.8.28", false)]
    [InlineData("ISO/IEC 27001:2022", "3.1", false)] // terms, not a requirement
    [InlineData("ISO/IEC 27001:2022", "11.1", false)]
    [InlineData("ISO/IEC 27001:2022", "5.1.1.1", false)]
    [InlineData("ISO/IEC 27001:2022", "", false)]
    [InlineData("ISO/IEC 27002:2022", "5.1", false)] // 27002's controls are numbered without "A."
    [InlineData("ISO/IEC 270011", "5.1", false)]
    [InlineData("CRA – Regulation (EU) 2024/2847", "5.1", false)]
    [InlineData("", "5.1", false)]
    public void Only_the_clauses_of_ISO_27001_are_management_clauses(string framework, string code, bool expected)
        => Assert.Equal(expected, FrameworkProfiles.IsManagementClause(framework, code));

    [Fact]
    public void A_missing_name_or_code_is_not_a_management_clause()
    {
        Assert.False(FrameworkProfiles.IsManagementClause(null!, "5.1"));
        Assert.False(FrameworkProfiles.IsManagementClause("ISO/IEC 27001:2022", null!));
    }
}
