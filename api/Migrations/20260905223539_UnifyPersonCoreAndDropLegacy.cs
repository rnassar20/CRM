using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Api.Migrations
{
    /// <inheritdoc />
    public partial class UnifyPersonCoreAndDropLegacy : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// SAFE / NON-DESTRUCTIVE: this migration repoints all CRM FKs from the legacy
        /// `Clients` / `Users` tables to the unified `persons` table, then RENAMES the legacy
        /// `ClientContacts`, `Clients` and `Users` tables to `*_TOBEDELETED` (instead of dropping
        /// them). This preserves any existing data so it can be audited / reconciled before a
        /// later, manual cleanup deletes those tables.
        ///
        /// The CRM never reads or writes these legacy tables anymore (staff are `persons`
        /// PersonType=11 + `person_credentials`; clients are `persons` PersonType=12 +
        /// `crm_client_extension`), so they are inert after this migration. Delete the
        /// `*_TOBEDELETED` tables once their contents have been verified.
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

            // Preserve (don't drop) the legacy tables; mark them for eventual manual cleanup.
            migrationBuilder.RenameTable(
                name: "ClientContacts",
                newName: "ClientContacts_TOBEDELETED");

            migrationBuilder.RenameTable(
                name: "Clients",
                newName: "Clients_TOBEDELETED");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "Users_TOBEDELETED");

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

            // Restore the legacy table names (contents were preserved, never dropped).
            migrationBuilder.RenameTable(
                name: "ClientContacts_TOBEDELETED",
                newName: "ClientContacts");

            migrationBuilder.RenameTable(
                name: "Clients_TOBEDELETED",
                newName: "Clients");

            migrationBuilder.RenameTable(
                name: "Users_TOBEDELETED",
                newName: "Users");

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
