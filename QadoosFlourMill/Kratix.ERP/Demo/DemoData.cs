using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Kratix.ERP.Data;
using Kratix.ERP.Models;

namespace Kratix.ERP.Demo;

// The public demo (Demo:Enabled): visitors sign in with one click as a role, and the data is put back to Demo/seed.sql
// once a day, with every date moved forward so the dashboard always shows recent activity.
public static class DemoData
{
    public static readonly string[] Accounts = ["owner", "admin", "operations", "sales", "worker"];
    private static readonly TimeSpan ResetEvery = TimeSpan.FromHours(24);

    public static bool Enabled(IConfiguration config) => config.GetValue<bool>("Demo:Enabled");

    // Resets when the data was never loaded or the last reset is a day old.
    public static async Task ResetIfDueAsync(AppDbContext db, CancellationToken cancel = default)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancel);
        try
        {
            await Execute(connection, null, """CREATE TABLE IF NOT EXISTS "DemoState" ("Id" int PRIMARY KEY, "LastReset" timestamp NOT NULL)""", cancel);
            await using var check = connection.CreateCommand();
            check.CommandText = """SELECT "LastReset" FROM "DemoState" WHERE "Id" = 1""";
            var last = await check.ExecuteScalarAsync(cancel) as DateTime?;
            if (last is null || DateTime.Now - last.Value >= ResetEvery)
                await ResetAsync(connection, cancel);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task ResetAsync(DbConnection connection, CancellationToken cancel)
    {
        var seed = await ReadSeedAsync();
        var anchor = DateTime.Parse(seed[(seed.IndexOf(':') + 1)..seed.IndexOf('\n')].Trim());
        var days = (DateTime.Today - anchor.Date).Days;
        // Nobody can sign in with a password; the demo uses one-click sign-in instead.
        var hash = new PasswordHasher<User>().HashPassword(new User(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)));

        await using var transaction = await connection.BeginTransactionAsync(cancel);
        // The same lock as BusinessWriteFilter, so no write is half-way through while the data is replaced.
        await Execute(connection, transaction, """
            SET LOCAL lock_timeout = '60s';
            SELECT pg_advisory_xact_lock(hashtext('Kratix.BusinessWrite'));
            DO $$ BEGIN
              EXECUTE (SELECT 'TRUNCATE ' || string_agg(format('%I', tablename), ', ') || ' RESTART IDENTITY CASCADE'
                       FROM pg_tables WHERE schemaname = 'public' AND tablename NOT IN ('__EFMigrationsHistory', 'DemoState'));
            END $$;
            """, cancel);
        await Execute(connection, transaction, seed, cancel);
        await Execute(connection, transaction, $$"""
            DO $$
            DECLARE c record;
            BEGIN
              FOR c IN SELECT table_name, column_name FROM information_schema.columns
                       WHERE table_schema = 'public' AND data_type = 'timestamp without time zone'
                         AND table_name NOT IN ('__EFMigrationsHistory', 'DemoState')
              LOOP
                EXECUTE format('UPDATE %I SET %I = %I + interval ''{{days}} days''', c.table_name, c.column_name, c.column_name);
              END LOOP;
            END $$;
            UPDATE "Users" SET "PasswordHash" = '{{hash}}';
            INSERT INTO "DemoState" ("Id", "LastReset") VALUES (1, '{{DateTime.Now:yyyy-MM-dd HH:mm:ss}}')
              ON CONFLICT ("Id") DO UPDATE SET "LastReset" = EXCLUDED."LastReset";
            """, cancel);
        await transaction.CommitAsync(cancel);
        Console.WriteLine($"Demo data reset (dates moved {days} days forward).");
    }

    private static async Task<string> ReadSeedAsync()
    {
        await using var stream = typeof(DemoData).Assembly.GetManifestResourceStream("Kratix.ERP.Demo.seed.sql")
            ?? throw new InvalidOperationException("Demo/seed.sql is not embedded in the build.");
        using var reader = new StreamReader(stream);
        return (await reader.ReadToEndAsync()).Replace("\r\n", "\n");
    }

    // Plain commands, not ExecuteSqlRaw: the seed is data and must not be read as a format string.
    private static async Task Execute(DbConnection connection, DbTransaction? transaction, string sql, CancellationToken cancel)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = 300;
        await command.ExecuteNonQueryAsync(cancel);
    }
}

// Checks every hour; free hosting sleeps when idle, so the check at startup matters as much.
public sealed class DemoResetService(IServiceScopeFactory scopes, ILogger<DemoResetService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await DemoData.ResetIfDueAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Demo data reset failed; trying again in an hour.");
            }
        }
    }
}
