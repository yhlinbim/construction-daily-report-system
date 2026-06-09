using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDRS.Domain.Entities
{
    public class SiteWorker
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;  // "Worker", "Supervisor", "ProjectManager"
        public string ProjectId { get; set; } = string.Empty;
    }
}
