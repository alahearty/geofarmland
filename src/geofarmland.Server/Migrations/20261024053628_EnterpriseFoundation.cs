using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace geofarmland.Server.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Country = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Farms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Region = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Country = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Farms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Farms_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationMembers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandParcels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    FarmId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Geometry = table.Column<Polygon>(type: "geometry", nullable: false),
                    AreaHectares = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LandUse = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TenureType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LegacyPlotId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandParcels", x => x.Id);
                    table.CheckConstraint("CK_LandParcels_Srid", "ST_SRID(\"Geometry\") = 4326");
                    table.CheckConstraint("CK_LandParcels_ValidGeometry", "ST_IsValid(\"Geometry\")");
                    table.ForeignKey(
                        name: "FK_LandParcels_Farms_FarmId",
                        column: x => x.FarmId,
                        principalTable: "Farms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandParcels_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Farms_OrganizationId_Code",
                table: "Farms",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandParcels_FarmId",
                table: "LandParcels",
                column: "FarmId");

            migrationBuilder.CreateIndex(
                name: "IX_LandParcels_LegacyPlotId",
                table: "LandParcels",
                column: "LegacyPlotId");

            migrationBuilder.CreateIndex(
                name: "IX_LandParcels_OrganizationId_FarmId",
                table: "LandParcels",
                columns: new[] { "OrganizationId", "FarmId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_OrganizationId_UserId",
                table: "OrganizationMembers",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMembers_UserId",
                table: "OrganizationMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_IsActive",
                table: "Organizations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Slug",
                table: "Organizations",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LandParcels");

            migrationBuilder.DropTable(
                name: "OrganizationMembers");

            migrationBuilder.DropTable(
                name: "Farms");

            migrationBuilder.DropTable(
                name: "Organizations");
        }
    }
}
