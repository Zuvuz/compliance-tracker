using System;
using System.Collections.Generic;
using System.Text;

namespace ComplianceTracker.Domain.Users
{
    public class User
    {
        public string Name { get; set; }
        public UserRole Role { get; set; }
        public Guid Id { get; } = Guid.NewGuid();
    }
}
