using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Infrastructure.Persistence
{
    public class CatalogDocument
    {
        public Guid FrameworkVersionId { get; set; }
        public byte[] Content { get; set; } = [];
    }
}
