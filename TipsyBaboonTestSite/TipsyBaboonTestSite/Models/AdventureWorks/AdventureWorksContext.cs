using Microsoft.EntityFrameworkCore;

namespace Models.AdventureWorks;

public partial class AdventureWorksContext : DbContext
{
    public AdventureWorksContext()
    {
    }

    public AdventureWorksContext(DbContextOptions<AdventureWorksContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Customer> Customers { get; set; }
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<ProductCategory> ProductCategories { get; set; }
    public virtual DbSet<SalesOrder> SalesOrders { get; set; }
    public virtual DbSet<SalesOrderDetail> SalesOrderDetails { get; set; }
    public virtual DbSet<Address> Addresses { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Server=localhost;Database=AdventureWorksLT;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customer", "SalesLT");
            entity.HasKey(e => e.CustomerID);
            entity.Property(e => e.CompanyName).HasMaxLength(128);
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50);
            entity.Property(e => e.EmailAddress).HasMaxLength(50);
            entity.Property(e => e.Phone).HasMaxLength(25);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Product", "SalesLT");
            entity.HasKey(e => e.ProductID);
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.ProductNumber).HasMaxLength(25);
            entity.Property(e => e.Color).HasMaxLength(15);
            entity.Property(e => e.StandardCost).HasColumnType("money");
            entity.Property(e => e.ListPrice).HasColumnType("money");
            entity.Property(e => e.Size).HasMaxLength(5);
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.ToTable("ProductCategory", "SalesLT");
            entity.HasKey(e => e.ProductCategoryID);
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<SalesOrder>(entity =>
        {
            entity.ToTable("SalesOrderHeader", "SalesLT");
            entity.HasKey(e => e.SalesOrderID);
            entity.Property(e => e.SalesOrderNumber).HasMaxLength(25);
            entity.Property(e => e.PurchaseOrderNumber).HasMaxLength(25);
            entity.Property(e => e.AccountNumber).HasMaxLength(15);
            entity.Property(e => e.SubTotal).HasColumnType("money");
            entity.Property(e => e.TaxAmt).HasColumnType("money");
            entity.Property(e => e.Freight).HasColumnType("money");
            entity.Property(e => e.TotalDue).HasColumnType("money");
        });

        modelBuilder.Entity<SalesOrderDetail>(entity =>
        {
            entity.ToTable("SalesOrderDetail", "SalesLT");
            entity.HasKey(e => e.SalesOrderDetailID);
            entity.Property(e => e.UnitPrice).HasColumnType("money");
            entity.Property(e => e.UnitPriceDiscount).HasColumnType("money");
            entity.Property(e => e.LineTotal).HasColumnType("numeric(38, 6)");
        });

        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("Address", "SalesLT");
            entity.HasKey(e => e.AddressID);
            entity.Property(e => e.AddressLine1).HasMaxLength(60);
            entity.Property(e => e.AddressLine2).HasMaxLength(60);
            entity.Property(e => e.City).HasMaxLength(30);
            entity.Property(e => e.StateProvince).HasMaxLength(50);
            entity.Property(e => e.CountryRegion).HasMaxLength(50);
            entity.Property(e => e.PostalCode).HasMaxLength(15);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
