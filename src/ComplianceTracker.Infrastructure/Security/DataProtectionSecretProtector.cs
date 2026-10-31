using ComplianceTracker.Application.Common;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace ComplianceTracker.Infrastructure.Security
{
    // ISecretProtector with ASP.NET Core Data Protection (AES-256-CBC + HMACSHA256, keys rotated every 90 days).
    // The keys are stored in PostgreSQL (DataProtectionKeys), so tokens can still be read after a restart
    // or in a new container (US-031 R3).
    public class DataProtectionSecretProtector : ISecretProtector
    {
        // A purpose of its own, so text protected here cannot be used as e.g. a cookie, and the other way round
        private const string Purpose = "ComplianceTracker.AccessTokens.v1";

        private readonly IDataProtector _protector;

        public DataProtectionSecretProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(Purpose);
        }

        public string Protect(string secret) => _protector.Protect(secret);

        public string Unprotect(string protectedSecret)
        {
            try
            {
                return _protector.Unprotect(protectedSecret);
            }
            catch (CryptographicException error)
            {
                throw new SecretUnreadableException(error);
            }
        }
    }
}
