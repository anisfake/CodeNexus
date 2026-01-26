using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class NoteTags
    {
        public Guid NoteId { get; set; }
        public virtual Note Note { get; set; } = null!;

        public Guid TagId { get; set; }
        public virtual Tag Tag { get; set; } = null!;
    }
}
