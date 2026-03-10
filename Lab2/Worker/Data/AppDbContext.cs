using Microsoft.EntityFrameworkCore;
using Worker.Data.Entities;

namespace Worker.Data
{
	public class AppDbContext : DbContext
	{
		public DbSet<Medicine> Medicines { get; set; }
		public DbSet<Analog> Analogs { get; set; }
		public DbSet<MedicineVersion> MedicineVersions { get; set; }
		public DbSet<Maker> Makers { get; set; }

		public AppDbContext()
		{
			Database.EnsureCreated();
		}

		public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
		{ }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// This loop finds every DateTimeOffset property in your database 
			// and adds a converter that calls .ToUniversalTime() before saving.
			foreach (var entityType in modelBuilder.Model.GetEntityTypes())
			{
				foreach (var property in entityType.GetProperties())
				{
					if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
					{
						property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, DateTimeOffset>(
							v => v.ToUniversalTime(), // Convert to UTC when writing to DB
							v => v // Keep as is when reading from DB
						));
					}
				}
			}

			// Configure Cascade Delete for relationships.
			modelBuilder.Entity<Medicine>()
				.HasMany(m => m.Analogs)
				.WithOne(a => a.Medicine)
				.HasForeignKey(a => a.MedicineId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<Medicine>()
				.HasMany(m => m.Versions)
				.WithOne(v => v.Medicine)
				.HasForeignKey(v => v.MedicineId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<MedicineVersion>()
				.HasMany(v => v.Makers)
				.WithOne(m => m.MedicineVersion)
				.HasForeignKey(m => m.MedicineVersionId)
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}
