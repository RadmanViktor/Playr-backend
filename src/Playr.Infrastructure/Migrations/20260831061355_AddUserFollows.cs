using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFollows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserFollows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowingUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFollows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFollows_AspNetUsers_FollowerUserId",
                        column: x => x.FollowerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserFollows_AspNetUsers_FollowingUserId",
                        column: x => x.FollowingUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFollows_FollowerUserId_FollowingUserId",
                table: "UserFollows",
                columns: new[] { "FollowerUserId", "FollowingUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFollows_FollowingUserId",
                table: "UserFollows",
                column: "FollowingUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFollows");
        }
    }
}
