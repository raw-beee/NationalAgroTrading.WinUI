using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NationalAgroTrading.WinUI.Data;
using NationalAgroTrading.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace NationalAgroTrading.WinUI.Views.Purchase
{
	public sealed partial class AddPurchasePage : Page
	{
		private const decimal VatRate = TaxCalculator.VatRate;

		private readonly ObservableCollection<PurchaseEntryItem> items = new();

		private List<string> companyNames = new();

		private List<string> productNames = new();

		public AddPurchasePage()
		{
			InitializeComponent();

			PurchaseItemsListView.ItemsSource =
				items;

			LoadCompanies();
			LoadProducts();

			UpdateTotals();
		}

		// ============================================================
		// COMPANY
		// ============================================================

		private void LoadCompanies()
		{
			companyNames =
				DatabaseHelper.GetCompanyNames();

			CompanyAutoSuggestBox.ItemsSource =
				companyNames;
		}

		private void CompanyAutoSuggestBox_TextChanged(
			AutoSuggestBox sender,
			AutoSuggestBoxTextChangedEventArgs args)
		{
			if (args.Reason !=
				AutoSuggestionBoxTextChangeReason.UserInput)
			{
				return;
			}

			string search =
				sender.Text.Trim();

			sender.ItemsSource =
				string.IsNullOrWhiteSpace(search)
					? companyNames
					: companyNames
						.Where(
							x => x.Contains(
								search,
								StringComparison.OrdinalIgnoreCase))
						.ToList();
		}

		// ============================================================
		// PRODUCT
		// ============================================================

		private void LoadProducts()
		{
			productNames =
				DatabaseHelper.GetProductNames();

			ProductAutoSuggestBox.ItemsSource =
				productNames;
		}

		private void ProductAutoSuggestBox_TextChanged(
			AutoSuggestBox sender,
			AutoSuggestBoxTextChangedEventArgs args)
		{
			if (args.Reason !=
				AutoSuggestionBoxTextChangeReason.UserInput)
			{
				return;
			}

			string search =
				sender.Text.Trim();

			sender.ItemsSource =
				string.IsNullOrWhiteSpace(search)
					? productNames
					: productNames
						.Where(
							x => x.Contains(
								search,
								StringComparison.OrdinalIgnoreCase))
						.ToList();
		}

		private void ProductAutoSuggestBox_SuggestionChosen(
			AutoSuggestBox sender,
			AutoSuggestBoxSuggestionChosenEventArgs args)
		{
			if (args.SelectedItem == null)
				return;

			string productName =
				args.SelectedItem.ToString()
				?? string.Empty;

			sender.Text =
				productName;

			LoadCurrentCostPrice(
				productName);

			UpdateItemPreview();
		}

		// ============================================================
		// CURRENT COST PRICE
		// ============================================================

		private void LoadCurrentCostPrice(
			string productName)
		{
			const string query = @"
                SELECT CostPrice
                FROM Product
                WHERE ProductName = @ProductName";

			object? result =
				DatabaseHelper.ExecuteScalar(
					query,
					new Dictionary<string, object>
					{
						["@ProductName"] =
							productName
					});

			if (result != null &&
				result != DBNull.Value)
			{
				CostPriceTextBox.Text =
					Convert.ToDecimal(result)
						.ToString("0.00");
			}
		}

		// ============================================================
		// VAT STATUS
		// ============================================================

		private bool GetProductVatStatus(
			string productName)
		{
			if (string.IsNullOrWhiteSpace(productName))
				return false;

			const string query = @"
                SELECT IsVattable
                FROM Product
                WHERE ProductName = @ProductName";

			object? result =
				DatabaseHelper.ExecuteScalar(
					query,
					new Dictionary<string, object>
					{
						["@ProductName"] =
							productName
					});

			if (result == null ||
				result == DBNull.Value)
			{
				return false;
			}

			return Convert.ToInt32(result) == 1;
		}

		// ============================================================
		// ITEM PREVIEW
		// ============================================================

		private void ItemInput_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			UpdateItemPreview();
		}

		private void UpdateItemPreview()
		{
			if (!long.TryParse(
					QuantityTextBox.Text,
					out long quantity) ||
				quantity <= 0)
			{
				ItemSubtotalTextBlock.Text =
					"Subtotal: 0.00";

				ItemVATTextBlock.Text =
					"VAT: 0.00";

				return;
			}

			if (!decimal.TryParse(
					CostPriceTextBox.Text,
					out decimal costPrice) ||
				costPrice < 0)
			{
				ItemSubtotalTextBlock.Text =
					"Subtotal: 0.00";

				ItemVATTextBlock.Text =
					"VAT: 0.00";

				return;
			}

			decimal subtotal =
				quantity * costPrice;

			bool isVattable =
				GetProductVatStatus(
					ProductAutoSuggestBox.Text.Trim());

			decimal vat =
				TaxCalculator.VatOnAmount(
					subtotal,
					isVattable);

			ItemSubtotalTextBlock.Text =
				$"Subtotal: {subtotal:N2}";

			ItemVATTextBlock.Text =
				$"VAT: {vat:N2}";
		}

		// ============================================================
		// ADD ITEM
		// ============================================================

		private async void AddItemButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			string productName =
				ProductAutoSuggestBox.Text.Trim();

			if (string.IsNullOrWhiteSpace(productName))
			{
				await ShowMessageAsync(
					"Please select a product.",
					"Validation");

				return;
			}

			long productId =
				DatabaseHelper.GetProductId(
					productName);

			if (productId <= 0)
			{
				await ShowMessageAsync(
					"The selected product was not found.",
					"Validation");

				return;
			}

			if (!long.TryParse(
					QuantityTextBox.Text,
					out long quantity) ||
				quantity <= 0)
			{
				await ShowMessageAsync(
					"Please enter a valid quantity.",
					"Validation");

				return;
			}

			if (!decimal.TryParse(
					CostPriceTextBox.Text,
					out decimal costPrice) ||
				costPrice < 0)
			{
				await ShowMessageAsync(
					"Please enter a valid cost price.",
					"Validation");

				return;
			}

			bool isVattable =
				GetProductVatStatus(
					productName);

			var item =
				new PurchaseEntryItem
				{
					ProductID = productId,
					ProductName = productName,
					Quantity = quantity,
					CostPrice = costPrice,
					IsVattable = isVattable
				};

			items.Add(item);

			ClearItemFields();

			UpdateTotals();
		}

		private void ClearItemFields()
		{
			ProductAutoSuggestBox.Text =
				string.Empty;

			QuantityTextBox.Text =
				string.Empty;

			CostPriceTextBox.Text =
				string.Empty;

			ItemSubtotalTextBlock.Text =
				"Subtotal: 0.00";

			ItemVATTextBlock.Text =
				"VAT: 0.00";
		}

		// ============================================================
		// DISCOUNT
		// ============================================================

		private void DiscountTextBox_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			UpdateTotals();
		}

		private decimal GetDiscountPercent()
		{
			if (!decimal.TryParse(
					DiscountTextBox.Text,
					out decimal discount))
			{
				return 0m;
			}

			if (discount < 0)
				return 0m;

			if (discount > 100)
				return 100m;

			return discount;
		}

		// ============================================================
		// TOTALS
		// ============================================================

		private void UpdateTotals()
		{
			decimal subtotal =
				items.Sum(
					x => x.Subtotal);

			decimal discountPercent =
				GetDiscountPercent();

			decimal discountAmount =
				subtotal *
				discountPercent /
				100m;

			decimal vat =
				CalculateVatAfterDiscount(
					subtotal,
					discountAmount);

			decimal grandTotal =
				subtotal -
				discountAmount +
				vat;

			SubtotalTextBlock.Text =
				$"Subtotal: Rs. {subtotal:N2}";

			DiscountAmountTextBlock.Text =
				$"Discount: Rs. {discountAmount:N2}";

			VATTotalTextBlock.Text =
				$"VAT: Rs. {vat:N2}";

			GrandTotalTextBlock.Text =
				$"Grand Total: Rs. {grandTotal:N2}";
		}


		// ============================================================
		// VAT AFTER DISCOUNT
		//
		// VAT is charged on the amount remaining after the bill
		// discount is allocated to each item, matching the sales
		// side and Nepali VAT rules. The item.VAT property shows
		// the undiscounted per-item preview only.
		// ============================================================

		private decimal CalculateVatAfterDiscount(
			decimal subtotal,
			decimal totalDiscount)
		{
			return
				TaxCalculator.BillVat(
					items.Select(
						x => (x.Subtotal, x.IsVattable)),
					subtotal,
					totalDiscount);
		}

		// ============================================================
		// SAVE PURCHASE
		// ============================================================

		private async void SavePurchaseButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(
					CompanyAutoSuggestBox.Text))
			{
				await ShowMessageAsync(
					"Please select a company.",
					"Validation");

				return;
			}

			if (string.IsNullOrWhiteSpace(
					BillNumberTextBox.Text))
			{
				await ShowMessageAsync(
					"Please enter the bill number.",
					"Validation");

				return;
			}

			if (items.Count == 0)
			{
				await ShowMessageAsync(
					"Please add at least one item.",
					"Validation");

				return;
			}

			int companyId =
				DatabaseHelper.GetCompanyIdByName(
					CompanyAutoSuggestBox.Text.Trim());

			if (companyId <= 0)
			{
				await ShowMessageAsync(
					"The selected company was not found.",
					"Validation");

				return;
			}

			// ------------------------------------------------------------
			// Duplicate bill number within the same company + fiscal year.
			// ------------------------------------------------------------

			string? purchaseFiscalYear =
				DatabaseHelper.FiscalYearOf(
					PurchaseDatePicker.SelectedBSDate);

			object? duplicateCount =
				DatabaseHelper.ExecuteScalar(
					@"
	                    SELECT COUNT(*)
	                    FROM Purchase
	                    WHERE
	                        CompanyID = @CompanyID
	                        AND BillNumber = @BillNumber
	                        AND FiscalYear = @FiscalYear
	                    ",
					new Dictionary<string, object>
					{
						["@CompanyID"] = companyId,

						["@BillNumber"] =
							$"INV {BillNumberTextBox.Text.Trim()}",

						["@FiscalYear"] =
							purchaseFiscalYear ?? (object)DBNull.Value
					});

			if (duplicateCount != null &&
				Convert.ToInt32(duplicateCount) > 0)
			{
				await ShowMessageAsync(
					$"A purchase bill numbered INV {BillNumberTextBox.Text.Trim()} " +
					$"already exists for this company in fiscal year {purchaseFiscalYear}.\n" +
					"Please use a different bill number.",
					"Duplicate Bill");

				return;
			}

			decimal discountPercent =
				GetDiscountPercent();

			decimal subtotal =
				items.Sum(
					x => x.Subtotal);

			decimal discountAmount =
				subtotal *
				discountPercent /
				100m;

			try
			{
				string adDate =
					PurchaseDatePicker.SelectedADDate
						.ToString("yyyy-MM-dd");

				string nepaliDate =
					PurchaseDatePicker.SelectedBSDate;

				string billNumber =
					$"INV {BillNumberTextBox.Text.Trim()}";

				using var conn =
					DatabaseHelper.GetConnection();

				conn.Open();

				using var transaction =
					conn.BeginTransaction();

				try
				{
					foreach (PurchaseEntryItem item in items)
					{
						decimal fullTotal =
							item.Subtotal;

						// Same proportional discount
						// logic as the existing WinForms
						// implementation.

						decimal itemDiscount =
							TaxCalculator.ItemDiscount(
								fullTotal,
								subtotal,
								discountAmount);

						decimal totalAfterDiscount =
							TaxCalculator.ItemAfterDiscount(
								fullTotal,
								subtotal,
								discountAmount);

						decimal vat =
							TaxCalculator.VatOnAmount(
								totalAfterDiscount,
								item.IsVattable);

						decimal totalWithVat =
							totalAfterDiscount +
							vat;

						// ------------------------------------------------
						// INSERT PURCHASE
						// ------------------------------------------------

						const string insertQuery = @"
	                        INSERT INTO Purchase
	                        (
	                            ProductID,
	                            CompanyID,
	                            Quantity,
	                            CostPrice,
	                            PurchaseDate,
	                            TotalPurchasePrice,
	                            TotalPurchaseAfterDiscount,
	                            BillNumber,
	                            NepaliDate,
	                            FiscalYear,
	                            Discount,
	                            VATAmount,
	                            TotalAmountWithVat,
	                            DPercentage
	                        )
	                        VALUES
	                        (
	                            @ProductID,
	                            @CompanyID,
	                            @Quantity,
	                            @CostPrice,
	                            @PurchaseDate,
	                            @TotalPurchasePrice,
	                            @TotalPurchaseAfterDiscount,
	                            @BillNumber,
	                            @NepaliDate,
	                            @FiscalYear,
	                            @Discount,
	                            @VATAmount,
	                            @TotalAmountWithVat,
	                            @DPercentage
	                        );
	                        SELECT last_insert_rowid();";

						long purchaseId;

						using (var insertCmd =
							new System.Data.SQLite.SQLiteCommand(
								insertQuery,
								conn,
								transaction))
						{
							insertCmd.Parameters.AddWithValue("@ProductID", item.ProductID);
							insertCmd.Parameters.AddWithValue("@CompanyID", companyId);
							insertCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
							insertCmd.Parameters.AddWithValue("@CostPrice", item.CostPrice);
							insertCmd.Parameters.AddWithValue("@PurchaseDate", adDate);
							insertCmd.Parameters.AddWithValue("@TotalPurchasePrice", fullTotal);
							insertCmd.Parameters.AddWithValue("@TotalPurchaseAfterDiscount", totalAfterDiscount);
							insertCmd.Parameters.AddWithValue("@BillNumber", billNumber);
							insertCmd.Parameters.AddWithValue("@NepaliDate", nepaliDate);
							insertCmd.Parameters.AddWithValue("@FiscalYear", DatabaseHelper.FiscalYearOf(nepaliDate) ?? (object)DBNull.Value);
							insertCmd.Parameters.AddWithValue("@Discount", itemDiscount);
							insertCmd.Parameters.AddWithValue("@VATAmount", vat);
							insertCmd.Parameters.AddWithValue("@TotalAmountWithVat", totalWithVat);
							insertCmd.Parameters.AddWithValue("@DPercentage", discountPercent);

							purchaseId =
								Convert.ToInt64(
									insertCmd.ExecuteScalar());
						}

						// ------------------------------------------------
						// COST PRICE HISTORY
						// ------------------------------------------------

						const string lastCostQuery = @"
	                        SELECT CostPrice
	                        FROM CostPriceHistory
	                        WHERE ProductID = @ProductID
	                        ORDER BY EffectiveDate DESC
	                        LIMIT 1";

						object? lastCost;

						using (var lastCostCmd =
							new System.Data.SQLite.SQLiteCommand(
								lastCostQuery,
								conn,
								transaction))
						{
							lastCostCmd.Parameters.AddWithValue(
								"@ProductID",
								item.ProductID);

							lastCost =
								lastCostCmd.ExecuteScalar();
						}

						bool priceChanged =
							lastCost == null ||
							lastCost == DBNull.Value ||
							decimal.Round(
								Convert.ToDecimal(lastCost),
								2) !=
							decimal.Round(
								item.CostPrice,
								2);

						if (priceChanged)
						{
							const string historyQuery = @"
	                            INSERT INTO CostPriceHistory
	                            (
	                                ProductID,
	                                CostPrice,
	                                EffectiveDate,
	                                ReferencePurchaseID
	                            )
	                            VALUES
	                            (
	                                @ProductID,
	                                @CostPrice,
	                                CURRENT_DATE,
	                                @PurchaseID
	                            )";

							using var historyCmd =
								new System.Data.SQLite.SQLiteCommand(
									historyQuery,
									conn,
									transaction);

							historyCmd.Parameters.AddWithValue("@ProductID", item.ProductID);
							historyCmd.Parameters.AddWithValue("@CostPrice", item.CostPrice);
							historyCmd.Parameters.AddWithValue("@PurchaseID", purchaseId);

							historyCmd.ExecuteNonQuery();
						}

						// ------------------------------------------------
						// INVENTORY
						// ------------------------------------------------

						const string inventoryQuery = @"
	                        UPDATE Inventory
	                        SET
	                            TotalStockLeft =
	                                TotalStockLeft + @Quantity,

	                            Quantity =
	                                Quantity + @Quantity

	                        WHERE ProductID = @ProductID";

						int inventoryAffected;

						using (var inventoryCmd =
							new System.Data.SQLite.SQLiteCommand(
								inventoryQuery,
								conn,
								transaction))
						{
							inventoryCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
							inventoryCmd.Parameters.AddWithValue("@ProductID", item.ProductID);

							inventoryAffected =
								inventoryCmd.ExecuteNonQuery();
						}

						if (inventoryAffected == 0)
						{
							throw new InvalidOperationException(
								$"No inventory record exists for {item.ProductName}. " +
								"The item was not added to stock.");
						}

						// ------------------------------------------------
						// CURRENT PRODUCT COST
						// ------------------------------------------------

						const string productCostQuery = @"
	                        UPDATE Product
	                        SET CostPrice = @CostPrice
	                        WHERE ProductID = @ProductID";

						using var productCostCmd =
							new System.Data.SQLite.SQLiteCommand(
								productCostQuery,
								conn,
								transaction);

						productCostCmd.Parameters.AddWithValue("@CostPrice", item.CostPrice);
						productCostCmd.Parameters.AddWithValue("@ProductID", item.ProductID);

						productCostCmd.ExecuteNonQuery();
					}

					transaction.Commit();
				}
				catch
				{
					transaction.Rollback();
					throw;
				}

				await ShowMessageAsync(
					"Purchase saved successfully.",
					"Success");

				if (Frame.CanGoBack)
				{
					Frame.GoBack();
				}
			}
			catch (Exception ex)
			{
				string reason =
					ex.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase)
						? $"Bill number INV {BillNumberTextBox.Text.Trim()} already exists " +
						  $"for this company in fiscal year {DatabaseHelper.FiscalYearOf(PurchaseDatePicker.SelectedBSDate)}.\n" +
						  "Please use a different bill number.\n\n"
						: string.Empty;

				await ShowMessageAsync(
					"Unable to save purchase.\n\n" +
					reason +
					ex.Message,
					"Purchase Error");
			}
		}

		// ============================================================
		// CANCEL
		// ============================================================

		private void CancelButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (Frame.CanGoBack)
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