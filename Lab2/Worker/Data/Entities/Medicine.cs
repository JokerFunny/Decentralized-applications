namespace Worker.Data.Entities
{
	public class Medicine
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Pharm { get; set; } = string.Empty;
		public string Group { get; set; } = string.Empty;

		// Navigation properties (One-to-Many relationships).
		public List<Analog> Analogs { get; set; } = new();
		public List<MedicineVersion> Versions { get; set; } = new();
	}
}
