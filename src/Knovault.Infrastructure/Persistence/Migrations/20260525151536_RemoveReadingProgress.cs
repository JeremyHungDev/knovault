using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knovault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReadingProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reading / Finished 不再存在，升遷為 WantToRead
            migrationBuilder.Sql(
                "UPDATE Books SET ReadingStatus = 'WantToRead' WHERE ReadingStatus IN ('Reading', 'Finished')");

            migrationBuilder.DropColumn(
                name: "ProgressCurrentPage",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "ProgressTotalPages",
                table: "Books");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgressCurrentPage",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPercent",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressTotalPages",
                table: "Books",
                type: "INTEGER",
                nullable: true);
        }
    }
}
