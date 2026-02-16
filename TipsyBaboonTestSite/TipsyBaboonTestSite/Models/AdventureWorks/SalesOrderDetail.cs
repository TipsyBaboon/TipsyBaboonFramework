using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Models;

namespace Models.AdventureWorks;

[UIEntity("Order Detail", ModuleName = "AdventureWorks", GroupName = "Sales", Order = 30, Icon = "bi bi-list-ul", HasOwnership = false, PluralName = "Order Details", ShowInMenu = false)]
[Table("SalesOrderDetail", Schema = "SalesLT")]
public partial class SalesOrderDetail : TipsyBaboonModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int SalesOrderDetailID { get; set; }

    [UIDisplay(Name = "Detail #", Order = 10, ShowInList = true)]
    public int? SalesOrderDetailIDVisible => SalesOrderDetailID;

    [Required]
    [UIDisplay(Name = "Sales Order", Order = 20)]
    public int SalesOrderID { get; set; }

    [Required]
    [UIDisplay(Name = "Order Qty", Order = 30, ShowInList = true)]
    public short OrderQty { get; set; }

    [Required]
    [UIDisplay(Name = "Product", Order = 40, ShowInList = true)]
    public int ProductID { get; set; }

    [UIDisplay(Name = "Unit Price", Order = 50, ShowInList = true)]
    [Column(TypeName = "money")]
    public decimal UnitPrice { get; set; }

    [UIDisplay(Name = "Unit Price Discount", Order = 60)]
    [Column(TypeName = "money")]
    public decimal UnitPriceDiscount { get; set; }

    [UIDisplay(Name = "Line Total", Order = 70, ShowInList = true)]
    [Column(TypeName = "numeric(38, 6)")]
    public decimal LineTotal { get; set; }

    [UIDisplay(Name = "Modified On", Order = 900)]
    [Column("ModifiedDate")]
    public override DateTime? ModifiedOn { get; set; } = DateTime.Now;
}
