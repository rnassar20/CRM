using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Crm.Api.Migrations
{
    /// <inheritdoc />
    public partial class UnifyPersonCoreAndDropLegacy : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// ⚠️ DATA LOSS: this migration repoints all CRM FKs from the legacy
        /// `Clients` / `Users` tables to the unified `persons` table, then DROPS
        /// `ClientContacts`, `Clients` and `Users`.
        ///
        /// The CRM never writes these legacy tables anymore (staff are `persons` PersonType=11 +
        /// `person_credentials`; clients are `persons` PersonType=12 + `crm_client_extension`),
        /// so on a freshly seeded DB there is nothing to lose. If a database still holds
        /// meaningful rows in `Clients`/`Users`, run the data migration below BEFORE applying
        /// this migration (or comment out the three DropTable calls to keep the tables):
        ///
        ///   -- 1) legacy clients -> persons (PersonType=12) + crm_client_extension
        ///   INSERT INTO persons (profile_id, person_type, first_name, phone, email,
        ///                         status, created_at, updated_at)
        ///   SELECT 1, 12, c."Name", c."Phone", c."Email", '1', now(), now()
        ///   FROM "Clients" c
        ///   WHERE NOT EXISTS (SELECT 1 FROM persons p WHERE p.first_name = c."Name" AND p.person_type = 12);
        ///   INSERT INTO crm_client_extension (person_id, status, client_type, created_at, updated_at)
        ///   SELECT p.id, c."Status"::text, c."Type"::text, now(), now()
        ///   FROM "Clients" c JOIN persons p ON p.person_type = 12 AND p.first_name = c."Name";
        ///
        ///   -- 2) legacy users -> persons (PersonType=11) + person_credentials
        ///   INSERT INTO persons (profile_id, person_type, first_name, email, status, created_at, updated_at)
        ///   SELECT 1, 11, u."FullName", u."Email", '1', now(), now()
        ///   FROM "Users" u WHERE NOT EXISTS (SELECT 1 FROM persons p WHERE p.email = u."Email");
        ///   INSERT INTO person_credentials (person_id, username, password_hash, access_level,
        ///                                    must_reset, created_at, updated_at)
        ///   SELECT p.id, u."Email", u."PasswordHash",
        ///          CASE WHEN u."Role" = 'Admin' THEN 1 ELSE 2 END, false, now(), now()
        ///   FROM "Users" u JOIN persons p ON p.email = u."Email";
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_Clients_ClientId",
                table: "FollowUps");

            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_Users_AssignedToId",
                table: "FollowUps");

            migrationBuilder.DropForeignKey(
                name: "FK_Interactions_Clients_ClientId",
                table: "Interactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Interactions_Users_UserId",
                table: "Interactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Clients_ClientId",
                table: "Subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_SubscriptionPlans_PlanId",
                table: "Subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketComments_Users_UserId",
                table: "TicketComments");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Clients_ClientId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_AssignedToId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_CreatedById",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_WhatsAppMessages_Clients_ClientId",
                table: "WhatsAppMessages");

            migrationBuilder.DropTable(
                name: "ClientContacts");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_CreatedById",
                table: "FollowUps",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_persons_AssignedToId",
                table: "FollowUps",
                column: "AssignedToId",
                principalTable: "persons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_persons_ClientId",
                table: "FollowUps",
                column: "ClientId",
                principalTable: "persons",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_persons_CreatedById",
                table: "FollowUps",
                column: "CreatedById",
                principalTable: "persons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Interactions_persons_ClientId",
                table: "Interactions",
                column: "ClientId",
                principalTable: "persons",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Interactions_persons_UserId",
                table: "Interactions",
                column: "UserId",
                principalTable: "persons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_SubscriptionPlans_PlanId",
                table: "Subscriptions",
                column: "PlanId",
                principalTable: "SubscriptionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_persons_ClientId",
                table: "Subscriptions",
                column: "ClientId",
                principalTable: "persons",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketComments_persons_UserId",
                table: "TicketComments",
                column: "UserId",
                principalTable: "persons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_persons_AssignedToId",
                table: "Tickets",
                column: "AssignedToId",
                principalTable: "persons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_persons_ClientId",
                table: "Tickets",
                column: "ClientId",
                principalTable: "persons",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_persons_CreatedById",
                table: "Tickets",
                column: "CreatedById",
                principalTable: "persons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WhatsAppMessages_persons_ClientId",
                table: "WhatsAppMessages",
                column: "ClientId",
                principalTable: "persons",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_persons_AssignedToId",
                table: "FollowUps");

            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_persons_ClientId",
                table: "FollowUps");

            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_persons_CreatedById",
                table: "FollowUps");

            migrationBuilder.DropForeignKey(
                name: "FK_Interactions_persons_ClientId",
                table: "Interactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Interactions_persons_UserId",
                table: "Interactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_SubscriptionPlans_PlanId",
                table: "Subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_persons_ClientId",
                table: "Subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketComments_persons_UserId",
                table: "TicketComments");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_persons_AssignedToId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_persons_ClientId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_persons_CreatedById",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_WhatsAppMessages_persons_ClientId",
                table: "WhatsAppMessages");

            migrationBuilder.DropIndex(
                name: "IX_FollowUps_CreatedById",
                table: "FollowUps");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    ContactPerson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    AllowWhatsApp = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: false)
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
                name: "IX_ClientContacts_ClientId",
                table: "ClientContacts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CreatedById",
                table: "Clients",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_Clients_ClientId",
                table: "FollowUps",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_Users_AssignedToId",
                table: "FollowUps",
                column: "AssignedToId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Interactions_Clients_ClientId",
                table: "Interactions",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Interactions_Users_UserId",
                table: "Interactions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Clients_ClientId",
                table: "Subscriptions",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_SubscriptionPlans_PlanId",
                table: "Subscriptions",
                column: "PlanId",
                principalTable: "SubscriptionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketComments_Users_UserId",
                table: "TicketComments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Clients_ClientId",
                table: "Tickets",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_AssignedToId",
                table: "Tickets",
                column: "AssignedToId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_CreatedById",
                table: "Tickets",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WhatsAppMessages_Clients_ClientId",
                table: "WhatsAppMessages",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id");
        }
    }
}
