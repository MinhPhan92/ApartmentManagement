using ApartmentManagement.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Building> Buildings => Set<Building>();

        public DbSet<Apartment> Apartments => Set<Apartment>();

        public DbSet<Resident> Residents => Set<Resident>();

        public DbSet<ApartmentResident> ApartmentResidents
            => Set<ApartmentResident>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // BUILDING
            builder.Entity<Building>()
                .HasIndex(x => x.BuildingCode)
                .IsUnique();

            builder.Entity<Building>()
                .Property(x => x.BuildingCode)
                .HasMaxLength(50);

            // APARTMENT
            builder.Entity<Apartment>()
                .HasIndex(x => new
                {
                    x.BuildingId,
                    x.ApartmentCode
                })
                .IsUnique();

            builder.Entity<Apartment>()
                .Property(x => x.ApartmentCode)
                .HasMaxLength(50);

            builder.Entity<Apartment>()
                .Property(x => x.Area)
                .HasPrecision(18, 2);

            builder.Entity<Apartment>()
                .HasOne(x => x.Building)
                .WithMany(x => x.Apartments)
                .HasForeignKey(x => x.BuildingId)
                .OnDelete(DeleteBehavior.Restrict);

            // RESIDENT
            builder.Entity<Resident>()
                .HasIndex(x => x.CitizenId)
                .IsUnique();

            builder.Entity<Resident>()
                .Property(x => x.CitizenId)
                .HasMaxLength(20);

            builder.Entity<Resident>()
                .HasOne(x => x.User)
                .WithOne(x => x.Resident)
                .HasForeignKey<Resident>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // APARTMENT_RESIDENT
            builder.Entity<ApartmentResident>()
                .HasKey(x => new
                {
                    x.ApartmentId,
                    x.ResidentId
                });

            builder.Entity<ApartmentResident>()
                .HasOne(x => x.Apartment)
                .WithMany(x => x.ApartmentResidents)
                .HasForeignKey(x => x.ApartmentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ApartmentResident>()
                .HasOne(x => x.Resident)
                .WithMany(x => x.ApartmentResidents)
                .HasForeignKey(x => x.ResidentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}