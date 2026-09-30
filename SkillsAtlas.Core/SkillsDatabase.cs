using System.Runtime.InteropServices;

namespace SkillsAtlas;

internal sealed class SkillsDatabase : IDisposable
{
    private const int SqliteOk = 0;
    private const int SqliteRow = 100;
    private const int SqliteDone = 101;
    private const int OpenReadWrite = 0x00000002;
    private const int OpenCreate = 0x00000004;
    private IntPtr _database;

    static SkillsDatabase()
    {
        NativeLibrary.SetDllImportResolver(typeof(SkillsDatabase).Assembly, (libraryName, assembly, searchPath) =>
        {
            if (libraryName != "sqlite3")
                return IntPtr.Zero;

            var candidates = OperatingSystem.IsWindows()
                ? new[] { "winsqlite3.dll", "sqlite3.dll" }
                : OperatingSystem.IsMacOS()
                    ? new[] { "/usr/lib/libsqlite3.dylib", "libsqlite3.dylib" }
                    : new[] { "libsqlite3.so.0", "libsqlite3.so" };

            foreach (var candidate in candidates)
            {
                if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
                    return handle;
            }

            return IntPtr.Zero;
        });
    }

    public SkillsDatabase(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var result = Native.sqlite3_open_v2(databasePath, out _database, OpenReadWrite | OpenCreate, IntPtr.Zero);
        if (result != SqliteOk)
            throw new InvalidOperationException($"Could not open SQLite database '{databasePath}'. SQLite error code: {result}.");

        Execute("""
            CREATE TABLE IF NOT EXISTS Skills (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RepositoryName TEXT NOT NULL,
                RepositoryUrl TEXT NOT NULL,
                SkillName TEXT NOT NULL,
                ShortDescription TEXT NOT NULL,
                RelativeFilePath TEXT NOT NULL,
                CommitHash TEXT NOT NULL,
                Content TEXT NOT NULL,
                FileUrl TEXT NOT NULL,
                UNIQUE (RepositoryUrl, SkillName, CommitHash)
            );
            CREATE INDEX IF NOT EXISTS IX_Skills_RepositoryCommit
                ON Skills (RepositoryUrl, CommitHash);
            """);

        // Keep one canonical row for each skill definition before enforcing the
        // same rule for future inserts. The oldest row retains its database order.
        Execute("""
            DELETE FROM Skills
            WHERE Id NOT IN (
                SELECT MIN(Id)
                FROM Skills
                GROUP BY SkillName, Content
            );
            CREATE UNIQUE INDEX IF NOT EXISTS UX_Skills_Name_Content
                ON Skills (SkillName, Content);
            """);
    }

    public void Save(IReadOnlyCollection<SkillEntry> skills)
    {
        const string insert = """
            INSERT OR IGNORE INTO Skills
                (RepositoryName, RepositoryUrl, SkillName, ShortDescription, RelativeFilePath, CommitHash, Content, FileUrl)
            VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8);
            """;

        Execute("BEGIN TRANSACTION;");
        try
        {
            var result = Native.sqlite3_prepare_v2(_database, insert, -1, out var statement, IntPtr.Zero);
            Check(result);

            try
            {
                foreach (var skill in skills)
                {
                    BindText(statement, 1, skill.RepositoryName);
                    BindText(statement, 2, skill.RepositoryUrl);
                    BindText(statement, 3, skill.Name);
                    BindText(statement, 4, skill.ShortDescription);
                    BindText(statement, 5, skill.RelativeFilePath);
                    BindText(statement, 6, skill.CommitHash);
                    BindText(statement, 7, skill.Content);
                    BindText(statement, 8, skill.FileUrl);

                    result = Native.sqlite3_step(statement);
                    if (result != SqliteDone)
                        Check(result);

                    Check(Native.sqlite3_reset(statement));
                    Check(Native.sqlite3_clear_bindings(statement));
                }
            }
            finally
            {
                Native.sqlite3_finalize(statement);
            }

            Execute("COMMIT;");
        }
        catch
        {
            Execute("ROLLBACK;");
            throw;
        }
    }

