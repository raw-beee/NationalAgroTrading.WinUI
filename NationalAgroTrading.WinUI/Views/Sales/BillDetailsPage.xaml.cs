using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NationalAgroTrading.WinUI.Data;
using NationalAgroTrading.WinUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace NationalAgroTrading.WinUI.Views.Sales
{
	public sealed partial class BillDetailsPage : Page
	{
		private readonly ObservableCollection<SalesDetailItem> details =
			new();

		private SalesBillItem? bill;

		private bool deleting;


		// ============================================================
		// CONSTRUCTOR
		// ============================================================

		public BillDetailsPage()
		{
			InitializeComponent();

			SalesDetailsListView.ItemsSource =
				details;
		}


		// ============================================================
		// NAVIGATION
		// ============================================================

		protected override void OnNavigatedTo(
			NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			if (e.Parameter is not SalesBillItem selectedBill)
			{
				return;
			}

			bill =
				selectedBill;

			BillNumberTextBlock.Text =
				$"Bill: {selectedBill.BillNumber}";

			CompanyTextBlock.Text =
				selectedBill.CompanyName;

			DateTextBlock.Text =
				selectedBill.NepaliDate;

			LoadDetails();
		}


		// ============================================================
		// LOAD BILL DETAILS
		// ============================================================

		private void LoadDetails()
		{
			if (bill == null)
			{
				return;
			}

			try
			{
				const string query =
					"""
					SELECT
						s.SaleID,
						s.ProductID,
						p.ProductName,
						s.Quantity,
						s.Price,
						s.TotalSalesAmount,
						s.Discount,
						s.VATAmount,
						s.TotalAmountAfterDiscount,
						s.TotalAmountWithVAT

					FROM Sales s

					INNER JOIN Product p
						ON s.ProductID = p.ProductID

					WHERE
						s.CompanyID = @CompanyID
						AND s.BillNumber = @BillNumber

					ORDER BY
						p.ProductName COLLATE NOCASE ASC
					""";

				DataTable table =
					DatabaseHelper.GetData(
						query,
						new Dictionary<string, object>
						{
							["@CompanyID"] =
								bill.CompanyID,

							["@BillNumber"] =
								bill.BillNumber
						});

				details.Clear();

				foreach (DataRow row in table.Rows)
				{
					long saleId =
						ToInt64(
							row["SaleID"]);

					long productId =
						ToInt64(
							row["ProductID"]);

					long quantity =
						ToInt64(
							row["Quantity"]);

					decimal price =
						ToDecimal(
							row["Price"]);

					decimal subtotal =
						ToDecimal(
							row["TotalSalesAmount"]);

					decimal discount =
						ToDecimal(
							row["Discount"]);

					decimal vat =
						ToDecimal(
							row["VATAmount"]);

					decimal total;

					if (row["TotalAmountWithVAT"] ==
						DBNull.Value)
					{
						decimal afterDiscount =
							ToDecimal(
								row["TotalAmountAfterDiscount"]);

						total =
							afterDiscount +
							vat;
					}
					else
					{
						total =
							ToDecimal(
								row["TotalAmountWithVAT"]);
					}

					details.Add(
						new SalesDetailItem
						{
							SaleID =
								saleId,

							ProductID =
								productId,

							ProductName =
								ToStringValue(
									row["ProductName"]),

							Quantity =
								quantity,

							Price =
								price,

							Subtotal =
								subtotal,

							Discount =
								discount,

							VAT =
								vat,

							Total =
								total
						});
				}

				UpdateSummary();
			}
			catch (Exception ex)
			{
				details.Clear();

				UpdateSummary();

				_ = ShowMessageAsync(
					"Unable to load bill details.\n\n" +
					ex.Message,
					"Sales Error");
			}
		}


		// ============================================================
		// SUMMARY
		// ============================================================

		private void UpdateSummary()
		{
			decimal subtotal =
				details.Sum(
					x => x.Subtotal);

			decimal discount =
				details.Sum(
					x => x.Discount);

			decimal vat =
				details.Sum(
					x => x.VAT);

			decimal total =
				details.Sum(
					x => x.Total);

			SubtotalTextBlock.Text =
				$"Subtotal: Rs. {subtotal:N2}";

			DiscountTextBlock.Text =
				$"Discount: Rs. {discount:N2}";

			VATTextBlock.Text =
				$"VAT: Rs. {vat:N2}";

			GrandTotalTextBlock.Text =
				$"Grand Total: Rs. {total:N2}";

			ItemCountTextBlock.Text =
				details.Count.ToString();
		}


		// ============================================================
		// EDIT SALE
		// ============================================================

		private void EditSaleButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (bill == null)
			{
				return;
			}

			Frame.Navigate(
				typeof(UpdateSalePage),
				bill);
		}


		// ============================================================
		// DELETE SALE BUTTON
		// ============================================================

		private async void DeleteSaleButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (bill == null ||
				XamlRoot == null ||
				deleting)
			{
				return;
			}

			ContentDialog dialog =
				new ContentDialog
				{
					Title =
						"Delete Sale",

					Content =
						$"Are you sure you want to delete {bill.BillNumber}?\n\n" +
						$"Company: {bill.CompanyName}\n" +
						$"Date: {bill.NepaliDate}\n" +
						$"Items: {details.Count}\n" +
						$"Amount: Rs. {bill.Amount:N2}\n\n" +
						"The sold quantities will be returned to inventory.",

					PrimaryButtonText =
						"Delete",

					CloseButtonText =
						"Cancel",

					DefaultButton =
						ContentDialogButton.Close,

					XamlRoot =
						XamlRoot
				};

			ContentDialogResult result =
				await dialog.ShowAsync();

			if (result !=
				ContentDialogResult.Primary)
			{
				return;
			}

			deleting = true;

			try
			{
				DeleteSaleBill();

				await ShowMessageAsync(
					"Sale deleted successfully.",
					"Success");

				if (Frame?.CanGoBack == true)
				{
					Frame.GoBack();
				}
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					"Unable to delete sale.\n\n" +
					ex.Message,
					"Sales Error");
			}
			finally
			{
				deleting = false;
			}
		}


		// ============================================================
		// DELETE COMPLETE BILL
		// ============================================================

		private void DeleteSaleBill()
		{
			if (bill == null)
			{
				throw new InvalidOperationException(
					"No sale bill selected.");
			}

			using var connection =
				DatabaseHelper.GetConnection();

			connection.Open();

			using var transaction =
				connection.BeginTransaction();

			try
			{
				// ----------------------------------------------------
				// STEP 1
				// Read all rows belonging to this bill.
				// ----------------------------------------------------

				const string selectQuery =
					"""
					SELECT
						SaleID,
						ProductID,
						Quantity

					FROM Sales

					WHERE
						CompanyID = @CompanyID
						AND BillNumber = @BillNumber
					""";

				var rows =
					new List<SaleDeleteRow>();

				using (
					var command =
						new System.Data.SQLite.SQLiteCommand(
							selectQuery,
							connection,
							transaction))
				{
					command.Parameters.AddWithValue(
						"@CompanyID",
						bill.CompanyID);

					command.Parameters.AddWithValue(
						"@BillNumber",
						bill.BillNumber);

					using var reader =
						command.ExecuteReader();

					while (reader.Read())
					{
						rows.Add(
							new SaleDeleteRow
							{
								SaleID =
									Convert.ToInt64(
										reader["SaleID"]),

								ProductID =
									Convert.ToInt64(
										reader["ProductID"]),

								Quantity =
									Convert.ToInt64(
										reader["Quantity"])
							});
					}
				}

				if (rows.Count == 0)
				{
					throw new InvalidOperationException(
						"Sale bill was not found.");
				}


				// ----------------------------------------------------
				// STEP 2
				// Return sold quantities to inventory.
				//
				// Grouping is important in case the same product
				// exists in more than one Sales row.
				// ----------------------------------------------------

				foreach (
					var group in rows.GroupBy(
						x => x.ProductID))
				{
					long quantity =
						group.Sum(
							x => x.Quantity);

					if (quantity <= 0)
					{
						continue;
					}

					const string inventoryQuery =
						"""
						UPDATE Inventory

						SET
							TotalStockLeft =
								TotalStockLeft + @Quantity

						WHERE
							ProductID = @ProductID
						""";

					using var inventoryCommand =
						new System.Data.SQLite.SQLiteCommand(
							inventoryQuery,
							connection,
							transaction);

					inventoryCommand.Parameters.AddWithValue(
						"@Quantity",
						quantity);

					inventoryCommand.Parameters.AddWithValue(
						"@ProductID",
						group.Key);

					int affected =
						inventoryCommand.ExecuteNonQuery();

					if (affected == 0)
					{
						throw new InvalidOperationException(
							$"Inventory record not found for ProductID {group.Key}.");
					}
				}


				// ----------------------------------------------------
				// STEP 3
				// Remove SyncQueue rows belonging to these SaleIDs.
				// ----------------------------------------------------

				foreach (SaleDeleteRow row in rows)
				{
					const string deleteSyncQuery =
						"""
						DELETE FROM SyncQueue

						WHERE
							TableName = 'Sales'
							AND RowId = @RowId
						""";

					using var syncCommand =
						new System.Data.SQLite.SQLiteCommand(
							deleteSyncQuery,
							connection,
							transaction);

					syncCommand.Parameters.AddWithValue(
						"@RowId",
						row.SaleID);

					syncCommand.ExecuteNonQuery();
				}


				// ----------------------------------------------------
				// STEP 4
				// Delete the Sales rows.
				// ----------------------------------------------------

				const string deleteSalesQuery =
					"""
					DELETE FROM Sales

					WHERE
						CompanyID = @CompanyID
						AND BillNumber = @BillNumber
					""";

				using (
					var deleteSalesCommand =
						new System.Data.SQLite.SQLiteCommand(
							deleteSalesQuery,
							connection,
							transaction))
				{
					deleteSalesCommand.Parameters.AddWithValue(
						"@CompanyID",
						bill.CompanyID);

					deleteSalesCommand.Parameters.AddWithValue(
						"@BillNumber",
						bill.BillNumber);

					int deleted =
						deleteSalesCommand.ExecuteNonQuery();

					if (deleted == 0)
					{
						throw new InvalidOperationException(
							"Sale bill could not be deleted.");
					}
				}


				// ----------------------------------------------------
				// STEP 5
				// Commit everything.
				// ----------------------------------------------------

				transaction.Commit();
			}
			catch
			{
				try
				{
					transaction.Rollback();
				}
				catch
				{
					// Ignore rollback failure.
				}

				throw;
			}
		}


		// ============================================================
		// BACK
		// ============================================================

		private void BackButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (deleting)
			{
				return;
			}

			if (Frame?.CanGoBack == true)
			{
				Frame.GoBack();
			}
		}


		// ============================================================
		// MESSAGE
		// ============================================================

		private async Task ShowMessageAsync(
			string message,
			string title)
		{
			if (XamlRoot == null)
			{
				return;
			}

			ContentDialog dialog =
				new ContentDialog
				{
					Title =
						title,

					Content =
						message,

					CloseButtonText =
						"OK",

					XamlRoot =
						XamlRoot
				};

			await dialog.ShowAsync();
		}


		// ============================================================
		// STRING HELPER
		// ============================================================

		private static string ToStringValue(
			object value)
		{
			if (value == null ||
				value == DBNull.Value)
			{
				return string.Empty;
			}

			return value.ToString()
				?? string.Empty;
		}


		// ============================================================
		// INT64 HELPER
		// ============================================================

		private static long ToInt64(
			object value)
		{
			if (value == null ||
				value == DBNull.Value)
			{
				return 0;
			}

			return Convert.ToInt64(value);
		}


		// ============================================================
		// DECIMAL HELPER
		// ============================================================

		private static decimal ToDecimal(
			object value)
		{
			if (value == null ||
				value == DBNull.Value)
			{
				return 0m;
			}

			return Convert.ToDecimal(value);
		}


		// ============================================================
		// DELETE ROW MODEL
		// ============================================================

		private sealed class SaleDeleteRow
		{
			public long SaleID { get; set; }

			public long ProductID { get; set; }

			public long Quantity { get; set; }
		}
	}
}