using System.Globalization;
using AIFramework.Core.Metering;
using Microsoft.Data.Sqlite;

namespace AstroIllustrator.Infrastructure;

/// <summary>Single-host product/environment budget backed by the application's SQLite database.</summary>
public sealed class SqliteAiBudgetLedger : IAiBudgetLedger
{
    private readonly string connectionString;
    private readonly string product;
    private readonly string environment;
    private readonly decimal monthlyAmount;
    private readonly string currency;
    private readonly TimeProvider clock;

    public SqliteAiBudgetLedger(string connectionString, string product, string environment,
        decimal monthlyAmount, string currency, TimeProvider clock)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || monthlyAmount <= 0 ||
            currency is null || currency.Length != 3 || currency.Any(c => c is not (>= 'A' and <= 'Z')))
            throw new ArgumentException("Invalid AI budget configuration.");
        new AiUsageScope(product, environment, "local", "chart-proposal").Validate();
        this.connectionString = connectionString;
        this.product = product;
        this.environment = environment;
        this.monthlyAmount = monthlyAmount;
        this.currency = currency;
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<AiReservation> ReserveAsync(AiReservationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: false);
        using (var duplicate = Command(connection, transaction, "SELECT 1 FROM \"AiUsageEntries\" WHERE \"RequestKey\" = $key"))
        {
            duplicate.Parameters.AddWithValue("$key", request.RequestKey);
            if (await duplicate.ExecuteScalarAsync(cancellationToken) is not null)
                return new("", false, "duplicate_request");
        }
        if (request.Scope.Product != product || request.Scope.Environment != environment || request.Currency != currency)
            return new("", false, "no_budget");
        var now = clock.GetUtcNow();
        string period = now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        decimal used = 0;
        using (var amounts = Command(connection, transaction, """
            SELECT "Amount" FROM "AiUsageEntries"
            WHERE "Product" = $product AND "Environment" = $environment AND "Period" = $period AND "State" <> 'Released'
            """))
        {
            amounts.Parameters.AddWithValue("$product", product);
            amounts.Parameters.AddWithValue("$environment", environment);
            amounts.Parameters.AddWithValue("$period", period);
            using var reader = await amounts.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                used = checked(used + decimal.Parse(reader.GetString(0), CultureInfo.InvariantCulture));
        }
        if (used + request.Amount > monthlyAmount) return new("", false, "budget_exhausted");
        string id = Guid.NewGuid().ToString("N");
        using (var insert = Command(connection, transaction, """
            INSERT INTO "AiUsageEntries" ("ReservationId", "RequestKey", "Product", "Environment", "TenantKey", "Feature",
              "Provider", "Model", "Period", "State", "ReservedAmount", "Amount", "Currency", "PriceVersion", "ReservedAtUtc")
            VALUES ($id, $key, $product, $environment, $tenant, $feature, $provider, $model, $period,
              'Reserved', $amount, $amount, $currency, $version, $now)
            """))
        {
            insert.Parameters.AddWithValue("$id", id);
            insert.Parameters.AddWithValue("$key", request.RequestKey);
            insert.Parameters.AddWithValue("$product", product);
            insert.Parameters.AddWithValue("$environment", environment);
            insert.Parameters.AddWithValue("$tenant", request.Scope.TenantKey);
            insert.Parameters.AddWithValue("$feature", request.Scope.Feature);
            insert.Parameters.AddWithValue("$provider", request.ModelKey.Provider);
            insert.Parameters.AddWithValue("$model", request.ModelKey.Model);
            insert.Parameters.AddWithValue("$period", period);
            insert.Parameters.AddWithValue("$amount", Number(request.Amount));
            insert.Parameters.AddWithValue("$currency", currency);
            insert.Parameters.AddWithValue("$version", request.PriceVersion);
            insert.Parameters.AddWithValue("$now", now.ToString("O", CultureInfo.InvariantCulture));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        transaction.Commit();
        return new(id, true, null);
    }

    public async Task CommitAsync(AiUsageRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: false);
        using var lookup = Command(connection, transaction, """
            SELECT "RequestKey", "Product", "Environment", "TenantKey", "Feature", "Provider", "Model",
              "ReservedAmount", "Currency", "PriceVersion", "State" FROM "AiUsageEntries" WHERE "ReservationId" = $id
            """);
        lookup.Parameters.AddWithValue("$id", record.ReservationId);
        using (var reader = await lookup.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("Unknown reservation.", nameof(record));
            if (reader.GetString(10) != "Reserved") return;
            if (record.EstimatedCost < 0 || record.RequestKey != reader.GetString(0) ||
                record.Scope != new AiUsageScope(reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)) ||
                record.ModelKey != new AiModelKey(reader.GetString(5), reader.GetString(6)) ||
                record.ReservedAmount != decimal.Parse(reader.GetString(7), CultureInfo.InvariantCulture) ||
                record.Currency != reader.GetString(8) || record.PriceVersion != reader.GetString(9))
                throw new ArgumentException("Usage record does not match the reservation.", nameof(record));
        }
        using var update = Command(connection, transaction, """
            UPDATE "AiUsageEntries" SET "State" = 'Committed', "Amount" = $amount, "ResponseModel" = $response,
              "Outcome" = $outcome, "InputTokens" = $input, "OutputTokens" = $output,
              "UsageReported" = $reported, "StartedAtUtc" = $started, "CompletedAtUtc" = $completed
            WHERE "ReservationId" = $id
            """);
        update.Parameters.AddWithValue("$id", record.ReservationId);
        update.Parameters.AddWithValue("$amount", Number(record.EstimatedCost));
        update.Parameters.AddWithValue("$response", (object?)record.ResponseModel ?? DBNull.Value);
        update.Parameters.AddWithValue("$outcome", record.Outcome.ToString());
        update.Parameters.AddWithValue("$input", (object?)record.InputTokens ?? DBNull.Value);
        update.Parameters.AddWithValue("$output", (object?)record.OutputTokens ?? DBNull.Value);
        update.Parameters.AddWithValue("$reported", record.UsageReported ? 1 : 0);
        update.Parameters.AddWithValue("$started", record.StartedAt.ToString("O", CultureInfo.InvariantCulture));
        update.Parameters.AddWithValue("$completed", record.CompletedAt.ToString("O", CultureInfo.InvariantCulture));
        await update.ExecuteNonQueryAsync(cancellationToken);
        transaction.Commit();
    }

    public async Task ReleaseAsync(string reservationId, AiOutcome reason, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: false);
        using var lookup = Command(connection, transaction, "SELECT \"State\" FROM \"AiUsageEntries\" WHERE \"ReservationId\" = $id");
        lookup.Parameters.AddWithValue("$id", reservationId);
        var state = (string?)await lookup.ExecuteScalarAsync(cancellationToken);
        if (state is null) throw new ArgumentException("Unknown reservation.", nameof(reservationId));
        if (state != "Reserved") return;
        using var update = Command(connection, transaction, """
            UPDATE "AiUsageEntries" SET "State" = 'Released', "Amount" = '0', "Outcome" = $outcome,
              "CompletedAtUtc" = $completed WHERE "ReservationId" = $id
            """);
        update.Parameters.AddWithValue("$id", reservationId);
        update.Parameters.AddWithValue("$outcome", reason.ToString());
        update.Parameters.AddWithValue("$completed", clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
        await update.ExecuteNonQueryAsync(cancellationToken);
        transaction.Commit();
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString) { DefaultTimeout = 30 };
        try { await connection.OpenAsync(cancellationToken); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction, string sql)
        => new(sql, connection, transaction);
    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
