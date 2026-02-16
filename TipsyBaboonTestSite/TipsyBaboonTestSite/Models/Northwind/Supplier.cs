using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Models;

namespace Models.Northwind;

[UIEntity("Supplier", ModuleName = "Northwind", GroupName = "Inventory", Order = 20, Icon = "bi bi-truck", HasOwnership = false, PluralName = "Suppliers")]
[Table("Supplier")]
public partial class Supplier : TipsyBaboonModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [UIDisplay(Name = "Company Name", Order = 1, ShowInList = true, ShowInDetail = true, ColumnSize = 12)]
    [Required]
    [MaxLength(100)]
    [RecordName]
    public string CompanyName { get; set; } = null!;

    [UIDisplay(Name = "Contact Name", Order = 2, ShowInList = true, ShowInDetail = true, ColumnSize = 6)]
    [MaxLength(50)]
    public string? ContactName { get; set; }

    [UIDisplay(Name = "Contact Title", Order = 3, ShowInList = false, ShowInDetail = true, ColumnSize = 6)]
    [MaxLength(50)]
    public string? ContactTitle { get; set; }

    [MaxLength(50)]
    public string? City { get; set; }

    [MaxLength(50)]
    public string? Country { get; set; }

    [MaxLength(24)]
    public string? Phone { get; set; }

    [MaxLength(24)]
    public string? Fax { get; set; }

    [UIDisplay(Name = "Products", Order = 100, ShowInList = false, ShowInDetail = true, ColumnSize = 12)]
    [IncludeNavigation(InDetail = true, InGrid = false)]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    [NotMapped]
    [UIDisplay(Name = "Contact Info", Order = 8, ShowInList = false, ShowInDetail = true, ColumnSize = 12)]
    [FieldControl("ContactInfo")]
    public ContactInfo ContactInfoComplex
    {
        get
        {
            return new ContactInfo
            {
                City = City,
                Phone = Phone,
                Fax = Fax,
                Country = Country
            };
        }
        set
        {
            if (value != null)
            {
                City = value.City;
                Phone = value.Phone;
                Fax = value.Fax;
                Country = value.Country;
            }
        }
    }
}
