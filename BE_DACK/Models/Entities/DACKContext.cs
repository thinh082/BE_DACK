using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BE_DACK.Models.Entities;

public partial class DACKContext : DbContext
{
    public DACKContext()
    {
    }

    public DACKContext(DbContextOptions<DACKContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AccountType> AccountTypes { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<LienHe> LienHes { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductPromotion> ProductPromotions { get; set; }

    public virtual DbSet<ProductReview> ProductReviews { get; set; }

    public virtual DbSet<Promotion> Promotions { get; set; }

    public virtual DbSet<ShoppingCart> ShoppingCarts { get; set; }

    public virtual DbSet<ShoppingCartDetail> ShoppingCartDetails { get; set; }

    public virtual DbSet<TonKhoSummary> TonKhoSummaries { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=ConnectionStrings:Connection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AccountT__3214EC073E094E2C");

            entity.Property(e => e.TenLoaiTaiKhoan).HasMaxLength(100);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Categori__3214EC071A0126D0");

            entity.Property(e => e.TenDanhMucSp)
                .HasMaxLength(255)
                .HasColumnName("TenDanhMucSP");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC071DC8992C");

            entity.HasIndex(e => e.Email, "UQ__Customer__A9D10534FD654FE8").IsUnique();

            entity.Property(e => e.DiaChi).HasMaxLength(255);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.HoTen).HasMaxLength(255);
            entity.Property(e => e.Sdt)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("SDT");

            entity.HasOne(d => d.IdAccountTypesNavigation).WithMany(p => p.Customers)
                .HasForeignKey(d => d.IdAccountTypes)
                .HasConstraintName("FK_Customers_AccountTypes");
        });

        modelBuilder.Entity<LienHe>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__LienHe__3214EC074B5D986B");

            entity.ToTable("LienHe");

            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.NgayGui)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Orders__3214EC07F4939770");

            entity.Property(e => e.NgayTaoDonHang).HasColumnType("datetime");
            entity.Property(e => e.TongGiaTriDonHang).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__Orders__Customer__46E78A0C");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__OrderDet__3214EC07D3935592");

            entity.Property(e => e.Gia).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.SoLuongSp).HasColumnName("SoLuongSP");
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__OrderDeta__Order__44FF419A");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__OrderDeta__Produ__45F365D3");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Payments__3214EC07633E381E");

            entity.Property(e => e.NgayThanhToan).HasColumnType("datetime");
            entity.Property(e => e.PhuongThucThanhToan).HasMaxLength(50);
            entity.Property(e => e.SoTienThanhToan).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__Payments__OrderI__47DBAE45");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Products__3214EC075341894A");

            entity.Property(e => e.Gia).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TenSp)
                .HasMaxLength(255)
                .HasColumnName("TenSP");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK__Products__Catego__4D94879B");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ProductI__3214EC07F0AC15F9");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ProductIm__Produ__48CFD27E");
        });

        modelBuilder.Entity<ProductPromotion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ProductP__3214EC07CAAB8C2D");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductPromotions)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__ProductPr__Produ__4AB81AF0");

            entity.HasOne(d => d.Promotion).WithMany(p => p.ProductPromotions)
                .HasForeignKey(d => d.PromotionId)
                .HasConstraintName("FK__ProductPr__Promo__49C3F6B7");
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ProductR__3214EC07D575EE57");

            entity.Property(e => e.DiemDg).HasColumnName("DiemDG");
            entity.Property(e => e.NgayDg)
                .HasColumnType("datetime")
                .HasColumnName("NgayDG");
            entity.Property(e => e.NoiDungDg)
                .HasMaxLength(255)
                .HasColumnName("NoiDungDG");

            entity.HasOne(d => d.Customer).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__ProductRe__Custo__4BAC3F29");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductReviews)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__ProductRe__Produ__4CA06362");
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Promotio__3214EC07ECD44FDD");

            entity.Property(e => e.MoTa).HasMaxLength(255);
            entity.Property(e => e.PhanTramGiam).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.TenKhuyenMai).HasMaxLength(100);
        });

        modelBuilder.Entity<ShoppingCart>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Shopping__3214EC076E20EB7E");

            entity.ToTable("ShoppingCart");

            entity.HasOne(d => d.Customer).WithMany(p => p.ShoppingCarts)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__ShoppingC__Custo__4E88ABD4");
        });

        modelBuilder.Entity<ShoppingCartDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Shopping__3214EC070E223317");

            entity.Property(e => e.SoLuongTrongGh).HasColumnName("SoLuongTrongGH");

            entity.HasOne(d => d.Cart).WithMany(p => p.ShoppingCartDetails)
                .HasForeignKey(d => d.CartId)
                .HasConstraintName("FK__ShoppingC__CartI__4F7CD00D");

            entity.HasOne(d => d.Product).WithMany(p => p.ShoppingCartDetails)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__ShoppingC__Produ__5070F446");
        });

        modelBuilder.Entity<TonKhoSummary>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TonKhoSu__3214EC078D60EA23");

            entity.ToTable("TonKhoSummary");

            entity.Property(e => e.Dvt)
                .HasMaxLength(50)
                .HasColumnName("DVT");
            entity.Property(e => e.TenHh)
                .HasMaxLength(100)
                .HasColumnName("TenHH");
            entity.Property(e => e.TongSoLuongNhap).HasDefaultValue(0);
            entity.Property(e => e.TongSoLuongXuat).HasDefaultValue(0);
            entity.Property(e => e.TongSoTon).HasDefaultValue(0);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
