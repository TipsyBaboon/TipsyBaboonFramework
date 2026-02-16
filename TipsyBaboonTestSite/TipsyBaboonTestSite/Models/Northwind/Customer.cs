using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Models;

namespace Models.Northwind;

[UIEntity("Customer", ModuleName = "Northwind", GroupName = "Sales", Order = 10, Icon = "bi bi-person-badge", HasOwnership = false, PluralName = "Customers")]
[Table("Customer")]
public partial class Customer : TipsyBaboonModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)] 
    public int Id { get; set; }

    [UIDisplay(Name = "First Name", Order = 1, ShowInList = true, ShowInDetail = true, ColumnSize = 6)]
    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = null!;

    [UIDisplay(Name = "Last Name", Order = 2, ShowInList = true, ShowInDetail = true, ColumnSize = 6)]
    [Required]
    [MaxLength(50)]
    [RecordName]
    public string LastName { get; set; } = null!;

    [UIDisplay(Name = "City", Order = 3, ShowInList = true, ShowInDetail = true, ColumnSize = 6)]
    [MaxLength(50)]
    public string? City { get; set; }

    [UIDisplay(Name = "Country", Order = 4, ShowInList = true, ShowInDetail = true, ColumnSize = 6)]
    [MaxLength(50)]
    public string? Country { get; set; }

    [UIDisplay(Name = "Phone", Order = 5, ShowInList = false, ShowInDetail = true, ColumnSize = 6)]
    [MaxLength(24)]
    public string? Phone { get; set; }

    [UIDisplay(Name = "Orders", Order = 100, ShowInList = false, ShowInDetail = true, ColumnSize = 12)]
    [IncludeNavigation(InDetail = true, InGrid = false)]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
