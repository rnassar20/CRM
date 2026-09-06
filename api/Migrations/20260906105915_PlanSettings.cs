using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Api.Migrations
{
    /// <inheritdoc />
    public partial class PlanSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plan_settings",
                columns: table => new
                {
                    plan_id = table.Column<int>(type: "integer", nullable: false),
                    page = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    pscode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_settings", x => new { x.plan_id, x.page, x.pscode });
                    table.ForeignKey(
                        name: "FK_plan_settings_SubscriptionPlans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_settings");
        }
    }
}