    public List<SkillEntry> GetSkills(string repositoryUrl, string commitHash)
    {
        const string query = """
            SELECT RepositoryName, RepositoryUrl, SkillName, ShortDescription, RelativeFilePath, CommitHash, Content, FileUrl
            FROM Skills
            WHERE RepositoryUrl = ?1 AND CommitHash = ?2
            ORDER BY SkillName COLLATE NOCASE;
            """;
        return ReadSkills(query, statement =>
        {
            BindText(statement, 1, repositoryUrl);
            BindText(statement, 2, commitHash);
        });
    }

    public List<SkillEntry> GetAllSkills()
    {
        const string query = """
            SELECT RepositoryName, RepositoryUrl, SkillName, ShortDescription, RelativeFilePath, CommitHash, Content, FileUrl
            FROM Skills
            ORDER BY Id ASC;
            """;
        return ReadSkills(query);
    }

    private List<SkillEntry> ReadSkills(string query, Action<IntPtr>? bind = null)
    {
        var result = Native.sqlite3_prepare_v2(_database, query, -1, out var statement, IntPtr.Zero);
        Check(result);
        try
        {
            bind?.Invoke(statement);
            var skills = new List<SkillEntry>();
            while (true)
            {
                result = Native.sqlite3_step(statement);
                if (result == SqliteDone)
                    return skills;
                if (result != SqliteRow)
                    Check(result);

                skills.Add(new SkillEntry(
                    ColumnText(statement, 0), ColumnText(statement, 1), ColumnText(statement, 2), ColumnText(statement, 3),
                    ColumnText(statement, 4), ColumnText(statement, 5), ColumnText(statement, 6), ColumnText(statement, 7)));
            }
        }
        finally
        {
            Native.sqlite3_finalize(statement);
        }
    }

    private void Execute(string sql)
    {
        var result = Native.sqlite3_exec(_database, sql, IntPtr.Zero, IntPtr.Zero, out var errorPointer);
        if (result == SqliteOk)
            return;

        var error = errorPointer == IntPtr.Zero
            ? GetErrorMessage()
            : Marshal.PtrToStringUTF8(errorPointer) ?? GetErrorMessage();
        if (errorPointer != IntPtr.Zero)
            Native.sqlite3_free(errorPointer);
        throw new InvalidOperationException($"SQLite error: {error}");
    }

    private void BindText(IntPtr statement, int index, string value)
    {
        var result = Native.sqlite3_bind_text(statement, index, value, -1, new IntPtr(-1));
        Check(result);
    }

    private string ColumnText(IntPtr statement, int index)
    {
        var pointer = Native.sqlite3_column_text(statement, index);
        return pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
    }

    private string GetErrorMessage() => Marshal.PtrToStringUTF8(Native.sqlite3_errmsg(_database)) ?? "Unknown SQLite error";

    private void Check(int result)
    {
        if (result != SqliteOk)
            throw new InvalidOperationException($"SQLite error: {GetErrorMessage()} (code {result}).");
    }

    public void Dispose()
    {
        if (_database == IntPtr.Zero)
            return;
        Native.sqlite3_close_v2(_database);
        _database = IntPtr.Zero;
    }

    private static class Native
    {
        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_open_v2")]
        internal static extern int sqlite3_open_v2(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string filename, out IntPtr database, int flags, IntPtr vfs);

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_close_v2")]
        internal static extern int sqlite3_close_v2(IntPtr database);

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_exec")]
        internal static extern int sqlite3_exec(
            IntPtr database, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql,
            IntPtr callback, IntPtr callbackArgument, out IntPtr errorMessage);

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_free")]
        internal static extern void sqlite3_free(IntPtr pointer);

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_prepare_v2")]
        internal static extern int sqlite3_prepare_v2(
            IntPtr database, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int byteCount,
            out IntPtr statement, IntPtr tail);

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_bind_text")]
        internal static extern int sqlite3_bind_text(
            IntPtr statement, int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string value,
            int byteCount, IntPtr destructor);

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_step")]
        internal static extern int sqlite3_step(IntPtr statement);

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_reset")]
        internal static extern int sqlite3_reset(IntPtr statement);

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_clear_bindings")]
        internal static extern int sqlite3_clear_bindings(IntPtr statement);

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_text")]
        internal static extern IntPtr sqlite3_column_text(IntPtr statement, int column);

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_finalize")]
        internal static extern int sqlite3_finalize(IntPtr statement);

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_errmsg")]
        internal static extern IntPtr sqlite3_errmsg(IntPtr database);
    }
}
