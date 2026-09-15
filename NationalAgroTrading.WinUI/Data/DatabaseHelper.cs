using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using NationalAgroTrading.WinUI.Services;

namespace NationalAgroTrading.WinUI.Data
{
	public static class DatabaseHelper
	{
		public static SQLiteConnection GetConnection()
		{
			if (!File.Exists(AppConfig.DatabasePath))
			{
				throw new InvalidOperationException(
					"Database not found at: " + AppConfig.DatabasePath +
					"\nCheck the DatabasePath setting in " +
					(AppConfig.ConfigFilePath ?? "config.json") +
					" or restore a backup. " +
					"No new empty database will be created.");
			}

			return new SQLiteConnection(
				"Data Source=" + AppConfig.DatabasePath + ";Version=3;");
		}

		// One-time schema migration: the Ledger table originally had no
		// link to the Payment that created each row, which forced payment
		// deletion to match ledger rows by text and could delete unrelated
		// history. Adds the column if missing and backfills what can be
		// matched exactly; rows that cannot be matched keep NULL and are
		// never touched by payment deletion.
		public static void EnsureLedgerPaymentIdColumn()
		{
			using var conn = GetConnection();
			conn.Open();

			bool hasColumn = false;

			using (var info =
				new SQLiteCommand("PRAGMA table_info(Ledger);", conn))
			using (var reader = info.ExecuteReader())
			{
				while (reader.Read())
				{
					if (string.Equals(
							reader["name"]?.ToString(),
							"PaymentID",
							StringComparison.OrdinalIgnoreCase))
					{
						hasColumn = true;
						break;
					}
				}
			}

			if (!hasColumn)
			{
				using var alter = new SQLiteCommand(
					"ALTER TABLE Ledger ADD COLUMN PaymentID INTEGER;",
					conn);

				alter.ExecuteNonQuery();
			}

			const string backfillQuery = @"
                UPDATE Ledger
                SET PaymentID =
                    (
                        SELECT p.PaymentID
                        FROM Payment p
                        WHERE p.CompanyID = Ledger.CompanyID
                          AND p.Amount = (Ledger.Debit + Ledger.Credit)
                          AND p.PaymentDate = Ledger.Date
                          AND 'Payment ' || p.PaymentType ||
                              ' via ' || p.PaymentMethod ||
                              ', Receipt: ' || p.Receipt =
                              Ledger.Particular
                    )
                WHERE PaymentID IS NULL
                  AND Particular LIKE 'Payment % via %, Receipt: %'";

			using (var backfill =
				new SQLiteCommand(backfillQuery, conn))
			{
				backfill.ExecuteNonQuery();
			}
		}

		public static DataTable GetData(
			string query,
			Dictionary<string, object>? parameters = null)
		{
			DataTable dt = new DataTable();

			using (var conn = GetConnection())
			using (var cmd = new SQLiteCommand(query, conn))
			{
				if (parameters != null)
				{
					foreach (var param in parameters)
					{
						cmd.Parameters.AddWithValue(
							param.Key,
							param.Value ?? DBNull.Value);
					}
				}

				using (var adapter = new SQLiteDataAdapter(cmd))
				{
					adapter.Fill(dt);
				}
			}

			return dt;
		}

		public static SQLiteDataReader ExecuteReader(
			string query,
			Dictionary<string, object>? parameters = null)
		{
			var conn = GetConnection();
			conn.Open();

			var cmd = new SQLiteCommand(query, conn);

			if (parameters != null)
			{
				foreach (var param in parameters)
				{
					cmd.Parameters.AddWithValue(
						param.Key,
						param.Value ?? DBNull.Value);
				}
			}

			return cmd.ExecuteReader(
				CommandBehavior.CloseConnection);
		}

		public static void ExecuteQuery(
			string query,
			Dictionary<string, object>? parameters = null)
		{
			using var conn = GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(query, conn);

			AddParameters(cmd, parameters);

			cmd.ExecuteNonQuery();
		}

		public static void ExecuteNonQuery(
			string query,
			Dictionary<string, object>? parameters = null)
		{
			using var conn = GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(query, conn);

			AddParameters(cmd, parameters);

			cmd.ExecuteNonQuery();
		}

		public static int ExecuteNonQuery(
			string query,
			params SQLiteParameter[] parameters)
		{
			using var conn = GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(query, conn);

			if (parameters != null && parameters.Length > 0)
			{
				cmd.Parameters.AddRange(parameters);
			}

			return cmd.ExecuteNonQuery();
		}

