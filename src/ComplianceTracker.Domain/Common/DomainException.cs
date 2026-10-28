using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.Common
{
    // Thrown when a business rule is broken, e.g. "CVR must be 8 digits".
    // The message is written for the user: the web app shows it in the form as it is.
    public class DomainException : Exception
    {
        public DomainException(string message) : base(message) { }
    }
}
