using ComplianceTracker.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ComplianceTracker.UnitTests.Web;

// US-174: edge cases of the Undo after a small removal. Arrange / Act / Assert.
public class UndoOfferTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);

    private sealed class NoProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static TempDataDictionary TempData() => new(new DefaultHttpContext(), new NoProvider());

    [Fact]
    public void The_offer_is_open_for_two_minutes()
    {
        var tempData = TempData();
        UndoOffer.Offer(tempData, Now, "Removed the measure", "Risks", "AddMeasure", new() { ["id"] = "1" });

        var offer = UndoOffer.From(tempData[UndoOffer.Key])!;

        Assert.True(offer.IsOpen(Now.AddSeconds(119)));
        Assert.False(offer.IsOpen(Now.AddMinutes(2)));
    }

    [Fact]
    public void The_offer_keeps_the_form_it_sends()
    {
        var tempData = TempData();
        UndoOffer.Offer(tempData, Now, "Removed", "Projects", "LinkRequirement", new() { ["projectId"] = "p", ["requirementId"] = "r", ["workItemId"] = "w" });

        var offer = UndoOffer.From(tempData[UndoOffer.Key])!;

        Assert.Equal(("Projects", "LinkRequirement"), (offer.Controller, offer.Action));
        Assert.Equal(["projectId", "requirementId", "workItemId"], offer.Fields.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(42)]
    [InlineData("not json")]
    public void Anything_else_in_temp_data_is_no_offer(object? value)
        => Assert.Null(UndoOffer.From(value));
}