		public static object? ExecuteScalar(
			string query,
			Dictionary<string, object>? parameters = null)
		{
			using var conn = GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(query, conn);

			AddParameters(cmd, parameters);

			return cmd.ExecuteScalar();
		}

		// Nepali fiscal year (Shrawan start) of a stored BS date
		// 'yyyy-MM-dd': month >= 4 belongs to year/year+1, otherwise
		// year-1/year. Returns null for anything unparseable so that
		// legacy rows stay exempt from bill-number uniqueness.
		public static string? FiscalYearOf(string? nepaliDate)
		{
			if (nepaliDate == null ||
				nepaliDate.Length != 10 ||
				nepaliDate[4] != '-' ||
				nepaliDate[7] != '-')
			{
				return null;
			}

			if (!int.TryParse(
					nepaliDate.Substring(0, 4),
					out int year) ||
				!int.TryParse(
					nepaliDate.Substring(5, 2),
					out int month))
			{
				return null;
			}

			return month >= 4
				? $"{year}/{year + 1}"
				: $"{year - 1}/{year}";
		}

		// One-time schema migration for per-fiscal-year bill numbers:
		// 1. adds a FiscalYear column to Sales and Purchase,
		// 2. backfills it from well-formed NepaliDate values,
		// 3. creates unique indexes on (fiscal year, bill number) —
		//    per company for purchases.
		//
		// Rows already involved in duplicate bill numbers (legacy test
		// data) are excluded from the index, so the migration never
		// fails on old data and never rewrites it; every row saved
		// after this migration IS enforced. Once the legacy duplicates
		// are cleaned up the index can be recreated without the
		// exclusion for full coverage.
		public static void EnsureBillFiscalYearSupport()
		{
			using var conn = GetConnection();
			conn.Open();

			EnsureFiscalYearColumn(conn, "Sales");
			EnsureFiscalYearColumn(conn, "Purchase");

			const string salesBackfill = @"
                UPDATE Sales
                SET FiscalYear =
                    CASE
                        WHEN CAST(substr(NepaliDate, 6, 2) AS INTEGER) >= 4
                            THEN substr(NepaliDate, 1, 4) || '/' ||
                                 CAST(CAST(substr(NepaliDate, 1, 4) AS INTEGER) + 1 AS TEXT)
                        ELSE CAST(CAST(substr(NepaliDate, 1, 4) AS INTEGER) - 1 AS TEXT) ||
                                 '/' || substr(NepaliDate, 1, 4)
                    END
                WHERE NepaliDate GLOB '20[0-9][0-9]-[0-9][0-9]-[0-9][0-9]'
                  AND FiscalYear IS NULL";

			const string purchaseBackfill = @"
                UPDATE Purchase
                SET FiscalYear =
                    CASE
                        WHEN CAST(substr(NepaliDate, 6, 2) AS INTEGER) >= 4
                            THEN substr(NepaliDate, 1, 4) || '/' ||
                                 CAST(CAST(substr(NepaliDate, 1, 4) AS INTEGER) + 1 AS TEXT)
                        ELSE CAST(CAST(substr(NepaliDate, 1, 4) AS INTEGER) - 1 AS TEXT) ||
                                 '/' || substr(NepaliDate, 1, 4)
                    END
                WHERE NepaliDate GLOB '20[0-9][0-9]-[0-9][0-9]-[0-9][0-9]'
                  AND FiscalYear IS NULL";

			using (var cmd = new SQLiteCommand(salesBackfill, conn))
				cmd.ExecuteNonQuery();

			using (var cmd = new SQLiteCommand(purchaseBackfill, conn))
				cmd.ExecuteNonQuery();

			string salesExclusions = DuplicateRowIdList(
				conn,
				"Sales",
				String.Empty);

			string purchaseExclusions = DuplicateRowIdList(
				conn,
				"Purchase",
				"AND t.CompanyID = s.CompanyID");

			string salesIndex =
				"CREATE UNIQUE INDEX IF NOT EXISTS idx_sales_bill_fy " +
				"ON Sales(BillNumber, FiscalYear)" +
				salesExclusions;

			string purchaseIndex =
				"CREATE UNIQUE INDEX IF NOT EXISTS idx_purchase_company_bill_fy " +
				"ON Purchase(CompanyID, BillNumber, FiscalYear)" +
				purchaseExclusions;

			using (var cmd = new SQLiteCommand(salesIndex, conn))
				cmd.ExecuteNonQuery();

			using (var cmd = new SQLiteCommand(purchaseIndex, conn))
				cmd.ExecuteNonQuery();
		}

