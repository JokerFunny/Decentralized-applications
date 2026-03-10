namespace Shared.DTOs
{
	/// <summary>
	/// Root wrapper for the medicine request payload.
	/// Matches the requirement: "Кореневий елемент називається Medicine".
	/// </summary>
	public class MedicineRootDto
	{
		public MedicineDto Medicine { get; set; }
	}

	/// <summary>
	/// Core medicine details.
	/// </summary>
	public class MedicineDto
	{
		// Name of the medicine (e.g., "Ibuprofen")
		public string Name { get; set; }

		// Manufacturing firm (pharmaceutical company)
		public string Pharm { get; set; }

		// Pharmacological group (e.g., "Painkillers", "Antibiotics")
		public string Group { get; set; }

		// List of analog medicines
		public List<AnalogDto> Analogs { get; set; } = new();

		// Different forms of execution (e.g., Tablets, Drops)
		public List<MedicineVersionDto> Versions { get; set; } = new();
	}

	/// <summary>
	/// Represents an analog of the main medicine.
	/// </summary>
	public class AnalogDto
	{
		// Name of the analog medicine
		public string Name { get; set; }
	}

	/// <summary>
	/// Represents a specific execution form (version) of the medicine.
	/// </summary>
	public class MedicineVersionDto
	{
		// Physical form/consistency (e.g., "Tablets", "Capsules", "Powder")
		public string Type { get; set; }

		// List of makers/manufacturers producing this specific version
		public List<MakerDto> Makers { get; set; } = new();
	}

	/// <summary>
	/// Represents a specific manufacturer's offering for a medicine version.
	/// Grouping Certificate, Package, and Dosage as requested.
	/// </summary>
	public class MakerDto
	{
		public CertificateDto Certificate { get; set; }
		public PackageDto Package { get; set; }
		public DosageDto Dosage { get; set; }
	}

	/// <summary>
	/// Details about the medicine's registration certificate.
	/// </summary>
	public class CertificateDto
	{
		// Certificate registration number
		public string Number { get; set; }

		// Date when the certificate was issued
		public DateTimeOffset IssueDate { get; set; }

		// Expiration date of the certificate
		public DateTimeOffset ExpiryDate { get; set; }

		// Organization that issued the certificate (e.g., "Ministry of Health")
		public string Organization { get; set; }
	}

	/// <summary>
	/// Details about the packaging.
	/// </summary>
	public class PackageDto
	{
		// Type of packaging (e.g., "Blister", "Bottle", "Box")
		public string Type { get; set; }

		// Number of items in the package (e.g., 10 pills)
		public int Quantity { get; set; }

		// Price per package (using decimal for financial data)
		public decimal Price { get; set; }
	}

	/// <summary>
	/// Details about how to take the medicine.
	/// </summary>
	public class DosageDto
	{
		// Amount per intake (e.g., "400 mg", "10 ml")
		public string Amount { get; set; }

		// Frequency of intake (e.g., "Every 8 hours, max 3 times a day")
		public string Frequency { get; set; }
	}
}
