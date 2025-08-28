using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Common
{
    public interface ISoftDeletable
    {
        bool IsDeleted { get; set; }
    }

    public interface IHasCreationTime
    {
        DateTime CreatedAtUtc { get; set; }
    }

    public interface IHasModificationTime
    {
        DateTime? UpdatedAtUtc { get; set; }
    }
}