		private static void EnsureFiscalYearColumn(
			SQLiteConnection conn,
			string table)
		{
			bool hasColumn = false;

			using (var info =
				new SQLiteCommand($"PRAGMA table_info({table});", conn))
			using (var reader = info.ExecuteReader())
			{
				while (reader.Read())
				{
					if (string.Equals(
							reader["name"]?.ToString(),
							"FiscalYear",
							StringComparison.OrdinalIgnoreCase))
					{
						hasColumn = true;
						break;
					}
				}
			}

			if (!hasColumn)
			{
				using var alter = new SQLiteCommand(
					$"ALTER TABLE {table} ADD COLUMN FiscalYear TEXT;",
					conn);

				alter.ExecuteNonQuery();
			}
		}

		// Returns "" when the table has no duplicate bill numbers (full
		// unique index), otherwise a partial-index WHERE clause that
		// excludes exactly the rowids belonging to duplicate groups.
		private static string DuplicateRowIdList(
			SQLiteConnection conn,
			string table,
			string extraCompanyMatch)
		{
			string duplicateQuery = $@"
                SELECT s.rowid
                FROM {table} s
                WHERE s.FiscalYear IS NOT NULL
                  AND EXISTS
                  (
                      SELECT 1
                      FROM {table} t
                      WHERE t.BillNumber = s.BillNumber
                        AND t.FiscalYear = s.FiscalYear
                        {extraCompanyMatch}
                        AND t.rowid != s.rowid
                  )";

			var rowIds = new List<long>();

			using (var cmd = new SQLiteCommand(duplicateQuery, conn))
			using (var reader = cmd.ExecuteReader())
			{
				while (reader.Read())
				{
					rowIds.Add(Convert.ToInt64(reader[0]));
				}
			}

			if (rowIds.Count == 0)
				return String.Empty;

			return
				" WHERE rowid NOT IN (" +
				String.Join(", ", rowIds) +
				")";
		}

		private static void AddParameters(
			SQLiteCommand cmd,
			Dictionary<string, object>? parameters)
		{
			if (parameters == null)
				return;

			foreach (var pair in parameters)
			{
				cmd.Parameters.AddWithValue(
					pair.Key,
					pair.Value ?? DBNull.Value);
			}
		}

		public static List<string> GetProductNames()
		{
			var names = new List<string>();

			const string query =
				"SELECT DISTINCT ProductName FROM Product";

			using var conn = GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(query, conn);
			using var reader = cmd.ExecuteReader();

			while (reader.Read())
			{
				if (!reader.IsDBNull(0))
				{
					names.Add(reader.GetString(0));
				}
			}

			return names;
		}

		public static List<string> GetCompanyNames()
		{
			var names = new List<string>();

			const string query =
				"SELECT DISTINCT CompanyName FROM Company";

			using var conn = GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(query, conn);
			using var reader = cmd.ExecuteReader();

			while (reader.Read())
			{
				if (!reader.IsDBNull(0))
				{
					names.Add(reader.GetString(0));
				}
			}

			return names;
		}

		public static int GetCompanyIdByName(string companyName)
		{
			if (string.IsNullOrWhiteSpace(companyName))
				return -1;

			using var conn = GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(
				"SELECT CompanyID FROM Company " +
				"WHERE CompanyName = @name",
				conn);

			cmd.Parameters.AddWithValue(
				"@name",
				companyName.Trim());

			var result = cmd.ExecuteScalar();

			if (result != null &&
				int.TryParse(result.ToString(), out int companyId))
			{
				return companyId;
			}

			return -1;
		}

		public static List<string> GetInternationalCompanyNames()
		{
			var names = new List<string>();

			const string query =
				"SELECT DISTINCT SupplierName " +
				"FROM InternationalSupplier";

			using var conn = GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(query, conn);
			using var reader = cmd.ExecuteReader();

			while (reader.Read())
			{
				if (!reader.IsDBNull(0))
				{
					names.Add(reader.GetString(0));
				}
			}

			return names;
		}

		public static long GetProductId(string productName)
		{
			using var conn = GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(
				"SELECT ProductID FROM Product " +
				"WHERE ProductName = @name",
				conn);

			cmd.Parameters.AddWithValue("@name", productName);

			object? result = cmd.ExecuteScalar();

			return result != null
				? Convert.ToInt64(result)
				: 0;
		}

