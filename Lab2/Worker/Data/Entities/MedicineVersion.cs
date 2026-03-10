namespace Worker.Data.Entities
{
	public class MedicineVersion
	{
		public int Id { get; set; }
		public string Type { get; set; } = string.Empty;

		// Foreign Key.
		public int MedicineId { get; set; }
		public Medicine? Medicine { get; set; }

		// Navigation property (One-to-Many).
		public List<Maker> Makers { get; set; } = new();
	}
}
