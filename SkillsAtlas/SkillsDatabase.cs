using System.Reflection;
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
    }

    public void Save(IReadOnlyCollection<SkillEntry> skills)
    {
        Execute("BEGIN TRANSACTION;");
        try
        {
            foreach (var skill in skills)
            {
                Execute($"""
                    INSERT OR IGNORE INTO Skills
                        (RepositoryName, RepositoryUrl, SkillName, ShortDescription, RelativeFilePath, CommitHash, Content, FileUrl)
                    VALUES
                        ({Quote(skill.RepositoryName)}, {Quote(skill.RepositoryUrl)}, {Quote(skill.Name)},
                         {Quote(skill.ShortDescription)}, {Quote(skill.RelativeFilePath)}, {Quote(skill.CommitHash)},
                         {Quote(skill.Content)}, {Quote(skill.FileUrl)});
                    """);
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
        var result = Native.sqlite3_prepare_v2(_database, query, -1, out var statement, IntPtr.Zero);
        Check(result);

        try
        {
            BindText(statement, 1, repositoryUrl);
            BindText(statement, 2, commitHash);
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

    private static string Quote(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

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

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_text")]
        internal static extern IntPtr sqlite3_column_text(IntPtr statement, int column);

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_finalize")]
        internal static extern int sqlite3_finalize(IntPtr statement);

        [DllImport("sqlite3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_errmsg")]
        internal static extern IntPtr sqlite3_errmsg(IntPtr database);
    }
}