		public static int GetOrCreateCompany(string companyName)
		{
			var check = ExecuteScalar(
				"SELECT CompanyID FROM Company " +
				"WHERE CompanyName = @name",
				new Dictionary<string, object>
				{
					{ "@name", companyName }
				});

			if (check != null)
				return Convert.ToInt32(check);

			return Convert.ToInt32(
				ExecuteScalar(
					"INSERT INTO Company " +
					"(CompanyName, VAT_PAN_Number) " +
					"VALUES (@name, 'N/A'); " +
					"SELECT last_insert_rowid();",
					new Dictionary<string, object>
					{
						{ "@name", companyName }
					}));
		}

		public static double GetLatestBalance(int companyId)
		{
			const string query =
				"SELECT Balance FROM Ledger " +
				"WHERE CompanyID = @CompanyID " +
				"ORDER BY LedgerID DESC LIMIT 1";

			var parameters = new Dictionary<string, object>
			{
				{ "@CompanyID", companyId }
			};

			object? result = ExecuteScalar(query, parameters);

			return result != null
				? Convert.ToDouble(result)
				: 0;
		}

		public static decimal GetOpeningBalanceBeforeNepaliDate(
			int companyId,
			string nepaliDate)
		{
			string manualBalanceQuery = @"
                SELECT OpeningBalance
                FROM OpeningBalances
                WHERE CompanyID = @companyId
                  AND OpeningDate = @date
                LIMIT 1;
            ";

			var manualResult = ExecuteScalar(
				manualBalanceQuery,
				new Dictionary<string, object>
				{
					{ "@companyId", companyId },
					{ "@date", nepaliDate }
				});

			if (manualResult != null)
			{
				return Convert.ToDecimal(manualResult);
			}

			string query = @"
                SELECT
                    IFNULL(SUM(Credit), 0)
                    -
                    IFNULL(SUM(Debit), 0)
                    AS OpeningBalance
                FROM
                (
                    SELECT
                        SUM(TotalPurchasePrice) AS Debit,
                        0 AS Credit,
                        NepaliDate
                    FROM Purchase
                    WHERE CompanyID = @companyId
                      AND NepaliDate < @date

                    UNION ALL

                    SELECT
                        SUM(Amount) AS Debit,
                        0 AS Credit,
                        NepaliDate
                    FROM Payment
                    WHERE CompanyID = @companyId
                      AND NepaliDate < @date
                      AND PaymentType = 'Made'

                    UNION ALL

                    SELECT
                        0 AS Debit,
                        SUM(TotalSalesAmount) AS Credit,
                        NepaliDate
                    FROM Sales
                    WHERE CompanyID = @companyId
                      AND NepaliDate < @date

                    UNION ALL

                    SELECT
                        SUM(Discount) AS Debit,
                        0 AS Credit,
                        NepaliDate
                    FROM Sales
                    WHERE CompanyID = @companyId
                      AND NepaliDate < @date

                    UNION ALL

                    SELECT
                        0 AS Debit,
                        SUM(Discount) AS Credit,
                        NepaliDate
                    FROM Purchase
                    WHERE CompanyID = @companyId
                      AND NepaliDate < @date
                ) AS LedgerData;
            ";

			var result = ExecuteScalar(
				query,
				new Dictionary<string, object>
				{
					{ "@companyId", companyId },
					{ "@date", nepaliDate }
				});

			return result != null
				? Convert.ToDecimal(result)
				: 0m;
		}

		public static int ExecuteNonQueryWithResult(
			string query,
			Dictionary<string, object>? parameters = null)
		{
			using var conn = GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(query, conn);

			AddParameters(cmd, parameters);

			return cmd.ExecuteNonQuery();
		}

		public static void AddExpense(
			int categoryId,
			decimal amount,
			DateTime date,
			string nepaliDate,
			string description)
		{
			using var conn = GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(
				@"INSERT INTO Expenses
                    (
                        CategoryID,
                        Amount,
                        ExpenseDate,
                        NepaliDate,
                        Description
                    )
                  VALUES
                    (
                        @cid,
                        @amt,
                        @date,
                        @nepaliDate,
                        @desc
                    )",
				conn);

			cmd.Parameters.AddWithValue("@cid", categoryId);
			cmd.Parameters.AddWithValue("@amt", amount);
			cmd.Parameters.AddWithValue(
				"@date",
				date.ToString("yyyy-MM-dd"));
			cmd.Parameters.AddWithValue(
				"@nepaliDate",
				nepaliDate);
			cmd.Parameters.AddWithValue(
				"@desc",
				description);

			cmd.ExecuteNonQuery();
		}

