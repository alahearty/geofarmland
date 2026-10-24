using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace geofarmland.Server.Migrations
{
    /// <inheritdoc />
    public partial class P0OwnershipAndHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Value",
                table: "SensorData",
                newName: "SoilMoisture");

            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                table: "SensorData",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "SensorData"
                SET "DeviceId" = COALESCE(regexp_replace("SensorType", '^Device-', ''), 'legacy-sensor')
                WHERE "DeviceId" = '';
                """);

            migrationBuilder.DropColumn(
                name: "SensorType",
                table: "SensorData");

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "SensorData",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "SensorData",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "SensorData",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Rainfall",
                table: "SensorData",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Plots",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Plots",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensorData_OwnerId",
                table: "SensorData",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plots_OwnerId",
                table: "Plots",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Plots_AspNetUsers_OwnerId",
                table: "Plots",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SensorData_AspNetUsers_OwnerId",
                table: "SensorData",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Plots_AspNetUsers_OwnerId",
                table: "Plots");

            migrationBuilder.DropForeignKey(
                name: "FK_SensorData_AspNetUsers_OwnerId",
                table: "SensorData");

            migrationBuilder.DropIndex(
                name: "IX_SensorData_OwnerId",
                table: "SensorData");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Plots_OwnerId",
                table: "Plots");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "SensorData");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "SensorData");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "SensorData");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "SensorData");

            migrationBuilder.DropColumn(
                name: "Rainfall",
                table: "SensorData");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Plots");

            migrationBuilder.RenameColumn(
                name: "SoilMoisture",
                table: "SensorData",
                newName: "Value");

            migrationBuilder.AddColumn<string>(
                name: "SensorType",
                table: "SensorData",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Plots",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);
        }
    }
}
