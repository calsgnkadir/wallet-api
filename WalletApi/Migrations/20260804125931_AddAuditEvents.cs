using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WalletApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Details = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAt",
                table: "AuditEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_UserId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "UserId", "OccurredAt" });

            // Denetim kaydını veritabanı seviyesinde de korur. Uygulama katmanındaki
            // kontrol yalnızca bu uygulamayı bağlar; buradaki tetikleyici, veritabanına
            // doğrudan bağlanan bir yönetici veya sızan bir hesap için de geçerlidir.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_audit_event_change()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'Denetim kayitlari degistirilemez veya silinemez.';
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER audit_events_immutable
                BEFORE UPDATE OR DELETE ON "AuditEvents"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_event_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS audit_events_immutable ON "AuditEvents";""");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_audit_event_change();");

            migrationBuilder.DropTable(
                name: "AuditEvents");
        }
    }
}
