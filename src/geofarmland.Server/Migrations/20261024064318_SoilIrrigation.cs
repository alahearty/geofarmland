using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace geofarmland.Server.Migrations
{
    /// <inheritdoc />
    public partial class SoilIrrigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IrrigationEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    FarmId = table.Column<int>(type: "integer", nullable: false),
                    LandParcelId = table.Column<int>(type: "integer", nullable: false),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VolumeM3 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IrrigationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IrrigationEvents_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IrrigationEvents_LandParcels_LandParcelId",
                        column: x => x.LandParcelId,
                        principalTable: "LandParcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IrrigationEvents_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoilSamples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    FarmId = table.Column<int>(type: "integer", nullable: false),
                    LandParcelId = table.Column<int>(type: "integer", nullable: false),
                    SampleDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PH = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    OrganicMatterPct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    NitrogenPpm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    PhosphorusPpm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    PotassiumPpm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Laboratory = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoilSamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoilSamples_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoilSamples_LandParcels_LandParcelId",
                        column: x => x.LandParcelId,
                        principalTable: "LandParcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoilSamples_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationEvents_FarmId",
                table: "IrrigationEvents",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationEvents_LandParcelId",
                table: "IrrigationEvents",
                column: "LandParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationEvents_OrganizationId_EventDate",
                table: "IrrigationEvents",
                columns: new[] { "OrganizationId", "EventDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SoilSamples_FarmId",
                table: "SoilSamples",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_SoilSamples_LandParcelId",
                table: "SoilSamples",
                column: "LandParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_SoilSamples_OrganizationId_SampleDate",
                table: "SoilSamples",
                columns: new[] { "OrganizationId", "SampleDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IrrigationEvents");

            migrationBuilder.DropTable(
                name: "SoilSamples");
        }
    }
}
