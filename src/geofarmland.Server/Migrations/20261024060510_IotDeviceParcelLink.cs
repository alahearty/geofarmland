using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace geofarmland.Server.Migrations
{
    /// <inheritdoc />
    public partial class IotDeviceParcelLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LandParcelId",
                table: "Devices",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_LandParcelId",
                table: "Devices",
                column: "LandParcelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_LandParcels_LandParcelId",
                table: "Devices",
                column: "LandParcelId",
                principalTable: "LandParcels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_LandParcels_LandParcelId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_LandParcelId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LandParcelId",
                table: "Devices");
        }
    }
}
