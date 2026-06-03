using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDRS.Domain.Enums
{
    public enum ReportStatus
    {
        Draft = 0,
        Submitted = 1,
        UnderReview = 2,
        Approved = 3,
        Rejected = 4
    }
}
