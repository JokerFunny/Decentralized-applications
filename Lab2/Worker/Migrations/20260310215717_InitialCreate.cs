using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Worker.Migrations
{
	/// <inheritdoc />
	public partial class InitialCreate : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "Medicines",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					Name = table.Column<string>(type: "text", nullable: false),
					Pharm = table.Column<string>(type: "text", nullable: false),
					Group = table.Column<string>(type: "text", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Medicines", x => x.Id);
				});

			migrationBuilder.CreateTable(
				name: "Analogs",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					Name = table.Column<string>(type: "text", nullable: false),
					MedicineId = table.Column<int>(type: "integer", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Analogs", x => x.Id);
					table.ForeignKey(
						name: "FK_Analogs_Medicines_MedicineId",
						column: x => x.MedicineId,
						principalTable: "Medicines",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "MedicineVersions",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					Type = table.Column<string>(type: "text", nullable: false),
					MedicineId = table.Column<int>(type: "integer", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_MedicineVersions", x => x.Id);
					table.ForeignKey(
						name: "FK_MedicineVersions_Medicines_MedicineId",
						column: x => x.MedicineId,
						principalTable: "Medicines",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateTable(
				name: "Makers",
				columns: table => new
				{
					Id = table.Column<int>(type: "integer", nullable: false)
						.Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
					MedicineVersionId = table.Column<int>(type: "integer", nullable: false),
					CertNumber = table.Column<string>(type: "text", nullable: false),
					CertIssueDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					CertExpiryDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
					CertOrganization = table.Column<string>(type: "text", nullable: false),
					PackageType = table.Column<string>(type: "text", nullable: false),
					PackageQuantity = table.Column<int>(type: "integer", nullable: false),
					PackagePrice = table.Column<decimal>(type: "numeric", nullable: false),
					DosageAmount = table.Column<string>(type: "text", nullable: false),
					DosageFrequency = table.Column<string>(type: "text", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Makers", x => x.Id);
					table.ForeignKey(
						name: "FK_Makers_MedicineVersions_MedicineVersionId",
						column: x => x.MedicineVersionId,
						principalTable: "MedicineVersions",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});

			migrationBuilder.CreateIndex(
				name: "IX_Analogs_MedicineId",
				table: "Analogs",
				column: "MedicineId");

			migrationBuilder.CreateIndex(
				name: "IX_Makers_MedicineVersionId",
				table: "Makers",
				column: "MedicineVersionId");

			migrationBuilder.CreateIndex(
				name: "IX_MedicineVersions_MedicineId",
				table: "MedicineVersions",
				column: "MedicineId");
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "Analogs");

			migrationBuilder.DropTable(
				name: "Makers");

			migrationBuilder.DropTable(
				name: "MedicineVersions");

			migrationBuilder.DropTable(
				name: "Medicines");
		}
	}
}
