using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Infrastructure.Persistence
{
    // The PDF content of one framework version (US-005).
    // A storage detail, so it lives here and not in the domain. It has its own table,
    // so listing frameworks never loads the files.
    public class CatalogDocument
    {
        // Same id as the version: one PDF per version
        public Guid FrameworkVersionId { get; set; }

        public byte[] Content { get; set; } = [];
    }
}
