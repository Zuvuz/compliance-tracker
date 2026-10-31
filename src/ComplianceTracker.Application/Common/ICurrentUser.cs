using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Common
{
    // The person behind the current request (US-029 R1).
    // The web app implements it from the sign-in cookie; the tests supply a fixed user.
    // Use cases ask for it through dependency injection instead of taking the user as a parameter,
    // so no use case can act for someone else by mistake.
    public interface ICurrentUser
    {
        // Id, name and e-mail from the sign-in (Keycloak). Throws if nobody is signed in.
        UserIdentity Identity { get; }
    }
}
