using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace X39.UnitedTacticalForces.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWikiPage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WikiPages",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    TimeStampCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiPages", x => x.PrimaryKey);
                });

            migrationBuilder.CreateTable(
                name: "WikiPageAudits",
                columns: table => new
                {
                    PrimaryKey = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PageForeignKey = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeStampCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserForeignKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiPageAudits", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_WikiPageAudits_Users_UserForeignKey",
                        column: x => x.UserForeignKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WikiPageAudits_WikiPages_PageForeignKey",
                        column: x => x.PageForeignKey,
                        principalTable: "WikiPages",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WikiPageRevisions",
                columns: table => new
                {
                    PrimaryKey = table.Column<Guid>(type: "uuid", nullable: false),
                    PageForeignKey = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeStampCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Markdown = table.Column<string>(type: "text", nullable: false),
                    AuthorForeignKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiPageRevisions", x => x.PrimaryKey);
                    table.ForeignKey(
                        name: "FK_WikiPageRevisions_Users_AuthorForeignKey",
                        column: x => x.AuthorForeignKey,
                        principalTable: "Users",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WikiPageRevisions_WikiPages_PageForeignKey",
                        column: x => x.PageForeignKey,
                        principalTable: "WikiPages",
                        principalColumn: "PrimaryKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WikiPageAudits_PageForeignKey_TimeStampCreated",
                table: "WikiPageAudits",
                columns: new[] { "PageForeignKey", "TimeStampCreated" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_WikiPageAudits_UserForeignKey",
                table: "WikiPageAudits",
                column: "UserForeignKey");

            migrationBuilder.CreateIndex(
                name: "IX_WikiPageRevisions_AuthorForeignKey",
                table: "WikiPageRevisions",
                column: "AuthorForeignKey");

            migrationBuilder.CreateIndex(
                name: "IX_WikiPageRevisions_PageForeignKey_TimeStampCreated",
                table: "WikiPageRevisions",
                columns: new[] { "PageForeignKey", "TimeStampCreated" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_WikiPages_Title",
                table: "WikiPages",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WikiPageAudits");

            migrationBuilder.DropTable(
                name: "WikiPageRevisions");

            migrationBuilder.DropTable(
                name: "WikiPages");
        }
    }
}
