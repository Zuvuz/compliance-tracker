namespace ComplianceTracker.Application.Common
{
    // Encrypts secrets the tracker must store, e.g. a GitHub token (US-031 R3, ISO 27001 A.8.24).
    // Implemented in Infrastructure. The domain only ever sees the encrypted text.
    public interface ISecretProtector
    {
        string Protect(string secret);

        // Throws SecretUnreadableException when the text cannot be decrypted (e.g. the keys were lost)
        string Unprotect(string protectedSecret);
    }

    public class SecretUnreadableException : Exception
    {
        public SecretUnreadableException(Exception inner) : base("The stored secret cannot be decrypted", inner)
        {
        }
    }
}
