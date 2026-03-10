using Shared.DTOs;

namespace Worker.Services
{
	public interface IMedicineService
	{
		/// <summary>
		/// Maps the DTO to database entities and saves them to PostgreSQL.
		/// </summary>
		/// <param name="medicineDto">The medicine data transfer object.</param>
		Task SaveMedicineAsync(MedicineDto medicineDto);
	}
}
