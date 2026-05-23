using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knovault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Books",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Subtitle = table.Column<string>(type: "TEXT", nullable: true),
                    Language = table.Column<string>(type: "TEXT", nullable: true),
                    Publisher = table.Column<string>(type: "TEXT", nullable: true),
                    PublishedDate = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Isbn = table.Column<string>(type: "TEXT", nullable: true),
                    CoverPath = table.Column<string>(type: "TEXT", nullable: true),
                    ReadingStatus = table.Column<string>(type: "TEXT", nullable: false),
                    ProgressPercent = table.Column<int>(type: "INTEGER", nullable: true),
                    ProgressCurrentPage = table.Column<int>(type: "INTEGER", nullable: true),
                    ProgressTotalPages = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LibraryFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastScannedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryFolders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookAuthors",
                columns: table => new
                {
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookAuthors", x => new { x.BookId, x.Order });
                    table.ForeignKey(
                        name: "FK_BookAuthors_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookCopies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CopyKind = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: true),
                    Format = table.Column<string>(type: "TEXT", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    FileHash = table.Column<string>(type: "TEXT", nullable: true),
                    FileLastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TocJson = table.Column<string>(type: "TEXT", nullable: true),
                    LibraryFolderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastScannedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsMissing = table.Column<bool>(type: "INTEGER", nullable: true),
                    ParseFailed = table.Column<bool>(type: "INTEGER", nullable: true),
                    Location = table.Column<string>(type: "TEXT", nullable: true),
                    AcquiredDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookCopies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookCopies_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookTag",
                columns: table => new
                {
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagsId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookTag", x => new { x.BookId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_BookTag_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookTag_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookCopies_BookId",
                table: "BookCopies",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_BookTag_TagsId",
                table: "BookTag",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryFolders_Path",
                table: "LibraryFolders",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookAuthors");

            migrationBuilder.DropTable(
                name: "BookCopies");

            migrationBuilder.DropTable(
                name: "BookTag");

            migrationBuilder.DropTable(
                name: "LibraryFolders");

            migrationBuilder.DropTable(
                name: "Books");

            migrationBuilder.DropTable(
                name: "Tags");
        }
    }
}
