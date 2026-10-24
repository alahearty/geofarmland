using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace geofarmland.Server.Migrations
{
    /// <inheritdoc />
    public partial class LivestockProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LivestockGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    FarmId = table.Column<int>(type: "integer", nullable: false),
                    GrazingLandParcelId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Species = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Breed = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    HeadCount = table.Column<int>(type: "integer", nullable: false),
                    AverageWeightKg = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    JoinedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LivestockGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LivestockGroups_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LivestockGroups_LandParcels_GrazingLandParcelId",
                        column: x => x.GrazingLandParcelId,
                        principalTable: "LandParcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LivestockGroups_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LivestockEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    LivestockGroupId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: false),
                    HeadCount = table.Column<int>(type: "integer", nullable: true),
                    WeightKg = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LivestockEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LivestockEvents_LivestockGroups_LivestockGroupId",
                        column: x => x.LivestockGroupId,
                        principalTable: "LivestockGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LivestockEvents_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LivestockEvents_LivestockGroupId_EventDate",
                table: "LivestockEvents",
                columns: new[] { "LivestockGroupId", "EventDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LivestockEvents_OrganizationId_EventDate",
                table: "LivestockEvents",
                columns: new[] { "OrganizationId", "EventDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LivestockGroups_FarmId",
                table: "LivestockGroups",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_LivestockGroups_GrazingLandParcelId",
                table: "LivestockGroups",
                column: "GrazingLandParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_LivestockGroups_OrganizationId_Status",
                table: "LivestockGroups",
                columns: new[] { "OrganizationId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LivestockEvents");

            migrationBuilder.DropTable(
                name: "LivestockGroups");
        }
    }
}
