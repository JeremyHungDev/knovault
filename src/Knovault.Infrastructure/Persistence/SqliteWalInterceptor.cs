using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Knovault.Infrastructure.Persistence;

/// <summary>每次連線開啟時套用 WAL 與 busy_timeout（見 spec §3 SQLite 並發設定）。</summary>
public sealed class SqliteWalInterceptor : DbConnectionInterceptor
{
    private const string Pragmas = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=30000;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = Pragmas;
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Pragmas;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
