using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace geofarmland.Server.Migrations
{
    /// <inheritdoc />
    public partial class CropProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Crops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Variety = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Crops_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CropSeasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    FarmId = table.Column<int>(type: "integer", nullable: false),
                    LandParcelId = table.Column<int>(type: "integer", nullable: false),
                    CropId = table.Column<int>(type: "integer", nullable: false),
                    SeasonName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PlantingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedHarvestDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualHarvestDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AreaHectares = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ExpectedYieldTons = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropSeasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropSeasons_Crops_CropId",
                        column: x => x.CropId,
                        principalTable: "Crops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CropSeasons_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CropSeasons_LandParcels_LandParcelId",
                        column: x => x.LandParcelId,
                        principalTable: "LandParcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CropSeasons_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HarvestRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    CropSeasonId = table.Column<int>(type: "integer", nullable: false),
                    HarvestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    QuantityTons = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Grade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HarvestRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HarvestRecords_CropSeasons_CropSeasonId",
                        column: x => x.CropSeasonId,
                        principalTable: "CropSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HarvestRecords_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Crops_OrganizationId_Name",
                table: "Crops",
                columns: new[] { "OrganizationId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_CropSeasons_CropId",
                table: "CropSeasons",
                column: "CropId");

            migrationBuilder.CreateIndex(
                name: "IX_CropSeasons_FarmId",
                table: "CropSeasons",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_CropSeasons_LandParcelId",
                table: "CropSeasons",
                column: "LandParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_CropSeasons_OrganizationId_Status_PlantingDate",
                table: "CropSeasons",
                columns: new[] { "OrganizationId", "Status", "PlantingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HarvestRecords_CropSeasonId",
                table: "HarvestRecords",
                column: "CropSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_HarvestRecords_OrganizationId_HarvestDate",
                table: "HarvestRecords",
                columns: new[] { "OrganizationId", "HarvestDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HarvestRecords");

            migrationBuilder.DropTable(
                name: "CropSeasons");

            migrationBuilder.DropTable(
                name: "Crops");
        }
    }
}
