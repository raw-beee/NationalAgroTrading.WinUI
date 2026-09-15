using System.Data.SQLite;
using NationalAgroTrading.WinUI.Data;
using NationalAgroTrading.WinUI.Services;

// Dry-run of EnsureBillFiscalYearSupport() against a COPY of the
// production database. The real DatabaseHelper/AppConfig source files
// are compiled into this console app, so this exercises the exact
// migration that will run at app startup.

string liveDb = @"D:\National Software\natdatabase.db";
string workDir = Path.Combine(Path.GetTempPath(), "fy_migration_dryrun");
string testDb = Path.Combine(workDir, "dryrun.db");

Directory.CreateDirectory(workDir);
File.Copy(liveDb, testDb, true);

// ProgramData config takes precedence over the exe-adjacent one and
// points at the live DB; move it aside so AppConfig resolves to the
// copy, restoring it no matter how this ends.
string pdDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "NationalAgroTrading");
string pdConfig = Path.Combine(pdDir, "config.json");
string pdBackup = pdConfig + ".dryrun-bak";
bool movedProgramDataConfig = false;

try
{
    if (File.Exists(pdConfig))
    {
        File.Move(pdConfig, pdBackup);
        movedProgramDataConfig = true;
    }

    File.WriteAllText(
        Path.Combine(AppContext.BaseDirectory, "config.json"),
        System.Text.Json.JsonSerializer.Serialize(
            new AppConfig.ConfigFile { DatabasePath = testDb }));

    if (AppConfig.DatabasePath != testDb)
    {
        Console.WriteLine("AppConfig did not resolve to the test DB: "
            + AppConfig.DatabasePath);
        return;
    }

    Console.WriteLine("Target DB: " + AppConfig.DatabasePath);

    void Run(string title, string sql)
    {
        Console.WriteLine("=== " + title + " ===");
        using var conn = new SQLiteConnection(
            "Data Source=" + AppConfig.DatabasePath + ";Version=3;Read Only=True;");
        conn.Open();
        using var cmd = new SQLiteCommand(sql, conn);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var vals = new string[r.FieldCount];
            for (int i = 0; i < r.FieldCount; i++)
                vals[i] = r.IsDBNull(i) ? "NULL" : r.GetValue(i).ToString();
            Console.WriteLine(string.Join(" | ", vals));
        }
        Console.WriteLine();
    }

    Run("BEFORE: total rows / NULL FiscalYear (Sales, Purchase)", @"
SELECT 'Sales', COUNT(*), 'n/a' FROM Sales
UNION ALL SELECT 'Purchase', COUNT(*), 'n/a' FROM Purchase");

    Console.WriteLine("=== Running EnsureBillFiscalYearSupport() ===");
    DatabaseHelper.EnsureBillFiscalYearSupport();
    Console.WriteLine("Migration completed without error.");
    Console.WriteLine();

    Run("AFTER: FiscalYear coverage", @"
SELECT 'Sales', COUNT(*), SUM(CASE WHEN FiscalYear IS NULL THEN 1 ELSE 0 END) FROM Sales
UNION ALL
SELECT 'Purchase', COUNT(*), SUM(CASE WHEN FiscalYear IS NULL THEN 1 ELSE 0 END) FROM Purchase");

    Run("FiscalYear distribution (Sales)", @"
SELECT FiscalYear, COUNT(*) FROM Sales GROUP BY 1");

    Run("FiscalYear distribution (Purchase)", @"
SELECT FiscalYear, COUNT(*) FROM Purchase GROUP BY 1");

    Run("Created indexes", @"
SELECT name FROM sqlite_master
WHERE type = 'index'
  AND name IN ('idx_sales_bill_fy', 'idx_purchase_company_bill_fy')");

    // ---- Enforcement test on the copy ----
    Console.WriteLine("=== Enforcement test ===");
    using (var conn = new SQLiteConnection(
        "Data Source=" + AppConfig.DatabasePath + ";Version=3;"))
    {
        conn.Open();
        using (var c = conn.CreateCommand())
        {
            c.CommandText =
                "INSERT INTO Sales (ProductID, CompanyID, Quantity, Price, " +
                "BillNumber, NepaliDate, FiscalYear, TotalSalesAmount) VALUES " +
                "(1, 1, 1, 10, 'Bill DRYRUN-1', '2083-05-01', '2083/2084', 10)";
            c.ExecuteNonQuery();
            Console.WriteLine("1. Unique bill inserted: OK");
        }

        try
        {
            using (var c = conn.CreateCommand())
            {
                c.CommandText =
                    "INSERT INTO Sales (ProductID, CompanyID, Quantity, Price, " +
                    "BillNumber, NepaliDate, FiscalYear, TotalSalesAmount) VALUES " +
                    "(1, 1, 1, 10, 'Bill DRYRUN-1', '2083-06-01', '2083/2084', 10)";
                c.ExecuteNonQuery();
            }
            Console.WriteLine("2. DUPLICATE ACCEPTED - BAD");
        }
        catch (SQLiteException)
        {
            Console.WriteLine("2. Duplicate in same FY correctly REJECTED");
        }

        try
        {
            using (var c = conn.CreateCommand())
            {
                c.CommandText =
                    "INSERT INTO Sales (ProductID, CompanyID, Quantity, Price, " +
                    "BillNumber, NepaliDate, FiscalYear, TotalSalesAmount) VALUES " +
                    "(1, 1, 1, 10, 'Bill DRYRUN-1', '2082-05-01', '2082/2083', 10)";
                c.ExecuteNonQuery();
            }
            Console.WriteLine("3. Same number in different FY correctly ALLOWED");
        }
        catch (SQLiteException)
        {
            Console.WriteLine("3. Different-FY insert REJECTED - BAD");
        }

        using (var c = conn.CreateCommand())
        {
            c.CommandText =
                "DELETE FROM Sales WHERE BillNumber = 'Bill DRYRUN-1'";
            Console.WriteLine("4. Cleanup: " + c.ExecuteNonQuery() + " test rows removed");
        }
    }

    Console.WriteLine("=== dry-run complete ===");
}
finally
{
    if (movedProgramDataConfig)
    {
        File.Move(pdBackup, pdConfig);
        Console.WriteLine("(ProgramData config restored)");
    }
}
