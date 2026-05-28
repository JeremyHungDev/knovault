using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knovault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewerName = table.Column<string>(type: "TEXT", nullable: true),
                    Rating = table.Column<float>(type: "REAL", nullable: true),
                    ReviewText = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewDate = table.Column<string>(type: "TEXT", nullable: true),
                    HelpfulCount = table.Column<int>(type: "INTEGER", nullable: true),
                    FetchedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalReviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalReviews_BookId_Source",
                table: "ExternalReviews",
                columns: new[] { "BookId", "Source" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalReviews");
        }
    }
}
