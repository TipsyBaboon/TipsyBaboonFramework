using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Models;

namespace Models.Northwind;

[UIEntity("Product", ModuleName = "Northwind", GroupName = "Inventory", Order = 10, Icon = "bi bi-box-seam", HasOwnership = false, PluralName = "Products")]
[Table("Product")]
public partial class Product : TipsyBaboonModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [UIDisplay(Name = "Product Name", Order = 1, ShowInList = true, ShowInDetail = true, ColumnSize = 8)]
    [Required]
    [MaxLength(100)]
    [RecordName]
    public string ProductName { get; set; } = null!;

    [UIDisplay(Name = "Supplier", Order = 2, ShowInList = true, ShowInDetail = true, ColumnSize = 4)]
    [ForeignKey(nameof(Supplier))]
    public int SupplierId { get; set; }

    [UIDisplay(Name = "Unit Price", Order = 3, ShowInList = true, ShowInDetail = true, ColumnSize = 4)]
    [Column(TypeName = "decimal(18,2)")]
    public decimal? UnitPrice { get; set; }

    [UIDisplay(Name = "Package", Order = 4, ShowInList = false, ShowInDetail = true, ColumnSize = 4)]
    [MaxLength(50)]
    public string? Package { get; set; }

    [UIDisplay(Name = "Discontinued", Order = 5, ShowInList = true, ShowInDetail = true, ColumnSize = 4)]
    public bool IsDiscontinued { get; set; }

    [UIDisplay(Name = "Order Items", Order = 100, ShowInList = false, ShowInDetail = true, ColumnSize = 12)]
    [IncludeNavigation(InDetail = true, InGrid = false)]
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Supplier Supplier { get; set; } = null!;
}
