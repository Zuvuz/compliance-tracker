namespace ComplianceTracker.Web.Security
{
    // US-187: the organisation a person who is a member of several has chosen, kept in a cookie. The cookie only says
    // which one to show: CurrentMember ignores an organisation the person is not a member of (R2).
    public static class ChosenOrganisation
    {
        public const string CookieName = "ct_org";

        public static Guid? Read(HttpContext? context)
            => context?.Request.Cookies.TryGetValue(CookieName, out var value) == true && Guid.TryParse(value, out var id) ? id : null;

        public static void Write(HttpResponse response, Guid organizationId, bool development)
            => response.Cookies.Append(CookieName, organizationId.ToString(), new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = !development || response.HttpContext.Request.IsHttps,
                MaxAge = TimeSpan.FromDays(30)
            });
    }
}
