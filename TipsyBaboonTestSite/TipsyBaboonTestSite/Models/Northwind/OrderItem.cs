using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Models;

namespace Models.Northwind;

[UIEntity("Order Item", ModuleName = "Northwind", GroupName = "Sales", Order = 30, Icon = "bi bi-list-ul", HasOwnership = false, PluralName = "Order Items", ShowInMenu = false)]
[Table("OrderItem")]
public partial class OrderItem : TipsyBaboonModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [UIDisplay(Name = "Order", Order = 1, ShowInList = true, ShowInDetail = true, ColumnSize = 4)]
    [ForeignKey(nameof(Order))]
    public int OrderId { get; set; }

    [UIDisplay(Name = "Product", Order = 2, ShowInList = true, ShowInDetail = true, ColumnSize = 4)]
    [ForeignKey(nameof(Product))]
    public int ProductId { get; set; }

    [UIDisplay(Name = "Unit Price", Order = 3, ShowInList = true, ShowInDetail = true, ColumnSize = 3)]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [UIDisplay(Name = "Quantity", Order = 4, ShowInList = true, ShowInDetail = true, ColumnSize = 3)]
    public int Quantity { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
