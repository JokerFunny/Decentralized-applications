namespace Worker.Data.Entities
{
	public class Analog
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;

		// Foreign Key.
		public int MedicineId { get; set; }

		// Reference to the parent object.
		public Medicine? Medicine { get; set; }
	}
}
