using Microsoft.EntityFrameworkCore;
using Shared.DTOs;
using Worker.Data;
using Worker.Data.Entities;

namespace Worker.Services
{
	public class MedicineService : IMedicineService
	{
		private readonly IDbContextFactory<AppDbContext> _rContextFactory;
		private readonly ILogger<MedicineService> _rLogger;

		public MedicineService(IDbContextFactory<AppDbContext> contextFactory, ILogger<MedicineService> logger)
		{
			_rContextFactory = contextFactory;
			_rLogger = logger;
		}

		public async Task SaveMedicineAsync(MedicineDto dto)
		{
			_rLogger.LogInformation("Mapping and saving medicine: {MedicineName}", dto.Name);

			// Create a fresh DbContext instance for this specific operation.
			using var dbContext = await _rContextFactory.CreateDbContextAsync();

			var medicineEntity = new Medicine
			{
				Name = dto.Name,
				Pharm = dto.Pharm,
				Group = dto.Group,
				Analogs = dto.Analogs.Select(a => new Analog { Name = a.Name }).ToList(),
				Versions = dto.Versions.Select(v => new MedicineVersion
				{
					Type = v.Type,
					Makers = v.Makers.Select(m => new Maker
					{
						CertNumber = m.Certificate.Number,
						CertIssueDate = m.Certificate.IssueDate,
						CertExpiryDate = m.Certificate.ExpiryDate,
						CertOrganization = m.Certificate.Organization,
						PackageType = m.Package.Type,
						PackageQuantity = m.Package.Quantity,
						PackagePrice = m.Package.Price,
						DosageAmount = m.Dosage.Amount,
						DosageFrequency = m.Dosage.Frequency
					}).ToList()
				}).ToList()
			};

			dbContext.Medicines.Add(medicineEntity);
			await dbContext.SaveChangesAsync();

			_rLogger.LogInformation("Medicine {MedicineName} successfully saved to DB.", dto.Name);
		}
	}
}
