using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knovault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookPhysicalLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhysicalLocation",
                table: "Books",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalNotes",
                table: "Books",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhysicalLocation",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "PhysicalNotes",
                table: "Books");
        }
    }
}
