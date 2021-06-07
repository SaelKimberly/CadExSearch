using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace CadExSearch.Commons
{
    [Table("Record")]
    public class Record
    {
        public Record()
        {
            InverseBase = new HashSet<Record>();
        }

        [Key]
        [Column("ID", TypeName = "text")]
        public string Id { get; set; }

        [Required] [Column(TypeName = "text")] public string Content { get; set; }

        [Required]
        [Column("BaseID", TypeName = "text")]
        public string BaseId { get; set; }

        [ForeignKey(nameof(BaseId))]
        [InverseProperty(nameof(InverseBase))]
        public virtual Record Base { get; set; }

        [InverseProperty(nameof(Base))] public virtual ICollection<Record> InverseBase { get; set; }
    }
}