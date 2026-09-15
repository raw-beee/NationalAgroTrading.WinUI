using System;
using System.Collections.Generic;
using System.Data.SQLite;
using NationalAgroTrading.WinUI.Data;
using NationalAgroTrading.WinUI.Models;

namespace NationalAgroTrading.WinUI.Services
{
	public static class SaleService
	{
		public static long GetProductId(string productName)
		{
			using var conn = DatabaseHelper.GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(
				"SELECT ProductID FROM Product WHERE ProductName = @ProductName",
				conn);

			cmd.Parameters.AddWithValue("@ProductName", productName);

			object result = cmd.ExecuteScalar();

			return result == null || result == DBNull.Value
				? 0
				: Convert.ToInt64(result);
		}

		public static long GetCompanyId(string companyName)
		{
			using var conn = DatabaseHelper.GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(
				"SELECT CompanyID FROM Company WHERE CompanyName = @CompanyName",
				conn);

			cmd.Parameters.AddWithValue("@CompanyName", companyName);

			object result = cmd.ExecuteScalar();

			return result == null || result == DBNull.Value
				? 0
				: Convert.ToInt64(result);
		}

		public static long GetCustomerId(
			SQLiteConnection conn,
			string customerName)
		{
			using var find = new SQLiteCommand(
				"SELECT CustomerID FROM Customer WHERE CustomerName = @CustomerName",
				conn);

			find.Parameters.AddWithValue(
				"@CustomerName",
				customerName);

			object result = find.ExecuteScalar();

			if (result != null && result != DBNull.Value)
				return Convert.ToInt64(result);

			using var insert = new SQLiteCommand(
				@"INSERT INTO Customer(CustomerName)
                  VALUES(@CustomerName);
                  SELECT last_insert_rowid();",
				conn);

			insert.Parameters.AddWithValue(
				"@CustomerName",
				customerName);

			return Convert.ToInt64(insert.ExecuteScalar());
		}

		public static List<string> GetProductNames()
		{
			var result = new List<string>();

			using var conn = DatabaseHelper.GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(
				"SELECT ProductName FROM Product ORDER BY ProductName",
				conn);

			using var reader = cmd.ExecuteReader();

			while (reader.Read())
			{
				result.Add(reader["ProductName"]?.ToString() ?? "");
			}

			return result;
		}

		public static List<string> GetCompanyNames()
		{
			var result = new List<string>();

			using var conn = DatabaseHelper.GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(
				"SELECT CompanyName FROM Company ORDER BY CompanyName",
				conn);

			using var reader = cmd.ExecuteReader();

			while (reader.Read())
			{
				result.Add(reader["CompanyName"]?.ToString() ?? "");
			}

			return result;
		}

		public static List<string> GetCustomerNames()
		{
			var result = new List<string>();

			using var conn = DatabaseHelper.GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(
				"SELECT CustomerName FROM Customer ORDER BY CustomerName",
				conn);

			using var reader = cmd.ExecuteReader();

			while (reader.Read())
			{
				result.Add(reader["CustomerName"]?.ToString() ?? "");
			}

			return result;
		}

		public static ProductSaleInfo GetProductInfo(
			string productName,
			bool wholesale)
		{
			using var conn = DatabaseHelper.GetConnection();
			conn.Open();

			string priceColumn =
				wholesale
					? "WholesalePrice"
					: "RetailPrice";

			string sql = $@"
                SELECT
                    ProductID,
                    ProductName,
                    {priceColumn} AS Price,
                    IsVattable,
                    HSCode
                FROM Product
                WHERE ProductName = @ProductName";

			using var cmd = new SQLiteCommand(sql, conn);

			cmd.Parameters.AddWithValue(
				"@ProductName",
				productName);

			using var reader = cmd.ExecuteReader();

			if (!reader.Read())
				return null;

			return new ProductSaleInfo
			{
				ProductID = Convert.ToInt64(reader["ProductID"]),
				ProductName = reader["ProductName"]?.ToString() ?? "",
				Price = reader["Price"] == DBNull.Value
					? 0
					: Convert.ToDecimal(reader["Price"]),
				IsVattable =
					reader["IsVattable"] != DBNull.Value &&
					Convert.ToInt32(reader["IsVattable"]) == 1,
				HSCode =
					reader["HSCode"] == DBNull.Value
						? ""
						: reader["HSCode"].ToString() ?? ""
			};
		}

