using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Models;

namespace Models.AdventureWorks;

[UIEntity("Product Category", ModuleName = "AdventureWorks", GroupName = "Catalog", Order = 20, Icon = "bi bi-tags", HasOwnership = false, PluralName = "Product Categories")]
[Table("ProductCategory", Schema = "SalesLT")]
public partial class ProductCategory : TipsyBaboonModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ProductCategoryID { get; set; }

    [UIDisplay(Name = "Category #", Order = 10, ShowInList = true)]
    public int? ProductCategoryIDVisible => ProductCategoryID;

    [UIDisplay(Name = "Parent Category", Order = 20)]
    public int? ParentProductCategoryID { get; set; }

    [Required]
    [UIDisplay(Name = "Name", Order = 30, ShowInList = true)]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [UIDisplay(Name = "Modified On", Order = 900)]
    [Column("ModifiedDate")]
    public override DateTime? ModifiedOn { get; set; } = DateTime.Now;
}
