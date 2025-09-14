using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dncasbin.Migrations
{
    /// <inheritdoc />
    public partial class Casbin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "casbin");

            migrationBuilder.CreateTable(
                name: "casbin_rule",
                schema: "casbin",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ptype = table.Column<string>(type: "text", nullable: true),
                    v0 = table.Column<string>(type: "text", nullable: true),
                    v1 = table.Column<string>(type: "text", nullable: true),
                    v2 = table.Column<string>(type: "text", nullable: true),
                    v3 = table.Column<string>(type: "text", nullable: true),
                    v4 = table.Column<string>(type: "text", nullable: true),
                    v5 = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_casbin_rule", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_casbin_rule_ptype",
                schema: "casbin",
                table: "casbin_rule",
                column: "ptype");

            migrationBuilder.CreateIndex(
                name: "IX_casbin_rule_v0",
                schema: "casbin",
                table: "casbin_rule",
                column: "v0");

            migrationBuilder.CreateIndex(
                name: "IX_casbin_rule_v1",
                schema: "casbin",
                table: "casbin_rule",
                column: "v1");

            migrationBuilder.CreateIndex(
                name: "IX_casbin_rule_v2",
                schema: "casbin",
                table: "casbin_rule",
                column: "v2");

            migrationBuilder.CreateIndex(
                name: "IX_casbin_rule_v3",
                schema: "casbin",
                table: "casbin_rule",
                column: "v3");

            migrationBuilder.CreateIndex(
                name: "IX_casbin_rule_v4",
                schema: "casbin",
                table: "casbin_rule",
                column: "v4");

            migrationBuilder.CreateIndex(
                name: "IX_casbin_rule_v5",
                schema: "casbin",
                table: "casbin_rule",
                column: "v5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "casbin_rule",
                schema: "casbin");
        }
    }
}
