using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.Organizations
{
    public interface IOrganizationRepository
    {
        bool CvrExists(string cvr);
        void Add(Organization organization);
    }
}