		public static DataTable GetExpenses(
			string categoryName,
			string type,
			string fromNepDate,
			string toNepDate)
		{
			string query = @"
                SELECT
                    e.ExpenseID,
                    c.CategoryName,
                    e.Amount,
                    e.NepaliDate,
                    e.Description
                FROM Expenses e
                INNER JOIN ExpenseCategories c
                    ON e.CategoryID = c.CategoryID
                WHERE 1 = 1
            ";

			var parameters = new Dictionary<string, object>();

			if (!string.IsNullOrEmpty(categoryName))
			{
				query +=
					" AND c.CategoryName = @categoryName";

				parameters.Add(
					"@categoryName",
					categoryName);
			}

			if (!string.IsNullOrEmpty(type))
			{
				query +=
					" AND c.CategoryType = @type";

				parameters.Add(
					"@type",
					type);
			}

			if (!string.IsNullOrEmpty(fromNepDate))
			{
				query +=
					" AND e.NepaliDate >= @fromNepDate";

				parameters.Add(
					"@fromNepDate",
					fromNepDate);
			}

			if (!string.IsNullOrEmpty(toNepDate))
			{
				query +=
					" AND e.NepaliDate <= @toNepDate";

				parameters.Add(
					"@toNepDate",
					toNepDate);
			}

			query += " ORDER BY e.NepaliDate DESC";

			return GetData(query, parameters);
		}

		public static void DeleteExpense(int expenseId)
		{
			using var conn = GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(
				"DELETE FROM Expenses " +
				"WHERE ExpenseID = @eid",
				conn);

			cmd.Parameters.AddWithValue(
				"@eid",
				expenseId);

			cmd.ExecuteNonQuery();
		}

		public static void UpdateExpense(
			int expenseId,
			int categoryId,
			decimal amount,
			DateTime expenseAdDate,
			string nepaliDate,
			string description)
		{
			using var conn = GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(
				@"UPDATE Expenses
                  SET
                    CategoryID = @categoryId,
                    Amount = @amount,
                    ExpenseDate = @expenseDate,
                    NepaliDate = @nepaliDate,
                    Description = @description
                  WHERE ExpenseID = @expenseId",
				conn);

			cmd.Parameters.AddWithValue(
				"@categoryId",
				categoryId);

			cmd.Parameters.AddWithValue(
				"@amount",
				amount);

			cmd.Parameters.AddWithValue(
				"@expenseDate",
				expenseAdDate.ToString("yyyy-MM-dd"));

			cmd.Parameters.AddWithValue(
				"@nepaliDate",
				nepaliDate);

			cmd.Parameters.AddWithValue(
				"@description",
				description);

			cmd.Parameters.AddWithValue(
				"@expenseId",
				expenseId);

			cmd.ExecuteNonQuery();
		}

		public static long GetOrCreateCustomer(
			string customerName)
		{
			if (string.IsNullOrWhiteSpace(customerName))
				return 0;

			using var conn = GetConnection();
			conn.Open();

			using (var cmd = new SQLiteCommand(
				"SELECT CustomerID FROM Customer " +
				"WHERE CustomerName = @name",
				conn))
			{
				cmd.Parameters.AddWithValue(
					"@name",
					customerName.Trim());

				var result = cmd.ExecuteScalar();

				if (result != null &&
					long.TryParse(
						result.ToString(),
						out long customerId))
				{
					return customerId;
				}
			}

			using (var cmd = new SQLiteCommand(
				"INSERT INTO Customer (CustomerName) " +
				"VALUES (@name); " +
				"SELECT last_insert_rowid();",
				conn))
			{
				cmd.Parameters.AddWithValue(
					"@name",
					customerName.Trim());

				var newId = cmd.ExecuteScalar();

				if (newId != null &&
					long.TryParse(
						newId.ToString(),
						out long insertedId))
				{
					return insertedId;
				}
			}

			return 0;
		}

		public static void SaveOpeningBalance(
			int companyId,
			string openingDate,
			decimal amount,
			string? note = null)
		{
			string query = @"
                INSERT OR REPLACE INTO OpeningBalances
                (
                    CompanyID,
                    OpeningDate,
                    OpeningBalance,
                    Note
                )
                VALUES
                (
                    @CompanyID,
                    @OpeningDate,
                    @OpeningBalance,
                    @Note
                )";

			var parameters = new Dictionary<string, object>
			{
				{ "@CompanyID", companyId },
				{ "@OpeningDate", openingDate },
				{ "@OpeningBalance", amount },
				{ "@Note", note ?? string.Empty }
			};

			ExecuteNonQuery(query, parameters);
		}
	}
}