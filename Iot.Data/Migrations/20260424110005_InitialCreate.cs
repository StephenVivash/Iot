using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace Iot.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParentDeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Points",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RawStatus = table.Column<double>(type: "REAL", nullable: false),
                    Status0 = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status1 = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Scale = table.Column<double>(type: "REAL", nullable: false),
                    Units = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Points", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Points_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Devices",
                columns: new[] { "Id", "Description", "Name", "ParentDeviceId", "Status", "TypeId" },
                values: new object[,]
                {
                    { 1, "Primary network server for the demo site", "Main NET Server", 0, "Online", 0 },
                    { 2, "Local nano controller handling plant room IO", "Nano Controller 1", 1, "Online", 1 },
                    { 3, "Wireless LoRa gateway for remote sensors", "LoRa Gateway", 1, "Warning", 2 },
                    { 4, "Client display for local operators", "Operator Panel", 2, "Online", 3 }
                });

            migrationBuilder.InsertData(
                table: "Points",
                columns: new[] { "Id", "Address", "Description", "DeviceId", "Name", "RawStatus", "Scale", "Status", "Status0", "Status1", "TimeStamp", "TypeId", "Units" },
                values: new object[,]
                {
                    { 1, "DI:1", "Digital input showing the pump run state", 2, "Pump Run Feedback", 1.0, 1.0, "On", "Stopped", "Running", new DateTime(2026, 4, 24, 8, 0, 0, 0, DateTimeKind.Utc), 0, "" },
                    { 2, "DO:1", "Digital output command for pump start", 2, "Pump Command", 1.0, 1.0, "On", "Off", "On", new DateTime(2026, 4, 24, 8, 0, 5, 0, DateTimeKind.Utc), 1, "" },
                    { 3, "AI:1", "Analog tank level from remote sensor", 3, "Tank Level", 684.0, 0.10000000000000001, "68.4", "Low", "High", new DateTime(2026, 4, 24, 8, 1, 0, 0, DateTimeKind.Utc), 2, "%" },
                    { 4, "AO:1", "Analog output controlling valve opening", 2, "Valve Position", 450.0, 0.10000000000000001, "45.0", "Closed", "Open", new DateTime(2026, 4, 24, 8, 1, 30, 0, DateTimeKind.Utc), 3, "%" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Points_DeviceId",
                table: "Points",
                column: "DeviceId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Points");

            migrationBuilder.DropTable(
                name: "Devices");
        }
    }
}
