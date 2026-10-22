using ComplianceTracker.Domain.Organizations;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Application.Organizations
{
    public interface IOrganizationRepository
    {
        bool CvrExists(string cvr);
        void Add(Organization organization);
    }
}
