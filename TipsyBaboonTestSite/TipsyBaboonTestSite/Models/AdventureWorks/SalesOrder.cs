using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Models;

namespace Models.AdventureWorks;

[UIEntity("Sales Order", ModuleName = "AdventureWorks", GroupName = "Sales", Order = 20, Icon = "bi bi-cart-check", HasOwnership = false, PluralName = "Sales Orders")]
[Table("SalesOrderHeader", Schema = "SalesLT")]
public partial class SalesOrder : TipsyBaboonModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int SalesOrderID { get; set; }

    [UIDisplay(Name = "Order #", Order = 10, ShowInList = true)]
    public int? SalesOrderIDVisible => SalesOrderID;

    [Required]
    [UIDisplay(Name = "Order Number", Order = 20, ShowInList = true)]
    [StringLength(25)]
    public string SalesOrderNumber { get; set; } = string.Empty;

    [UIDisplay(Name = "Order Date", Order = 30, ShowInList = true)]
    public DateTime OrderDate { get; set; } = DateTime.Now;

    [UIDisplay(Name = "Due Date", Order = 40, ShowInList = true)]
    public DateTime DueDate { get; set; }

    [UIDisplay(Name = "Ship Date", Order = 50, ShowInList = true)]
    public DateTime? ShipDate { get; set; }

    [Required]
    [UIDisplay(Name = "Status", Order = 60, ShowInList = true)]
    public byte Status { get; set; }

    [UIDisplay(Name = "Online Order?", Order = 70)]
    public bool OnlineOrderFlag { get; set; }

    [Required]
    [UIDisplay(Name = "Customer", Order = 80)]
    public int CustomerID { get; set; }

    [UIDisplay(Name = "Ship to Address", Order = 90)]
    public int? ShipToAddressID { get; set; }

    [UIDisplay(Name = "Bill to Address", Order = 100)]
    public int? BillToAddressID { get; set; }

    [UIDisplay(Name = "Ship Method", Order = 110)]
    [StringLength(50)]
    public string? ShipMethod { get; set; }

    [UIDisplay(Name = "Credit Card Approval", Order = 120)]
    [StringLength(15)]
    public string? CreditCardApprovalCode { get; set; }

    [UIDisplay(Name = "Subtotal", Order = 200, ShowInList = true)]
    [Column(TypeName = "money")]
    public decimal SubTotal { get; set; }

    [UIDisplay(Name = "Tax", Order = 210)]
    [Column(TypeName = "money")]
    public decimal TaxAmt { get; set; }

    [UIDisplay(Name = "Freight", Order = 220)]
    [Column(TypeName = "money")]
    public decimal Freight { get; set; }

    [UIDisplay(Name = "Total", Order = 230, ShowInList = true)]
    [Column(TypeName = "money")]
    public decimal TotalDue { get; set; }

    [UIDisplay(Name = "Comment", Order = 300)]
    public string? Comment { get; set; }

    [UIDisplay(Name = "PO Number", Order = 310)]
    [StringLength(25)]
    public string? PurchaseOrderNumber { get; set; }

    [UIDisplay(Name = "Account Number", Order = 320)]
    [StringLength(15)]
    public string? AccountNumber { get; set; }

    [UIDisplay(Name = "Modified On", Order = 900)]
    [Column("ModifiedDate")]
    public override DateTime? ModifiedOn { get; set; } = DateTime.Now;
}
