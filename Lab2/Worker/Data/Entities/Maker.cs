namespace Worker.Data.Entities
{
	public class Maker
	{
		public int Id { get; set; }

		// Foreign Key.
		public int MedicineVersionId { get; set; }
		public MedicineVersion? MedicineVersion { get; set; }

		// --- Certificate details ---
		public string CertNumber { get; set; } = string.Empty;
		public DateTimeOffset CertIssueDate { get; set; }
		public DateTimeOffset CertExpiryDate { get; set; }
		public string CertOrganization { get; set; } = string.Empty;

		// --- Package details ---
		public string PackageType { get; set; } = string.Empty;
		public int PackageQuantity { get; set; }
		public decimal PackagePrice { get; set; }

		// --- Dosage details ---
		public string DosageAmount { get; set; } = string.Empty;
		public string DosageFrequency { get; set; } = string.Empty;
	}
}
