using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace geofarmland.Server.Migrations
{
    /// <inheritdoc />
    public partial class FieldOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FarmTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    FarmId = table.Column<int>(type: "integer", nullable: false),
                    LandParcelId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TaskType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FarmTasks_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FarmTasks_LandParcels_LandParcelId",
                        column: x => x.LandParcelId,
                        principalTable: "LandParcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FarmTasks_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FarmTasks_FarmId",
                table: "FarmTasks",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmTasks_LandParcelId",
                table: "FarmTasks",
                column: "LandParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmTasks_OrganizationId_Status_DueDate",
                table: "FarmTasks",
                columns: new[] { "OrganizationId", "Status", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FarmTasks");
        }
    }
}
