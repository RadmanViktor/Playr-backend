using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Playr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiplePostMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "MediaUrl",
                table: "Posts");

            migrationBuilder.CreateTable(
                name: "PostMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostMedia_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostMedia_PostId_SortOrder",
                table: "PostMedia",
                columns: new[] { "PostId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostMedia");

            migrationBuilder.AddColumn<string>(
                name: "MediaType",
                table: "Posts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaUrl",
                table: "Posts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
