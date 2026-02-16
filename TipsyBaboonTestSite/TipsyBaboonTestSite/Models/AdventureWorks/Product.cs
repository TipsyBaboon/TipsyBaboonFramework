using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Models;

namespace Models.AdventureWorks;

[UIEntity("Product", ModuleName = "AdventureWorks", GroupName = "Catalog", Order = 10, Icon = "bi bi-box-seam", HasOwnership = false, PluralName = "Products")]
[Table("Product", Schema = "SalesLT")]
public partial class Product : TipsyBaboonModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ProductID { get; set; }

    [UIDisplay(Name = "Product #", Order = 10, ShowInList = true)]
    public int? ProductIDVisible => ProductID;

    [Required]
    [UIDisplay(Name = "Name", Order = 20, ShowInList = true)]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [UIDisplay(Name = "Product Number", Order = 30, ShowInList = true)]
    [StringLength(25)]
    public string ProductNumber { get; set; } = string.Empty;

    [UIDisplay(Name = "Color", Order = 40, ShowInList = true)]
    [StringLength(15)]
    public string? Color { get; set; }

    [UIDisplay(Name = "Standard Cost", Order = 50, ShowInList = true)]
    [Column(TypeName = "money")]
    public decimal StandardCost { get; set; }

    [UIDisplay(Name = "List Price", Order = 60, ShowInList = true)]
    [Column(TypeName = "money")]
    public decimal ListPrice { get; set; }

    [UIDisplay(Name = "Size", Order = 70)]
    [StringLength(5)]
    public string? Size { get; set; }

    [UIDisplay(Name = "Weight", Order = 80)]
    public decimal? Weight { get; set; }

    [UIDisplay(Name = "Product Category", Order = 90)]
    public int? ProductCategoryID { get; set; }

    [UIDisplay(Name = "Product Model", Order = 100)]
    public int? ProductModelID { get; set; }

    [UIDisplay(Name = "Sell Start Date", Order = 200)]
    public DateTime SellStartDate { get; set; }

    [UIDisplay(Name = "Sell End Date", Order = 210)]
    public DateTime? SellEndDate { get; set; }

    [UIDisplay(Name = "Discontinued Date", Order = 220)]
    public DateTime? DiscontinuedDate { get; set; }

    [UIDisplay(Name = "Thumbnail Photo", Order = 300, ShowInDetail = false, ShowInList = false)]
    public byte[]? ThumbNailPhoto { get; set; }

    [UIDisplay(Name = "Thumbnail Photo File", Order = 310, ShowInDetail = false, ShowInList = false)]
    [StringLength(50)]
    public string? ThumbnailPhotoFileName { get; set; }

    [UIDisplay(Name = "Modified On", Order = 900)]
    [Column("ModifiedDate")]
    public override DateTime? ModifiedOn { get; set; } = DateTime.Now;
}
