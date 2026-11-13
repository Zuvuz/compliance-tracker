using System.Text;
using ComplianceTracker.Application.Reports;
using ComplianceTracker.Domain.Frameworks;

namespace ComplianceTracker.UnitTests.Reports;

// US-152: edge-case unit tests of US-056 (the draft of the CRA technical documentation). Arrange / Act / Assert.
public class TechnicalDocumentationTests
{
    private static Framework Uploaded(string name)
    {
        var bytes = new byte[100];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(bytes, 0);
        return Framework.Create(Guid.NewGuid(), name, CatalogFile.Create("f.pdf", bytes), Guid.NewGuid(), DateTime.UtcNow, FrameworkLevel.Organisation);
    }

    [Theory]
    [InlineData("CRA – Regulation (EU) 2024/2847")]
    [InlineData("CRA notes")]
    public void An_uploaded_framework_named_like_the_CRA_is_not_the_CRA(string name)
        => Assert.False(TechnicalDocumentationQueries.IsCra(Uploaded(name)));

    [Fact]
    public void The_placeholder_for_missing_text_says_what_to_do()
        => Assert.Equal("To be completed", TechnicalDocumentationQueries.ToBeCompleted);
}
