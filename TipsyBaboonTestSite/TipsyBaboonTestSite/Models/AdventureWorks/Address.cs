using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Models;

namespace Models.AdventureWorks;

[UIEntity("Address", ModuleName = "AdventureWorks", GroupName = "Reference", Order = 10, Icon = "bi bi-geo-alt", HasOwnership = false, PluralName = "Addresses")]
[Table("Address", Schema = "SalesLT")]
public partial class Address : TipsyBaboonModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int AddressID { get; set; }

    [UIDisplay(Name = "Address #", Order = 10, ShowInList = true)]
    public int? AddressIDVisible => AddressID;

    [Required]
    [UIDisplay(Name = "Address Line 1", Order = 20, ShowInList = true)]
    [StringLength(60)]
    public string AddressLine1 { get; set; } = string.Empty;

    [UIDisplay(Name = "Address Line 2", Order = 30)]
    [StringLength(60)]
    public string? AddressLine2 { get; set; }

    [Required]
    [UIDisplay(Name = "City", Order = 40, ShowInList = true)]
    [StringLength(30)]
    public string City { get; set; } = string.Empty;

    [Required]
    [UIDisplay(Name = "State/Province", Order = 50, ShowInList = true)]
    [StringLength(50)]
    public string StateProvince { get; set; } = string.Empty;

    [Required]
    [UIDisplay(Name = "Country/Region", Order = 60, ShowInList = true)]
    [StringLength(50)]
    public string CountryRegion { get; set; } = string.Empty;

    [Required]
    [UIDisplay(Name = "Postal Code", Order = 70)]
    [StringLength(15)]
    public string PostalCode { get; set; } = string.Empty;

    [UIDisplay(Name = "Modified On", Order = 900)]
    [Column("ModifiedDate")]
    public override DateTime? ModifiedOn { get; set; } = DateTime.Now;

    [NotMapped]
    [UIDisplay(Name = "Full Address", Order = 5, ShowInList = true)]
    public string FullAddress => $"{AddressLine1}, {City}, {StateProvince} {PostalCode}, {CountryRegion}";
}
