using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ComplianceTracker.Web.Models
{
    // US-174: after a small removal, the message says "Removed … · Undo" for 2 minutes (R1). Undo sends the same form as
    // making the link the normal way (the same permission and log), so it can never do more than the member may do (R2).
    // Only the member who removed it sees the button: the offer is in their own TempData. After 2 minutes the button is
    // gone, and the link is made again the normal way (R4).
    public sealed record UndoOffer(string Text, string Controller, string Action, Dictionary<string, string> Fields, DateTime UntilUtc)
    {
        public const string Key = "Undo";
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(2);

        public bool IsOpen(DateTime nowUtc) => nowUtc < UntilUtc;

        public static void Offer(ITempDataDictionary tempData, DateTime nowUtc, string text, string controller, string action, Dictionary<string, string> fields)
            => tempData[Key] = JsonSerializer.Serialize(new UndoOffer(text, controller, action, fields, nowUtc + Window));

        public static UndoOffer? From(object? value)
        {
            if (value is not string json)
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<UndoOffer>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
