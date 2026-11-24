using System.Runtime.InteropServices;

internal sealed class LabDatabase : IDisposable
{
    delegate int RowCallback(IntPtr argument, int columns, IntPtr values, IntPtr names);

    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_close(IntPtr db);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_exec(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, RowCallback callback, IntPtr argument, out IntPtr error);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern void sqlite3_free(IntPtr value);

    readonly IntPtr db;
    readonly object gate = new();

    internal LabDatabase(string path)
    {
        if (sqlite3_open(path, out db) != 0)
        {
            sqlite3_close(db);
            throw new IOException($"Cannot open the results database at {path}");
        }
        Execute("""
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=FULL;
            PRAGMA busy_timeout=5000;
            CREATE TABLE IF NOT EXISTS campaign (
                id INTEGER PRIMARY KEY, created TEXT NOT NULL, plan TEXT NOT NULL, signature TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS trial (
                campaign INTEGER NOT NULL, engine TEXT NOT NULL, task INTEGER NOT NULL, seed INTEGER NOT NULL,
                mode TEXT NOT NULL, shooter TEXT NOT NULL, target TEXT NOT NULL, distance INTEGER NOT NULL,
                terrain INTEGER NOT NULL, health INTEGER NOT NULL, lost INTEGER NOT NULL, max_hp INTEGER NOT NULL,
                shooter_lost INTEGER NOT NULL, radius INTEGER NOT NULL, shots INTEGER NOT NULL, hits INTEGER NOT NULL,
                dodged INTEGER NOT NULL, outcome INTEGER NOT NULL, seconds REAL NOT NULL, travel REAL NOT NULL,
                PRIMARY KEY (campaign, engine, task)
            );
            CREATE INDEX IF NOT EXISTS trial_results ON trial (campaign, mode, terrain, health, distance);
            """);
    }

    internal static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

    internal List<Dictionary<string, string>> Query(string sql)
    {
        var result = new List<Dictionary<string, string>>();
        lock (gate)
            Execute(sql, (_, columns, values, names) =>
            {
                var row = new Dictionary<string, string>();
                for (int i = 0; i < columns; i++)
                {
                    int offset = i * IntPtr.Size;
                    row[Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(names, offset))] = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(values, offset));
                }
                result.Add(row);
                return 0;
            });
        return result;
    }

    internal void Transaction(string statements)
    {
        lock (gate)
        {
            Execute("BEGIN IMMEDIATE");
            try { Execute(statements); Execute("COMMIT"); }
            catch { Execute("ROLLBACK"); throw; }
        }
    }

    void Execute(string sql, RowCallback callback = null)
    {
        if (sqlite3_exec(db, sql, callback, IntPtr.Zero, out var error) == 0) return;
        string message = Marshal.PtrToStringUTF8(error);
        sqlite3_free(error);
        throw new IOException(message ?? "SQLite command failed");
    }

    public void Dispose() => sqlite3_close(db);
}
