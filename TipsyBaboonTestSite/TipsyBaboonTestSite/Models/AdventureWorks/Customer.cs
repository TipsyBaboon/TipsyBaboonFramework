using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Models;

namespace Models.AdventureWorks;

[UIEntity("Customer", ModuleName = "AdventureWorks", GroupName = "Sales", Order = 10, Icon = "bi bi-person-badge", HasOwnership = false, PluralName = "Customers")]
[Table("Customer", Schema = "SalesLT")]
public partial class Customer : TipsyBaboonModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int CustomerID { get; set; }

    [UIDisplay(Name = "Customer #", Order = 10, ShowInList = true)]
    public int? CustomerIDVisible => CustomerID;

    [Required]
    [UIDisplay(Name = "Company Name", Order = 20, ShowInList = true)]
    [StringLength(128)]
    public string CompanyName { get; set; } = string.Empty;

    [UIDisplay(Name = "Salutation", Order = 30)]
    [StringLength(8)]
    public string? SalesPerson { get; set; }

    [UIDisplay(Name = "Title", Order = 40)]
    [StringLength(8)]
    public string? Title { get; set; }

    [Required]
    [UIDisplay(Name = "First Name", Order = 50, ShowInList = true)]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [UIDisplay(Name = "Middle Name", Order = 60)]
    [StringLength(50)]
    public string? MiddleName { get; set; }

    [Required]
    [UIDisplay(Name = "Last Name", Order = 70, ShowInList = true)]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [UIDisplay(Name = "Suffix", Order = 80)]
    [StringLength(10)]
    public string? Suffix { get; set; }

    [UIDisplay(Name = "Email", Order = 90, ShowInList = true)]
    [StringLength(50)]
    [EmailAddress]
    public string? EmailAddress { get; set; }

    [UIDisplay(Name = "Phone", Order = 100, ShowInList = true)]
    [StringLength(25)]
    [Phone]
    public string? Phone { get; set; }

    [StringLength(128)]
    public string? PasswordHash { get; set; }

    [StringLength(10)]
    public string? PasswordSalt { get; set; }

    [UIDisplay(Name = "Modified On", Order = 900)]
    [Column("ModifiedDate")]
    public override DateTime? ModifiedOn { get; set; } = DateTime.Now;

    [NotMapped]
    [UIDisplay(Name = "Full Name", Order = 5, ShowInList = true)]
    public string FullName => $"{FirstName} {LastName}";
}
