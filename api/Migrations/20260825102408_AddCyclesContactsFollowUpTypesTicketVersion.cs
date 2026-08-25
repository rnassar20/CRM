using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Crm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCyclesContactsFollowUpTypesTicketVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "SubscriptionPlans");

            migrationBuilder.AddColumn<string>(
                name: "ResolvedVersion",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cycle",
                table: "Subscriptions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Yearly");

            migrationBuilder.AddColumn<string>(
                name: "Cycle",
                table: "SubscriptionPlans",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Yearly");

            migrationBuilder.AddColumn<int>(
                name: "TicketId",
                table: "FollowUps",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "FollowUps",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "Marketing");

            migrationBuilder.CreateTable(
                name: "ClientContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    AllowWhatsApp = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientContacts_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_TicketId",
                table: "FollowUps",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientContacts_ClientId",
                table: "ClientContacts",
                column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_Tickets_TicketId",
                table: "FollowUps",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_Tickets_TicketId",
                table: "FollowUps");

            migrationBuilder.DropTable(
                name: "ClientContacts");

            migrationBuilder.DropIndex(
                name: "IX_FollowUps_TicketId",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "ResolvedVersion",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Cycle",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "Cycle",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "TicketId",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "FollowUps");

            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "SubscriptionPlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
