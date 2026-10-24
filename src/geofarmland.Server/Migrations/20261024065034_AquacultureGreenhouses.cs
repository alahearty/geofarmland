using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace geofarmland.Server.Migrations
{
    /// <inheritdoc />
    public partial class AquacultureGreenhouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AquaculturePonds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    FarmId = table.Column<int>(type: "integer", nullable: false),
                    LandParcelId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    WaterType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SystemType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AreaM2 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Species = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    StockCount = table.Column<int>(type: "integer", nullable: false),
                    AverageWeightG = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LastStockedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AquaculturePonds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AquaculturePonds_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AquaculturePonds_LandParcels_LandParcelId",
                        column: x => x.LandParcelId,
                        principalTable: "LandParcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AquaculturePonds_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GreenhouseUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    FarmId = table.Column<int>(type: "integer", nullable: false),
                    LandParcelId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StructureType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AreaM2 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentCrop = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GreenhouseUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GreenhouseUnits_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GreenhouseUnits_LandParcels_LandParcelId",
                        column: x => x.LandParcelId,
                        principalTable: "LandParcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GreenhouseUnits_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AquaculturePonds_FarmId",
                table: "AquaculturePonds",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_AquaculturePonds_LandParcelId",
                table: "AquaculturePonds",
                column: "LandParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_AquaculturePonds_OrganizationId_Status",
                table: "AquaculturePonds",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GreenhouseUnits_FarmId",
                table: "GreenhouseUnits",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_GreenhouseUnits_LandParcelId",
                table: "GreenhouseUnits",
                column: "LandParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_GreenhouseUnits_OrganizationId_Status",
                table: "GreenhouseUnits",
                columns: new[] { "OrganizationId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AquaculturePonds");

            migrationBuilder.DropTable(
                name: "GreenhouseUnits");
        }
    }
}
