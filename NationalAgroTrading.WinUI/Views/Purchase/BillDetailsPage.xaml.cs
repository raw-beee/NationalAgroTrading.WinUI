using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;

namespace NationalAgroTrading.WinUI.Views.Purchase
{
	public sealed partial class BillDetailsPage : Page
	{
		private readonly ObservableCollection<PurchaseDetailItem> details = new();

		private PurchaseBillItem? bill;

		public BillDetailsPage()
		{
			InitializeComponent();

			PurchaseDetailsListView.ItemsSource = details;
		}

		protected override void OnNavigatedTo(
			NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			if (e.Parameter is not PurchaseBillItem selectedBill)
				return;

			bill = selectedBill;

			BillNumberTextBlock.Text =
				selectedBill.BillNumber;

			CompanyTextBlock.Text =
				$"Company: {selectedBill.CompanyName}";

			DateTextBlock.Text =
				$"Nepali Date: {selectedBill.NepaliDate}";

			LoadDetails();
		}

		private void LoadDetails()
		{
			if (bill == null)
				return;

			const string query = @"
                SELECT
                    p.PurchaseID,
                    p.ProductID,
                    pr.ProductName,
                    p.Quantity,
                    p.CostPrice,
                    p.TotalPurchasePrice,
                    p.Discount,
                    p.VATAmount,
                    p.TotalAmountWithVat
                FROM Purchase p
                INNER JOIN Product pr
                    ON p.ProductID = pr.ProductID
                WHERE
                    p.CompanyID = @CompanyID
                    AND p.BillNumber = @BillNumber
                ORDER BY
                    pr.ProductName COLLATE NOCASE ASC";

			DataTable table =
				DatabaseHelper.GetData(
					query,
					new Dictionary<string, object>
					{
						["@CompanyID"] = bill.CompanyID,
						["@BillNumber"] = bill.BillNumber
					});

			details.Clear();

			foreach (DataRow row in table.Rows)
			{
				decimal subtotal =
					ToDecimal(row["TotalPurchasePrice"]);

				decimal discount =
					ToDecimal(row["Discount"]);

				decimal vat =
					ToDecimal(row["VATAmount"]);

				decimal total =
					row["TotalAmountWithVat"] == DBNull.Value
						? subtotal - discount + vat
						: ToDecimal(row["TotalAmountWithVat"]);

				details.Add(
					new PurchaseDetailItem
					{
						PurchaseID =
							Convert.ToInt64(row["PurchaseID"]),

						ProductID =
							Convert.ToInt64(row["ProductID"]),

						ProductName =
							row["ProductName"]?.ToString()
							?? string.Empty,

						Quantity =
							Convert.ToInt64(row["Quantity"]),

						CostPrice =
							ToDecimal(row["CostPrice"]),

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

		private void UpdateSummary()
		{
			decimal subtotal =
				details.Sum(x => x.Subtotal);

			decimal discount =
				details.Sum(x => x.Discount);

			decimal vat =
				details.Sum(x => x.VAT);

			decimal total =
				details.Sum(x => x.Total);

			SubtotalTextBlock.Text =
				$"Subtotal: Rs. {subtotal:N2}";

			DiscountTextBlock.Text =
				$"Discount: Rs. {discount:N2}";

			VATTextBlock.Text =
				$"VAT: Rs. {vat:N2}";

			GrandTotalTextBlock.Text =
				$"Grand Total: Rs. {total:N2}";
		}

		private void EditPurchaseButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (bill == null)
				return;

			Frame.Navigate(
				typeof(UpdatePurchasePage),
				bill);
		}

		private async void DeletePurchaseButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (bill == null || XamlRoot == null)
				return;

			var dialog =
				new ContentDialog
				{
					Title = "Delete Purchase",
					Content =
						$"Are you sure you want to delete {bill.BillNumber}?\n\n" +
						"The purchased quantities will also be removed from inventory.",
					PrimaryButtonText = "Delete",
					CloseButtonText = "Cancel",
					DefaultButton = ContentDialogButton.Close,
					XamlRoot = XamlRoot
				};

			ContentDialogResult result =
				await dialog.ShowAsync();

			if (result != ContentDialogResult.Primary)
				return;

			try
			{
				DeletePurchaseBill();

				await ShowMessageAsync(
					"Purchase deleted successfully.",
					"Success");

				if (Frame.CanGoBack)
					Frame.GoBack();
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					"Unable to delete purchase.\n\n" +
					ex.Message,
					"Purchase Error");
			}
		}

		private void DeletePurchaseBill()
		{
			if (bill == null)
				throw new InvalidOperationException(
					"No purchase bill selected.");

			const string selectQuery = @"
                SELECT
                    p.PurchaseID,
                    p.ProductID,
                    pr.ProductName,
                    p.Quantity
                FROM Purchase p
                INNER JOIN Product pr
                    ON p.ProductID = pr.ProductID
                WHERE
                    p.CompanyID = @CompanyID
                    AND p.BillNumber = @BillNumber";

			DataTable rows =
				DatabaseHelper.GetData(
					selectQuery,
					new Dictionary<string, object>
					{
						["@CompanyID"] = bill.CompanyID,
						["@BillNumber"] = bill.BillNumber
					});

			if (rows.Rows.Count == 0)
				throw new InvalidOperationException(
					"Purchase bill was not found.");

			// One connection / one transaction for every write,
			// so a mid-way failure cannot leave stock decremented
			// with the purchase rows still present.

			using var conn =
				DatabaseHelper.GetConnection();

			conn.Open();

			using var transaction =
				conn.BeginTransaction();

			try
			{
				/*
				 * Reverse every purchase quantity from inventory,
				 * refusing to go below zero stock.
				 */
				foreach (DataRow row in rows.Rows)
				{
					long productId =
						Convert.ToInt64(row["ProductID"]);

					string productName =
						row["ProductName"]?.ToString()
						?? $"Product {productId}";

					long quantity =
						Convert.ToInt64(row["Quantity"]);

					object? stockValue =
						Scalar(
							conn,
							transaction,
							@"SELECT TotalStockLeft
                              FROM Inventory
                              WHERE ProductID = @ProductID",
							new Dictionary<string, object>
							{
								["@ProductID"] = productId
							});

					decimal stockLeft =
						stockValue == null ||
						stockValue == DBNull.Value
							? 0m
							: Convert.ToDecimal(stockValue);

					if (stockLeft < quantity)
					{
						throw new InvalidOperationException(
							$"'{productName}' cannot be removed from stock: " +
							$"this bill purchased {quantity} but only " +
							$"{stockLeft} remain (units were already sold). " +
							"Reverse those sales before deleting this bill.");
					}

					const string inventoryQuery = @"
                        UPDATE Inventory
                        SET
                            TotalStockLeft =
                                TotalStockLeft - @Quantity,
                            Quantity =
                                Quantity - @Quantity
                        WHERE ProductID = @ProductID";

					Exec(
						conn,
						transaction,
						inventoryQuery,
						new Dictionary<string, object>
						{
							["@Quantity"] = quantity,
							["@ProductID"] = productId
						});
				}

				/*
				 * CostPriceHistory has a foreign key to Purchase
				 * with ON DELETE NO ACTION.
				 *
				 * Therefore remove history records referring
				 * to these purchases before deleting Purchase.
				 */
				const string deleteHistoryQuery = @"
                    DELETE FROM CostPriceHistory
                    WHERE ReferencePurchaseID IN
                    (
                        SELECT PurchaseID
                        FROM Purchase
                        WHERE
                            CompanyID = @CompanyID
                            AND BillNumber = @BillNumber
                    )";

				Exec(
					conn,
					transaction,
					deleteHistoryQuery,
					new Dictionary<string, object>
					{
						["@CompanyID"] = bill.CompanyID,
						["@BillNumber"] = bill.BillNumber
					});

				/*
				 * Delete purchase rows.
				 */
				const string deletePurchaseQuery = @"
                    DELETE FROM Purchase
                    WHERE
                        CompanyID = @CompanyID
                        AND BillNumber = @BillNumber";

				Exec(
					conn,
					transaction,
					deletePurchaseQuery,
					new Dictionary<string, object>
					{
						["@CompanyID"] = bill.CompanyID,
						["@BillNumber"] = bill.BillNumber
					});

				/*
				 * Restore current Product.CostPrice from the
				 * most recent remaining purchase for each product.
				 *
				 * If there is no remaining purchase, leave the
				 * existing product cost untouched.
				 */
				foreach (DataRow row in rows.Rows)
				{
					long productId =
						Convert.ToInt64(row["ProductID"]);

					const string latestCostQuery = @"
                        SELECT CostPrice
                        FROM Purchase
                        WHERE ProductID = @ProductID
                        ORDER BY
                            PurchaseDate DESC,
                            PurchaseID DESC
                        LIMIT 1";

					object? latestCost =
						Scalar(
							conn,
							transaction,
							latestCostQuery,
							new Dictionary<string, object>
							{
								["@ProductID"] = productId
							});

					if (latestCost != null &&
						latestCost != DBNull.Value)
					{
						const string updateProductQuery = @"
                            UPDATE Product
                            SET CostPrice = @CostPrice
                            WHERE ProductID = @ProductID";

						Exec(
							conn,
							transaction,
							updateProductQuery,
							new Dictionary<string, object>
							{
								["@CostPrice"] =
									Convert.ToDecimal(latestCost),

								["@ProductID"] =
									productId
							});
					}
				}

				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}

		private void BackButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (Frame.CanGoBack)
				Frame.GoBack();
		}

		private static int Exec(
			SQLiteConnection conn,
			SQLiteTransaction transaction,
			string query,
			Dictionary<string, object> parameters)
		{
			using var cmd =
				new SQLiteCommand(
					query,
					conn,
					transaction);

			foreach (var pair in parameters)
			{
				cmd.Parameters.AddWithValue(
					pair.Key,
					pair.Value ?? DBNull.Value);
			}

			return cmd.ExecuteNonQuery();
		}


		private static object? Scalar(
			SQLiteConnection conn,
			SQLiteTransaction transaction,
			string query,
			Dictionary<string, object> parameters)
		{
			using var cmd =
				new SQLiteCommand(
					query,
					conn,
					transaction);

			foreach (var pair in parameters)
			{
				cmd.Parameters.AddWithValue(
					pair.Key,
					pair.Value ?? DBNull.Value);
			}

			return cmd.ExecuteScalar();
		}


		private static decimal ToDecimal(object value)
		{
			if (value == null ||
				value == DBNull.Value)
			{
				return 0m;
			}

			return Convert.ToDecimal(value);
		}

		private async Task ShowMessageAsync(
			string message,
			string title)
		{
			if (XamlRoot == null)
				return;

			var dialog =
				new ContentDialog
				{
					Title = title,
					Content = message,
					CloseButtonText = "OK",
					XamlRoot = XamlRoot
				};

			await dialog.ShowAsync();
		}
	}
}