		public static decimal GetStock(long productId)
		{
			using var conn = DatabaseHelper.GetConnection();
			conn.Open();

			using var cmd = new SQLiteCommand(
				@"SELECT TotalStockLeft
                  FROM Inventory
                  WHERE ProductID = @ProductID",
				conn);

			cmd.Parameters.AddWithValue(
				"@ProductID",
				productId);

			object result = cmd.ExecuteScalar();

			return result == null || result == DBNull.Value
				? 0
				: Convert.ToDecimal(result);
		}

		public static long SaveCompanySale(
			string companyName,
			string billNumber,
			string nepaliDate,
			DateTime saleDate,
			List<SaleItem> items,
			decimal discountPercent)
		{
			if (items == null || items.Count == 0)
				throw new InvalidOperationException(
					"No sale items were supplied.");

			if (string.IsNullOrWhiteSpace(companyName))
				throw new InvalidOperationException(
					"Company name is required.");

			if (string.IsNullOrWhiteSpace(billNumber))
				throw new InvalidOperationException(
					"Bill number is required.");

			long companyId = GetCompanyId(companyName);

			if (companyId == 0)
				throw new InvalidOperationException(
					"Company was not found.");

			decimal subtotal =
				0m;

			foreach (var item in items)
				subtotal += item.Subtotal;

			decimal totalDiscount =
				subtotal * discountPercent / 100m;

			long firstSaleId = 0;

			using var conn = DatabaseHelper.GetConnection();
			conn.Open();

			using var transaction = conn.BeginTransaction();

			try
			{
				foreach (var item in items)
				{
					long productId =
						item.ProductID > 0
							? item.ProductID
							: GetProductIdFromConnection(
								conn,
								item.ProductName);

					if (productId == 0)
						throw new InvalidOperationException(
							$"Product not found: {item.ProductName}");

					decimal itemSubtotal = item.Subtotal;

					decimal itemDiscount =
						subtotal == 0
							? 0
							: itemSubtotal /
							  subtotal *
							  totalDiscount;

					decimal afterDiscount =
						itemSubtotal - itemDiscount;

					decimal vat =
						item.IsVattable
							? itemSubtotal * 0.13m
							: 0m;

					decimal totalWithVat =
						afterDiscount + vat;

					decimal stock =
						GetStockFromConnection(
							conn,
							productId);

					if (stock < item.Quantity)
					{
						throw new InvalidOperationException(
							$"Insufficient stock for {item.ProductName}. " +
							$"Available: {stock}, Required: {item.Quantity}");
					}

					string sql = @"
                        INSERT INTO Sales
                        (
                            ProductID,
                            CompanyID,
                            Quantity,
                            Price,
                            Discount,
                            DPercentage,
                            SaleDate,
                            TotalSalesAmount,
                            TotalAmountAfterDiscount,
                            BillNumber,
                            NepaliDate,
                            VATAmount,
                            TotalAmountWithVAT
                        )
                        VALUES
                        (
                            @ProductID,
                            @CompanyID,
                            @Quantity,
                            @Price,
                            @Discount,
                            @DPercentage,
                            @SaleDate,
                            @TotalSalesAmount,
                            @TotalAmountAfterDiscount,
                            @BillNumber,
                            @NepaliDate,
                            @VATAmount,
                            @TotalAmountWithVAT
                        );

                        SELECT last_insert_rowid();";

					using var cmd =
						new SQLiteCommand(
							sql,
							conn,
							transaction);

					cmd.Parameters.AddWithValue(
						"@ProductID",
						productId);

					cmd.Parameters.AddWithValue(
						"@CompanyID",
						companyId);

					cmd.Parameters.AddWithValue(
						"@Quantity",
						item.Quantity);

					cmd.Parameters.AddWithValue(
						"@Price",
						item.Price);

					cmd.Parameters.AddWithValue(
						"@Discount",
						itemDiscount);

					cmd.Parameters.AddWithValue(
						"@DPercentage",
						discountPercent);

					cmd.Parameters.AddWithValue(
						"@SaleDate",
						saleDate.ToString("yyyy-MM-dd"));

					cmd.Parameters.AddWithValue(
						"@TotalSalesAmount",
						itemSubtotal);

					cmd.Parameters.AddWithValue(
						"@TotalAmountAfterDiscount",
						afterDiscount);

					cmd.Parameters.AddWithValue(
						"@BillNumber",
						$"Bill {billNumber}");

					cmd.Parameters.AddWithValue(
						"@NepaliDate",
						nepaliDate ?? "");

					cmd.Parameters.AddWithValue(
						"@VATAmount",
						vat);

					cmd.Parameters.AddWithValue(
						"@TotalAmountWithVAT",
						totalWithVat);

					long saleId =
						Convert.ToInt64(
							cmd.ExecuteScalar());

					if (firstSaleId == 0)
						firstSaleId = saleId;

					AddSyncQueue(
						conn,
						transaction,
						saleId);

					UpdateStock(
						conn,
						transaction,
						productId,
						item.Quantity);
				}

				transaction.Commit();

				return firstSaleId;
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}

		public static long SaveCounterSale(
			string customerName,
			string billNumber,
			string nepaliDate,
			DateTime saleDate,
			List<SaleItem> items,
			decimal discountPercent)
		{
			if (items == null || items.Count == 0)
				throw new InvalidOperationException(
					"No sale items were supplied.");

			if (string.IsNullOrWhiteSpace(customerName))
				throw new InvalidOperationException(
					"Customer name is required.");

			long firstSaleId = 0;

			decimal subtotal =
				0m;

			foreach (var item in items)
				subtotal += item.Subtotal;

			decimal totalDiscount =
				subtotal * discountPercent / 100m;

			using var conn = DatabaseHelper.GetConnection();
			conn.Open();

			using var transaction = conn.BeginTransaction();

			try
			{
				long customerId =
					GetCustomerId(conn, customerName);

				foreach (var item in items)
				{
					long productId =
						item.ProductID > 0
							? item.ProductID
							: GetProductIdFromConnection(
								conn,
								item.ProductName);

					if (productId == 0)
						throw new InvalidOperationException(
							$"Product not found: {item.ProductName}");

					decimal itemSubtotal =
						item.Subtotal;

					decimal itemDiscount =
						subtotal == 0
							? 0
							: itemSubtotal /
							  subtotal *
							  totalDiscount;

					decimal afterDiscount =
						itemSubtotal - itemDiscount;

					decimal vat =
						item.IsVattable
							? itemSubtotal * 0.13m
							: 0m;

					decimal totalWithVat =
						afterDiscount + vat;

					decimal stock =
						GetStockFromConnection(
							conn,
							productId);

					if (stock < item.Quantity)
					{
						throw new InvalidOperationException(
							$"Insufficient stock for {item.ProductName}. " +
							$"Available: {stock}, Required: {item.Quantity}");
					}

					string sql = @"
                        INSERT INTO Sales
                        (
                            ProductID,
                            CustomerID,
                            Quantity,
                            Price,
                            Discount,
                            DPercentage,
                            SaleDate,
                            TotalSalesAmount,
                            TotalAmountAfterDiscount,
                            BillNumber,
                            NepaliDate,
                            VATAmount,
                            TotalAmountWithVAT
                        )
                        VALUES
                        (
                            @ProductID,
                            @CustomerID,
                            @Quantity,
                            @Price,
                            @Discount,
                            @DPercentage,
                            @SaleDate,
                            @TotalSalesAmount,
                            @TotalAmountAfterDiscount,
                            @BillNumber,
                            @NepaliDate,
                            @VATAmount,
                            @TotalAmountWithVAT
                        );

                        SELECT last_insert_rowid();";

					using var cmd =
						new SQLiteCommand(
							sql,
							conn,
							transaction);

					cmd.Parameters.AddWithValue(
						"@ProductID",
						productId);

					cmd.Parameters.AddWithValue(
						"@CustomerID",
						customerId);

					cmd.Parameters.AddWithValue(
						"@Quantity",
						item.Quantity);

					cmd.Parameters.AddWithValue(
						"@Price",
						item.Price);

					cmd.Parameters.AddWithValue(
						"@Discount",
						itemDiscount);

					cmd.Parameters.AddWithValue(
						"@DPercentage",
						discountPercent);

					cmd.Parameters.AddWithValue(
						"@SaleDate",
						saleDate.ToString("yyyy-MM-dd"));

					cmd.Parameters.AddWithValue(
						"@TotalSalesAmount",
						itemSubtotal);

					cmd.Parameters.AddWithValue(
						"@TotalAmountAfterDiscount",
						afterDiscount);

					cmd.Parameters.AddWithValue(
						"@BillNumber",
						$"Bill {billNumber}");

					cmd.Parameters.AddWithValue(
						"@NepaliDate",
						nepaliDate ?? "");

					cmd.Parameters.AddWithValue(
						"@VATAmount",
						vat);

					cmd.Parameters.AddWithValue(
						"@TotalAmountWithVAT",
						totalWithVat);

					long saleId =
						Convert.ToInt64(
							cmd.ExecuteScalar());

					if (firstSaleId == 0)
						firstSaleId = saleId;

					UpdateStock(
						conn,
						transaction,
						productId,
						item.Quantity);
				}

				transaction.Commit();

				return firstSaleId;
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}

		public static List<Sale> GetCompanySales(
			string search = "")
		{
			var result = new List<Sale>();

			using var conn = DatabaseHelper.GetConnection();
			conn.Open();

			string sql = @"
                SELECT
                    s.SaleID,
                    s.ProductID,
                    s.CompanyID,
                    s.CustomerID,
                    p.ProductName,
                    c.CompanyName,
                    cu.CustomerName,
                    s.Quantity,
                    s.Price,
                    s.TotalSalesAmount,
                    s.Discount,
                    s.DPercentage,
                    s.TotalAmountAfterDiscount,
                    s.VATAmount,
                    s.TotalAmountWithVAT,
                    s.BillNumber,
                    s.NepaliDate,
                    s.SaleDate
                FROM Sales s
                JOIN Product p
                    ON s.ProductID = p.ProductID
                LEFT JOIN Company c
                    ON s.CompanyID = c.CompanyID
                LEFT JOIN Customer cu
                    ON s.CustomerID = cu.CustomerID
                WHERE s.CompanyID IS NOT NULL
                  AND
                  (
                      @Search = ''
                      OR s.BillNumber LIKE @LikeSearch
                      OR c.CompanyName LIKE @LikeSearch
                  )
                ORDER BY s.SaleID DESC";

			using var cmd =
				new SQLiteCommand(sql, conn);

			cmd.Parameters.AddWithValue(
				"@Search",
				search ?? "");

			cmd.Parameters.AddWithValue(
				"@LikeSearch",
				$"%{search}%");

			using var reader =
				cmd.ExecuteReader();

			while (reader.Read())
			{
				result.Add(ReadSale(reader));
			}

			return result;
		}

		public static List<Sale> GetCounterSales(
			string search = "")
		{
			var result = new List<Sale>();

			using var conn = DatabaseHelper.GetConnection();
			conn.Open();

			string sql = @"
                SELECT
                    s.SaleID,
                    s.ProductID,
                    s.CompanyID,
                    s.CustomerID,
                    p.ProductName,
                    c.CompanyName,
                    cu.CustomerName,
                    s.Quantity,
                    s.Price,
                    s.TotalSalesAmount,
                    s.Discount,
                    s.DPercentage,
                    s.TotalAmountAfterDiscount,
                    s.VATAmount,
                    s.TotalAmountWithVAT,
                    s.BillNumber,
                    s.NepaliDate,
                    s.SaleDate
                FROM Sales s
                JOIN Product p
                    ON s.ProductID = p.ProductID
                LEFT JOIN Company c
                    ON s.CompanyID = c.CompanyID
                LEFT JOIN Customer cu
                    ON s.CustomerID = cu.CustomerID
                WHERE s.CustomerID IS NOT NULL
                  AND
                  (
                      @Search = ''
                      OR s.BillNumber LIKE @LikeSearch
                      OR p.ProductName LIKE @LikeSearch
                      OR cu.CustomerName LIKE @LikeSearch
                  )
                ORDER BY s.SaleID DESC";

			using var cmd =
				new SQLiteCommand(sql, conn);

			cmd.Parameters.AddWithValue(
				"@Search",
				search ?? "");

			cmd.Parameters.AddWithValue(
				"@LikeSearch",
				$"%{search}%");

			using var reader =
				cmd.ExecuteReader();

			while (reader.Read())
			{
				result.Add(ReadSale(reader));
			}

			return result;
		}

		private static Sale ReadSale(
			SQLiteDataReader reader)
		{
			return new Sale
			{
				SaleID =
					Convert.ToInt64(reader["SaleID"]),

				ProductID =
					Convert.ToInt64(reader["ProductID"]),

				CompanyID =
					reader["CompanyID"] == DBNull.Value
						? null
						: Convert.ToInt64(reader["CompanyID"]),

				CustomerID =
					reader["CustomerID"] == DBNull.Value
						? null
						: Convert.ToInt64(reader["CustomerID"]),

				ProductName =
					reader["ProductName"]?.ToString() ?? "",

				CompanyName =
					reader["CompanyName"]?.ToString() ?? "",

				CustomerName =
					reader["CustomerName"]?.ToString() ?? "",

				Quantity =
					Convert.ToInt64(reader["Quantity"]),

				Price =
					Convert.ToDecimal(reader["Price"]),

				TotalSalesAmount =
					Convert.ToDecimal(reader["TotalSalesAmount"]),

				Discount =
					reader["Discount"] == DBNull.Value
						? 0
						: Convert.ToDecimal(reader["Discount"]),

				DiscountPercent =
					reader["DPercentage"] == DBNull.Value
						? 0
						: Convert.ToDecimal(reader["DPercentage"]),

				TotalAmountAfterDiscount =
					reader["TotalAmountAfterDiscount"] == DBNull.Value
						? 0
						: Convert.ToDecimal(
							reader["TotalAmountAfterDiscount"]),

				VATAmount =
					reader["VATAmount"] == DBNull.Value
						? 0
						: Convert.ToDecimal(reader["VATAmount"]),

				TotalAmountWithVAT =
					reader["TotalAmountWithVAT"] == DBNull.Value
						? 0
						: Convert.ToDecimal(
							reader["TotalAmountWithVAT"]),

				BillNumber =
					reader["BillNumber"]?.ToString() ?? "",

				NepaliDate =
					reader["NepaliDate"]?.ToString() ?? "",

				SaleDate =
					reader["SaleDate"] == DBNull.Value
						? DateTime.MinValue
						: Convert.ToDateTime(reader["SaleDate"])
			};
		}

		private static long GetProductIdFromConnection(
			SQLiteConnection conn,
			string productName)
		{
			using var cmd = new SQLiteCommand(
				@"SELECT ProductID
                  FROM Product
                  WHERE ProductName = @ProductName",
				conn);

			cmd.Parameters.AddWithValue(
				"@ProductName",
				productName);

			object result = cmd.ExecuteScalar();

			return result == null || result == DBNull.Value
				? 0
				: Convert.ToInt64(result);
		}

		private static decimal GetStockFromConnection(
			SQLiteConnection conn,
			long productId)
		{
			using var cmd = new SQLiteCommand(
				@"SELECT TotalStockLeft
                  FROM Inventory
                  WHERE ProductID = @ProductID",
				conn);

			cmd.Parameters.AddWithValue(
				"@ProductID",
				productId);

			object result = cmd.ExecuteScalar();

			return result == null || result == DBNull.Value
				? 0
				: Convert.ToDecimal(result);
		}

		private static void UpdateStock(
			SQLiteConnection conn,
			SQLiteTransaction transaction,
			long productId,
			long quantity)
		{
			using var cmd = new SQLiteCommand(
				@"UPDATE Inventory
                  SET TotalStockLeft =
                      TotalStockLeft - @Quantity
                  WHERE ProductID = @ProductID",
				conn,
				transaction);

			cmd.Parameters.AddWithValue(
				"@Quantity",
				quantity);

			cmd.Parameters.AddWithValue(
				"@ProductID",
				productId);

			int affected =
				cmd.ExecuteNonQuery();

			if (affected == 0)
			{
				throw new InvalidOperationException(
					$"Inventory record not found for ProductID {productId}.");
			}
		}

		private static void AddSyncQueue(
			SQLiteConnection conn,
			SQLiteTransaction transaction,
			long saleId)
		{
			using var cmd = new SQLiteCommand(
				@"INSERT INTO SyncQueue
                  (
                      TableName,
                      RowId,
                      Action,
                      Synced,
                      LastUpdated
                  )
                  VALUES
                  (
                      'Sales',
                      @RowId,
                      'INSERT',
                      0,
                      datetime('now')
                  )",
				conn,
				transaction);

			cmd.Parameters.AddWithValue(
				"@RowId",
				saleId);

			cmd.ExecuteNonQuery();
		}
	}

	public sealed class ProductSaleInfo
	{
		public long ProductID { get; set; }

		public string ProductName { get; set; } = "";

		public decimal Price { get; set; }

		public bool IsVattable { get; set; }

		public string HSCode { get; set; } = "";
	}
}