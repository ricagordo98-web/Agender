using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Agender.Infrastructure.Data;

public sealed class WriteTrackingInterceptor : DbCommandInterceptor
{
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Track(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        Track(command, eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Track(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Track(command, eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    private static void Track(DbCommand command, CommandExecutedEventData eventData)
    {
        if (eventData.Context is AgenderDbContext context && IsWrite(command.CommandText))
        {
            context.MarkWritten();
        }
    }

    private static bool IsWrite(string text)
    {
        ReadOnlySpan<char> sql = text.AsSpan().TrimStart();

        return sql.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
            || sql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
            || sql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase);
    }
}
