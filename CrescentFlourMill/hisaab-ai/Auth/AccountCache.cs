using System.Collections.Concurrent;

namespace Hisaab.ERP.Auth;

// Every API request checks that its token's account is still active with the same role and password. That was one
// database round trip per request; it is now remembered for a few seconds. Account changes go through
// UsersController and change-password, which forget the account at once, so "deactivating signs them out at once"
// still holds on this server. (With several API instances another instance could lag by up to Ttl.)
public static class AccountCache
{
    public sealed record Account(bool IsActive, string Role, string PasswordStamp);

    private sealed record Entry(Account? Account, DateTime At);

    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private static readonly ConcurrentDictionary<int, Entry> Entries = new();

    public static async Task<Account?> GetAsync(int userId, Func<Task<Account?>> load)
    {
        if (Entries.TryGetValue(userId, out var entry) && DateTime.UtcNow - entry.At < Ttl)
            return entry.Account;
        var account = await load();
        Entries[userId] = new Entry(account, DateTime.UtcNow);
        return account;
    }

    public static void Forget(int userId) => Entries.TryRemove(userId, out _);

    public static void Clear() => Entries.Clear();
}
