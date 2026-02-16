using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Models;

namespace Models.Northwind;

[UIEntity("Order", ModuleName = "Northwind", GroupName = "Sales", Order = 20, Icon = "bi bi-cart-check", HasOwnership = false, PluralName = "Orders")]
[Table("Order")]
public partial class Order : TipsyBaboonModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [UIDisplay(Name = "Order Date", Order = 1, ShowInList = true, ShowInDetail = true, ColumnSize = 4)]
    public DateTime OrderDate { get; set; }

    [UIDisplay(Name = "Order Number", Order = 2, ShowInList = true, ShowInDetail = true, ColumnSize = 4)]
    [MaxLength(20)]
    [RecordName]
    public string? OrderNumber { get; set; }

    [UIDisplay(Name = "Customer", Order = 3, ShowInList = true, ShowInDetail = true, ColumnSize = 4)]
    [ForeignKey(nameof(Customer))]
    public int CustomerId { get; set; }

    [UIDisplay(Name = "Total Amount", Order = 4, ShowInList = true, ShowInDetail = true, ColumnSize = 4)]
    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalAmount { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    [FieldControl("editable-grid", """{"EditableColumns":["ProductId","Quantity","UnitPrice","Discount"]}""")]
    [UIDisplay(Name = "Order Items", Order = 100, ShowInList = false, ShowInDetail = true, ColumnSize = 12)]
    [IncludeNavigation(InDetail = true, InGrid = false)]
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
