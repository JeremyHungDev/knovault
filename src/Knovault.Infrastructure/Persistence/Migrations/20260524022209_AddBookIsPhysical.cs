using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Knovault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookIsPhysical : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPhysical",
                table: "Books",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // 保留既有資料：有實體版本的書 → 標記為實體
            migrationBuilder.Sql(
                "UPDATE \"Books\" SET \"IsPhysical\" = 1 " +
                "WHERE \"Id\" IN (SELECT \"BookId\" FROM \"BookCopies\" WHERE \"CopyKind\" = 'Physical');");

            // 實體已改為 Book 旗標，移除實體版本資料列
            migrationBuilder.Sql("DELETE FROM \"BookCopies\" WHERE \"CopyKind\" = 'Physical';");

            migrationBuilder.DropColumn(
                name: "AcquiredDate",
                table: "BookCopies");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "BookCopies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPhysical",
                table: "Books");

            migrationBuilder.AddColumn<DateOnly>(
                name: "AcquiredDate",
                table: "BookCopies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "BookCopies",
                type: "TEXT",
                nullable: true);
        }
    }
}
