using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace geofarmland.Server.Migrations
{
    /// <inheritdoc />
    public partial class InventoryFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    FarmId = table.Column<int>(type: "integer", nullable: true),
                    RecordType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecordDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialRecords_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FinancialRecords_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ItemType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Supplier = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialRecords_FarmId",
                table: "FinancialRecords",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialRecords_OrganizationId_RecordDate",
                table: "FinancialRecords",
                columns: new[] { "OrganizationId", "RecordDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_OrganizationId_ItemType",
                table: "InventoryItems",
                columns: new[] { "OrganizationId", "ItemType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialRecords");

            migrationBuilder.DropTable(
                name: "InventoryItems");
        }
    }
}
