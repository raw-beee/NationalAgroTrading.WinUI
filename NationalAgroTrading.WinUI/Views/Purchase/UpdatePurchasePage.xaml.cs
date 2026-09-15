using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NationalAgroTrading.WinUI.Data;
using NationalAgroTrading.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NationalAgroTrading.WinUI.Controls;

namespace NationalAgroTrading.WinUI.Views.Purchase
{
	public sealed partial class UpdatePurchasePage : Page
	{
		private const decimal VatRate = TaxCalculator.VatRate;

		private readonly ObservableCollection<EditablePurchaseItem> items =
			new();

		private List<string> productNames = new();

		private PurchaseBillItem? bill;

		private decimal discountPercent;

		private bool loadingDiscount;


		// ============================================================
		// CONSTRUCTOR
		// ============================================================

		public UpdatePurchasePage()
		{
			InitializeComponent();

			PurchaseItemsListView.ItemsSource =
				items;

			LoadProducts();
		}


		// ============================================================
		// NAVIGATION
		// ============================================================

		protected override void OnNavigatedTo(
			NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			if (e.Parameter is not PurchaseBillItem selectedBill)
				return;

			bill =
				selectedBill;

			BillTextBlock.Text =
				$"Bill: {selectedBill.BillNumber}";

			CompanyTextBlock.Text =
				$"Company: {selectedBill.CompanyName}";

			LoadExistingPurchaseDate();

			LoadPurchaseItems();
		}


		// ============================================================
		// PRODUCTS
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

			UpdateNewItemPreview();
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
				NewCostPriceTextBox.Text =
					Convert.ToDecimal(result)
						.ToString("0.00");
			}
		}


		// ============================================================
		// VAT STATUS
		// ============================================================

		private bool GetProductVatStatus(
			long productId)
		{
			const string query = @"
                SELECT IsVattable
                FROM Product
                WHERE ProductID = @ProductID";

			object? result =
				DatabaseHelper.ExecuteScalar(
					query,
					new Dictionary<string, object>
					{
						["@ProductID"] =
							productId
					});

			if (result == null ||
				result == DBNull.Value)
			{
				return false;
			}

			return Convert.ToInt32(result) == 1;
		}


		// ============================================================
		// NEW ITEM PREVIEW
		// ============================================================

		private void NewItemInput_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			UpdateNewItemPreview();
		}


		private void UpdateNewItemPreview()
		{
			if (!long.TryParse(
					NewQuantityTextBox.Text,
					out long quantity) ||
				quantity <= 0)
			{
				NewItemSubtotalTextBlock.Text =
					"Subtotal: 0.00";

				NewItemVATTextBlock.Text =
					"VAT: 0.00";

				return;
			}

			if (!decimal.TryParse(
					NewCostPriceTextBox.Text,
					out decimal costPrice) ||
				costPrice < 0)
			{
				NewItemSubtotalTextBlock.Text =
					"Subtotal: 0.00";

				NewItemVATTextBlock.Text =
					"VAT: 0.00";

				return;
			}

			string productName =
				ProductAutoSuggestBox.Text.Trim();

			long productId =
				DatabaseHelper.GetProductId(
					productName);

			if (productId <= 0)
			{
				NewItemSubtotalTextBlock.Text =
					"Subtotal: 0.00";

				NewItemVATTextBlock.Text =
					"VAT: 0.00";

				return;
			}

			decimal subtotal =
				quantity * costPrice;

			bool isVattable =
				GetProductVatStatus(
					productId);

			decimal vat =
				TaxCalculator.VatOnAmount(
					subtotal,
					isVattable);

			NewItemSubtotalTextBlock.Text =
				$"Subtotal: {subtotal:N2}";

			NewItemVATTextBlock.Text =
				$"VAT: {vat:N2}";
		}


		// ============================================================
		// LOAD PURCHASE ITEMS
		// ============================================================

		private void LoadPurchaseItems()
		{
			if (bill == null)
				return;

			/*
             * IMPORTANT:
             *
             * PurchaseDate and NepaliDate are intentionally NOT
             * selected here.
             *
             * Old database records can contain values such as:
             *
             * 6/8/2025 12:00:00 AM
             *
             * Selecting those fields into a DataTable can make the
             * SQLite provider attempt an automatic DateTime
             * conversion and throw FormatException.
             */

			const string query = @"
                SELECT
                    p.PurchaseID,
                    p.ProductID,
                    pr.ProductName,
                    p.Quantity,
                    p.CostPrice,
                    p.Discount,
                    p.VATAmount,
                    p.TotalPurchasePrice,
                    p.TotalPurchaseAfterDiscount,
                    p.TotalAmountWithVat,
                    p.DPercentage
                FROM Purchase p
                INNER JOIN Product pr
                    ON p.ProductID = pr.ProductID
                WHERE
                    p.CompanyID = @CompanyID
                    AND p.BillNumber = @BillNumber
                ORDER BY
                    p.PurchaseID ASC";

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

			items.Clear();

			discountPercent =
				0m;

			bool discountFound =
				false;

			foreach (DataRow row in table.Rows)
			{
				long productId =
					Convert.ToInt64(
						row["ProductID"]);

				decimal rowDiscountPercent =
					ToDecimal(
						row["DPercentage"]);

				if (!discountFound)
				{
					discountPercent =
						rowDiscountPercent;

					discountFound =
						true;
				}

				bool isVattable =
					GetProductVatStatus(
						productId);

				var item =
					new EditablePurchaseItem
					{
						PurchaseID =
							Convert.ToInt64(
								row["PurchaseID"]),

						ProductID =
							productId,

						ProductName =
							row["ProductName"]?.ToString()
							?? string.Empty,

						Quantity =
							Convert.ToInt64(
								row["Quantity"]),

						CostPrice =
							ToDecimal(
								row["CostPrice"]),

						OriginalQuantity =
							Convert.ToInt64(
								row["Quantity"]),

						OriginalCostPrice =
							ToDecimal(
								row["CostPrice"]),

						OriginalDiscount =
							ToDecimal(
								row["Discount"]),

						OriginalVAT =
							ToDecimal(
								row["VATAmount"]),

						OriginalTotal =
							ToDecimal(
								row["TotalAmountWithVat"]),

						DiscountPercent =
							rowDiscountPercent,

						IsVattable =
							isVattable,

						IsNew =
							false
					};

				items.Add(item);
			}

			loadingDiscount =
				true;

			DiscountPercentTextBox.Text =
				discountPercent.ToString(
					"0.##",
					CultureInfo.InvariantCulture);

			loadingDiscount =
				false;

			UpdateTotals();
		}


		// ============================================================
		// DISCOUNT
		// ============================================================

		private void DiscountPercentTextBox_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			if (loadingDiscount)
				return;

			if (!decimal.TryParse(
					DiscountPercentTextBox.Text,
					out decimal percentage))
			{
				discountPercent =
					0m;

				UpdateTotals();

				return;
			}

			if (percentage < 0m)
				percentage = 0m;

			if (percentage > 100m)
				percentage = 100m;

			discountPercent =
				percentage;

			UpdateTotals();
		}


		// ============================================================
		// ADD NEW ITEM
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
					NewQuantityTextBox.Text,
					out long quantity) ||
				quantity <= 0)
			{
				await ShowMessageAsync(
					"Please enter a quantity greater than zero.",
					"Validation");

				return;
			}

			if (!decimal.TryParse(
					NewCostPriceTextBox.Text,
					out decimal costPrice) ||
				costPrice < 0)
			{
				await ShowMessageAsync(
					"Please enter a valid cost price.",
					"Validation");

				return;
			}

			EditablePurchaseItem? existing =
				items.FirstOrDefault(
					x => x.ProductID == productId);

			if (existing != null)
			{
				await ShowMessageAsync(
					$"{productName} is already in this purchase.\n\n" +
					"Edit the existing row instead of adding it again.",
					"Product Already Exists");

				return;
			}

			bool isVattable =
				GetProductVatStatus(
					productId);

			var item =
				new EditablePurchaseItem
				{
					PurchaseID =
						0,

					ProductID =
						productId,

					ProductName =
						productName,

					Quantity =
						quantity,

					CostPrice =
						costPrice,

					OriginalQuantity =
						0,

					OriginalCostPrice =
						0m,

					OriginalDiscount =
						0m,

					OriginalVAT =
						0m,

					OriginalTotal =
						0m,

					DiscountPercent =
						discountPercent,

					IsVattable =
						isVattable,

					IsNew =
						true
				};

			items.Add(item);

			ClearNewItemFields();

			UpdateTotals();
		}


		// ============================================================
		// REMOVE ITEM
		// ============================================================

		private async void RemoveItemButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (sender is not Button button ||
				button.Tag is not EditablePurchaseItem item)
			{
				return;
			}

			if (XamlRoot == null)
				return;

			var dialog =
				new ContentDialog
				{
					Title =
						"Remove Item",

					Content =
						$"Remove '{item.ProductName}' from this purchase?\n\n" +
						"When saved, the original quantity will be removed from inventory.",

					PrimaryButtonText =
						"Remove",

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

			items.Remove(item);

			UpdateTotals();
		}


		// ============================================================
		// CLEAR NEW ITEM FIELDS
		// ============================================================

		private void ClearNewItemFields()
		{
			ProductAutoSuggestBox.Text =
				string.Empty;

			NewQuantityTextBox.Text =
				string.Empty;

			NewCostPriceTextBox.Text =
				string.Empty;

			NewItemSubtotalTextBlock.Text =
				"Subtotal: 0.00";

			NewItemVATTextBlock.Text =
				"VAT: 0.00";
		}


		// ============================================================
		// TOTALS
		// ============================================================

		private void UpdateTotals()
		{
			decimal subtotal =
				items.Sum(
					x => x.Subtotal);

			decimal discountAmount =
				subtotal *
				discountPercent /
				100m;

			decimal afterDiscount =
				subtotal -
				discountAmount;

			decimal vat =
				items.Sum(
					x => x.VAT);

			decimal grandTotal =
				afterDiscount +
				vat;

			SubtotalTextBlock.Text =
				$"Subtotal: Rs. {subtotal:N2}";

			DiscountTextBlock.Text =
				$"Discount ({discountPercent:0.##}%): Rs. {discountAmount:N2}";

			AfterDiscountTextBlock.Text =
				$"After Discount: Rs. {afterDiscount:N2}";

			VATTextBlock.Text =
				$"VAT: Rs. {vat:N2}";

			GrandTotalTextBlock.Text =
				$"Grand Total: Rs. {grandTotal:N2}";
		}


		// ============================================================
		// SAVE BUTTON
		// ============================================================

		private async void SaveChangesButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (bill == null)
				return;

			List<EditablePurchaseItem> zeroQuantityItems =
				items
					.Where(
						x => x.Quantity <= 0)
					.ToList();

			foreach (EditablePurchaseItem item
					 in zeroQuantityItems)
			{
				items.Remove(item);
			}

			if (items.Count == 0)
			{
				await ShowMessageAsync(
					"A purchase must contain at least one item.",
					"Validation");

				return;
			}

			if (discountPercent < 0m ||
				discountPercent > 100m)
			{
				await ShowMessageAsync(
					"Discount percentage must be between 0 and 100.",
					"Validation");

				return;
			}

			foreach (EditablePurchaseItem item in items)
			{
				if (item.Quantity <= 0)
				{
					await ShowMessageAsync(
						$"Quantity for '{item.ProductName}' must be greater than zero.",
						"Validation");

					return;
				}

				if (item.CostPrice < 0m)
				{
					await ShowMessageAsync(
						$"Cost price for '{item.ProductName}' cannot be negative.",
						"Validation");

					return;
				}
			}

			/*
             * Validate date BEFORE confirmation.
             */

			if (!PurchaseDatePicker.TryGetSelectedDate(
					out _,
					out _))
			{
				await ShowMessageAsync(
					"Please enter a valid Nepali date in YYYY-MM-DD format.",
					"Invalid Date");

				return;
			}

			var confirmation =
				new ContentDialog
				{
					Title =
						"Save Purchase Changes",

					Content =
						"The purchase items, discount, VAT, cost prices, date and inventory will be updated.\n\n" +
						"Do you want to continue?",

					PrimaryButtonText =
						"Save Changes",

					CloseButtonText =
						"Cancel",

					DefaultButton =
						ContentDialogButton.Primary,

					XamlRoot =
						XamlRoot
				};

			ContentDialogResult result =
				await confirmation.ShowAsync();

			if (result !=
				ContentDialogResult.Primary)
			{
				return;
			}

			try
			{
				SavePurchaseChanges();

				await ShowMessageAsync(
					"Purchase updated successfully.",
					"Success");

				if (Frame.CanGoBack)
					Frame.GoBack();
			}
			catch (Exception ex)
			{
				string reason =
					ex.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase)
						? "A bill with this number already exists for this company " +
						  $"in fiscal year {DatabaseHelper.FiscalYearOf(PurchaseDatePicker.SelectedBSDate)}.\n" +
						  "Please use a different bill number.\n\n"
						: string.Empty;

				await ShowMessageAsync(
					"Unable to update purchase.\n\n" +
					reason +
					ex.Message,
					"Purchase Error");
			}
		}


		// ============================================================
		// SAVE PURCHASE CHANGES
		// ============================================================

		private void SavePurchaseChanges()
		{
			if (bill == null)
			{
				throw new InvalidOperationException(
					"No purchase bill selected.");
			}

			/*
             * Get date from editable Nepali date picker.
             */

			if (!PurchaseDatePicker.TryGetSelectedDate(
					out DateTime purchaseAdDate,
					out string nepaliDate))
			{
				throw new InvalidOperationException(
					"Please enter a valid Nepali date in YYYY-MM-DD format.");
			}

			string purchaseDate =
				purchaseAdDate.ToString(
					"yyyy-MM-dd",
					CultureInfo.InvariantCulture);


			// ========================================================
			// ORIGINAL DATABASE ROWS
			// ========================================================

			const string originalQuery = @"
                SELECT
                    PurchaseID,
                    ProductID,
                    Quantity,
                    CostPrice,
                    Discount,
                    VATAmount,
                    TotalPurchasePrice,
                    TotalPurchaseAfterDiscount,
                    TotalAmountWithVat,
                    DPercentage
                FROM Purchase
                WHERE
                    CompanyID = @CompanyID
                    AND BillNumber = @BillNumber";

			DataTable originalRows =
				DatabaseHelper.GetData(
					originalQuery,
					new Dictionary<string, object>
					{
						["@CompanyID"] =
							bill.CompanyID,

						["@BillNumber"] =
							bill.BillNumber
					});


			HashSet<long> currentPurchaseIds =
				items
					.Where(
						x => !x.IsNew)
					.Select(
						x => x.PurchaseID)
					.ToHashSet();


			HashSet<long> affectedProductIds =
				new();


			// ========================================================
			// ONE CONNECTION / ONE TRANSACTION FOR EVERY WRITE
			// ========================================================

			using var conn =
				DatabaseHelper.GetConnection();

			conn.Open();

			using var transaction =
				conn.BeginTransaction();

			try
			{

			// ========================================================
			// DELETE REMOVED ITEMS
			// ========================================================

			foreach (DataRow row
					 in originalRows.Rows)
			{
				long purchaseId =
					Convert.ToInt64(
						row["PurchaseID"]);

				long productId =
					Convert.ToInt64(
						row["ProductID"]);

				long oldQuantity =
					Convert.ToInt64(
						row["Quantity"]);

				if (currentPurchaseIds.Contains(
						purchaseId))
				{
					continue;
				}

				affectedProductIds.Add(
					productId);


				const string deleteHistoryQuery = @"
                    DELETE FROM CostPriceHistory
                    WHERE ReferencePurchaseID = @PurchaseID";

				Exec(
					conn,
					transaction,
					deleteHistoryQuery,
					new Dictionary<string, object>
					{
						["@PurchaseID"] =
							purchaseId
					});


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
						["@Quantity"] =
							oldQuantity,

						["@ProductID"] =
							productId
					});


				const string deletePurchaseQuery = @"
                    DELETE FROM Purchase
                    WHERE PurchaseID = @PurchaseID";

				Exec(
					conn,
					transaction,
					deletePurchaseQuery,
					new Dictionary<string, object>
					{
						["@PurchaseID"] =
							purchaseId
					});
			}


			// ========================================================
			// UPDATE EXISTING ITEMS
			// ========================================================

			foreach (EditablePurchaseItem item in items)
			{
				if (item.IsNew)
					continue;

				affectedProductIds.Add(
					item.ProductID);

				long quantityDifference =
					item.Quantity -
					item.OriginalQuantity;

				decimal subtotal =
					item.Subtotal;

				decimal discountAmount =
					subtotal *
					discountPercent /
					100m;

				decimal afterDiscount =
					subtotal -
					discountAmount;

				decimal vat =
					item.VAT;

				decimal totalWithVat =
					afterDiscount +
					vat;


				const string updatePurchaseQuery = @"
                    UPDATE Purchase
                    SET
                        Quantity = @Quantity,
                        CostPrice = @CostPrice,
                        PurchaseDate = @PurchaseDate,
                        NepaliDate = @NepaliDate,
                        FiscalYear = @FiscalYear,
                        TotalPurchasePrice = @TotalPurchasePrice,
                        TotalPurchaseAfterDiscount = @TotalPurchaseAfterDiscount,
                        Discount = @Discount,
                        VATAmount = @VATAmount,
                        TotalAmountWithVat = @TotalAmountWithVat,
                        DPercentage = @DPercentage
                    WHERE PurchaseID = @PurchaseID";


				Exec(
					conn,
					transaction,
					updatePurchaseQuery,
					new Dictionary<string, object>
					{
						["@Quantity"] =
							item.Quantity,

						["@CostPrice"] =
							item.CostPrice,

						["@PurchaseDate"] =
							purchaseDate,

						["@NepaliDate"] =
							nepaliDate,

						["@FiscalYear"] =
							DatabaseHelper.FiscalYearOf(nepaliDate) ?? (object)DBNull.Value,

						["@TotalPurchasePrice"] =
							subtotal,

						["@TotalPurchaseAfterDiscount"] =
							afterDiscount,

						["@Discount"] =
							discountAmount,

						["@VATAmount"] =
							vat,

						["@TotalAmountWithVat"] =
							totalWithVat,

						["@DPercentage"] =
							discountPercent,

						["@PurchaseID"] =
							item.PurchaseID
					});


				// ----------------------------------------------------
				// INVENTORY DIFFERENCE
				// ----------------------------------------------------

				if (quantityDifference != 0)
				{
					const string inventoryQuery = @"
                        UPDATE Inventory
                        SET
                            TotalStockLeft =
                                TotalStockLeft + @Difference,

                            Quantity =
                                Quantity + @Difference
                        WHERE ProductID = @ProductID";

					Exec(
						conn,
						transaction,
						inventoryQuery,
						new Dictionary<string, object>
						{
							["@Difference"] =
								quantityDifference,

							["@ProductID"] =
								item.ProductID
						});
				}


				// ----------------------------------------------------
				// COST HISTORY
				// ----------------------------------------------------

				if (item.CostPrice !=
					item.OriginalCostPrice)
				{
					UpdateCostPriceHistory(
						item,
						conn,
						transaction);
				}
			}


			// ========================================================
			// INSERT NEW ITEMS
			// ========================================================

			foreach (EditablePurchaseItem item in items)
			{
				if (!item.IsNew)
					continue;

				affectedProductIds.Add(
					item.ProductID);

				decimal subtotal =
					item.Subtotal;

				decimal discountAmount =
					subtotal *
					discountPercent /
					100m;

				decimal afterDiscount =
					subtotal -
					discountAmount;

				decimal vat =
					item.VAT;

				decimal totalWithVat =
					afterDiscount +
					vat;


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
					new SQLiteCommand(
						insertQuery,
						conn,
						transaction))
				{
					insertCmd.Parameters.AddWithValue("@ProductID", item.ProductID);
					insertCmd.Parameters.AddWithValue("@CompanyID", bill.CompanyID);
					insertCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
					insertCmd.Parameters.AddWithValue("@CostPrice", item.CostPrice);
					insertCmd.Parameters.AddWithValue("@PurchaseDate", purchaseDate);
					insertCmd.Parameters.AddWithValue("@TotalPurchasePrice", subtotal);
					insertCmd.Parameters.AddWithValue("@TotalPurchaseAfterDiscount", afterDiscount);
					insertCmd.Parameters.AddWithValue("@BillNumber", bill.BillNumber);
					insertCmd.Parameters.AddWithValue("@NepaliDate", nepaliDate);
					insertCmd.Parameters.AddWithValue("@FiscalYear", DatabaseHelper.FiscalYearOf(nepaliDate) ?? (object)DBNull.Value);
					insertCmd.Parameters.AddWithValue("@Discount", discountAmount);
					insertCmd.Parameters.AddWithValue("@VATAmount", vat);
					insertCmd.Parameters.AddWithValue("@TotalAmountWithVat", totalWithVat);
					insertCmd.Parameters.AddWithValue("@DPercentage", discountPercent);

					purchaseId =
						Convert.ToInt64(
							insertCmd.ExecuteScalar());
				}


				// ----------------------------------------------------
				// INVENTORY
				// ----------------------------------------------------

				const string inventoryQuery = @"
                    UPDATE Inventory
                    SET
                        TotalStockLeft =
                            TotalStockLeft + @Quantity,

                        Quantity =
                            Quantity + @Quantity
                    WHERE ProductID = @ProductID";

				int inventoryAffected =
					Exec(
						conn,
						transaction,
						inventoryQuery,
						new Dictionary<string, object>
						{
							["@Quantity"] =
								item.Quantity,

							["@ProductID"] =
								item.ProductID
						});

				if (inventoryAffected == 0)
				{
					throw new InvalidOperationException(
						$"No inventory record exists for {item.ProductName}. " +
						"The item was not added to stock.");
				}


				InsertCostPriceHistoryIfRequired(
					item.ProductID,
					item.CostPrice,
					purchaseId,
					conn,
					transaction);
			}


			// ========================================================
			// RESTORE PRODUCT COST
			// ========================================================

			foreach (long productId
					 in affectedProductIds)
			{
				RestoreLatestProductCost(
					productId,
					conn,
					transaction);
			}

			transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}


		// ============================================================
		// COST PRICE HISTORY
		// ============================================================

		private void UpdateCostPriceHistory(
			EditablePurchaseItem item,
			SQLiteConnection conn,
			SQLiteTransaction transaction)
		{
			const string historyQuery = @"
                SELECT CostHistoryID
                FROM CostPriceHistory
                WHERE ReferencePurchaseID = @PurchaseID
                ORDER BY CostHistoryID DESC
                LIMIT 1";

			object? existing =
				Scalar(
					conn,
					transaction,
					historyQuery,
					new Dictionary<string, object>
					{
						["@PurchaseID"] =
							item.PurchaseID
					});


			if (existing != null &&
				existing != DBNull.Value)
			{
				const string updateQuery = @"
                    UPDATE CostPriceHistory
                    SET
                        CostPrice = @CostPrice,
                        EffectiveDate = CURRENT_TIMESTAMP
                    WHERE CostHistoryID = @CostHistoryID";

				Exec(
					conn,
					transaction,
					updateQuery,
					new Dictionary<string, object>
					{
						["@CostPrice"] =
							item.CostPrice,

						["@CostHistoryID"] =
							Convert.ToInt64(
								existing)
					});
			}
			else
			{
				InsertCostPriceHistoryIfRequired(
					item.ProductID,
					item.CostPrice,
					item.PurchaseID,
					conn,
					transaction);
			}
		}


		private void InsertCostPriceHistoryIfRequired(
			long productId,
			decimal costPrice,
			long purchaseId,
			SQLiteConnection conn,
			SQLiteTransaction transaction)
		{
			const string lastCostQuery = @"
                SELECT CostPrice
                FROM CostPriceHistory
                WHERE ProductID = @ProductID
                ORDER BY EffectiveDate DESC,
                         CostHistoryID DESC
                LIMIT 1";

			object? lastCost =
				Scalar(
					conn,
					transaction,
					lastCostQuery,
					new Dictionary<string, object>
					{
						["@ProductID"] =
							productId
					});


			bool shouldInsert =
				lastCost == null ||
				lastCost == DBNull.Value ||
				Convert.ToDecimal(
					lastCost) != costPrice;


			if (!shouldInsert)
				return;


			const string insertHistoryQuery = @"
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
                    CURRENT_TIMESTAMP,
                    @PurchaseID
                )";


			Exec(
				conn,
				transaction,
				insertHistoryQuery,
				new Dictionary<string, object>
				{
					["@ProductID"] =
						productId,

					["@CostPrice"] =
						costPrice,

					["@PurchaseID"] =
						purchaseId
				});
		}


		// ============================================================
		// RESTORE LATEST PRODUCT COST
		// ============================================================

		private void RestoreLatestProductCost(
			long productId,
			SQLiteConnection conn,
			SQLiteTransaction transaction)
		{
			/*
             * IMPORTANT:
             *
             * PurchaseDate is stored as text in yyyy-MM-dd format
             * by this page.
             *
             * Therefore SQLite can safely order this format
             * chronologically.
             */

			const string query = @"
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
					query,
					new Dictionary<string, object>
					{
						["@ProductID"] =
							productId
					});


			if (latestCost == null ||
				latestCost == DBNull.Value)
			{
				return;
			}


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
						Convert.ToDecimal(
							latestCost),

					["@ProductID"] =
						productId
				});
		}


		// ============================================================
		// LOAD EXISTING PURCHASE DATE
		// ============================================================

		private void LoadExistingPurchaseDate()
		{
			if (bill == null)
			{
				PurchaseDatePicker.SetADDate(
					DateTime.Today);

				return;
			}


			// --------------------------------------------------------
			// First try NepaliDate.
			// --------------------------------------------------------

			const string nepaliDateQuery = @"
                SELECT NepaliDate
                FROM Purchase
                WHERE
                    CompanyID = @CompanyID
                    AND BillNumber = @BillNumber
                ORDER BY PurchaseID ASC
                LIMIT 1";


			object? nepaliDateResult =
				DatabaseHelper.ExecuteScalar(
					nepaliDateQuery,
					new Dictionary<string, object>
					{
						["@CompanyID"] =
							bill.CompanyID,

						["@BillNumber"] =
							bill.BillNumber
					});


			string nepaliDate =
				nepaliDateResult?.ToString()
				?? string.Empty;


			if (TrySetNepaliDate(
					nepaliDate))
			{
				return;
			}


			// --------------------------------------------------------
			// Fallback to old AD PurchaseDate.
			// --------------------------------------------------------

			const string adDateQuery = @"
                SELECT PurchaseDate
                FROM Purchase
                WHERE
                    CompanyID = @CompanyID
                    AND BillNumber = @BillNumber
                ORDER BY PurchaseID ASC
                LIMIT 1";


			object? adDateResult =
				DatabaseHelper.ExecuteScalar(
					adDateQuery,
					new Dictionary<string, object>
					{
						["@CompanyID"] =
							bill.CompanyID,

						["@BillNumber"] =
							bill.BillNumber
					});


			string adDateText =
				adDateResult?.ToString()
				?? string.Empty;


			if (TryParseStoredAdDate(
					adDateText,
					out DateTime adDate))
			{
				PurchaseDatePicker.SetADDate(
					adDate);

				return;
			}


			// --------------------------------------------------------
			// Final fallback.
			// --------------------------------------------------------

			PurchaseDatePicker.SetADDate(
				DateTime.Today);
		}


		// ============================================================
		// SET NEPALI DATE
		// ============================================================

		private bool TrySetNepaliDate(
			string nepaliDate)
		{
			if (string.IsNullOrWhiteSpace(
					nepaliDate))
			{
				return false;
			}


			string[] parts =
				nepaliDate.Trim().Split(
					'-',
					StringSplitOptions.RemoveEmptyEntries);


			if (parts.Length != 3)
				return false;


			if (!int.TryParse(
					parts[0],
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out int year))
			{
				return false;
			}


			if (!int.TryParse(
					parts[1],
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out int month))
			{
				return false;
			}


			if (!int.TryParse(
					parts[2],
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out int day))
			{
				return false;
			}


			try
			{
				PurchaseDatePicker.SetBSDate(
					year,
					month,
					day);

				return PurchaseDatePicker.IsDateValid;
			}
			catch
			{
				return false;
			}
		}


		// ============================================================
		// PARSE OLD AD DATE
		// ============================================================

		private static bool TryParseStoredAdDate(
			string value,
			out DateTime date)
		{
			date =
				default;


			if (string.IsNullOrWhiteSpace(
					value))
			{
				return false;
			}


			string[] formats =
			{
				"yyyy-MM-dd",

				"M/d/yyyy h:mm:ss tt",

				"MM/dd/yyyy h:mm:ss tt",

				"M/d/yyyy",

				"MM/dd/yyyy"
			};


			if (DateTime.TryParseExact(
					value.Trim(),
					formats,
					CultureInfo.InvariantCulture,
					DateTimeStyles.AllowWhiteSpaces,
					out date))
			{
				date =
					date.Date;

				return true;
			}


			if (DateTime.TryParse(
					value.Trim(),
					CultureInfo.InvariantCulture,
					DateTimeStyles.AllowWhiteSpaces,
					out date))
			{
				date =
					date.Date;

				return true;
			}


			return false;
		}


		// ============================================================
		// NAVIGATION
		// ============================================================

		private void CancelButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (Frame.CanGoBack)
				Frame.GoBack();
		}


		private void BackButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (Frame.CanGoBack)
				Frame.GoBack();
		}


		// ============================================================
		// HELPERS
		// ============================================================

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


		private static decimal ToDecimal(
			object value)
		{
			if (value == null ||
				value == DBNull.Value)
			{
				return 0m;
			}

			return Convert.ToDecimal(
				value);
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
	}


	// =================================================================
	// EDITABLE PURCHASE ITEM
	// =================================================================

	public sealed class EditablePurchaseItem
	{
		public long PurchaseID { get; set; }

		public long ProductID { get; set; }

		public string ProductName { get; set; } =
			string.Empty;

		public long Quantity { get; set; }

		public decimal CostPrice { get; set; }

		public long OriginalQuantity { get; set; }

		public decimal OriginalCostPrice { get; set; }

		public decimal OriginalDiscount { get; set; }

		public decimal OriginalVAT { get; set; }

		public decimal OriginalTotal { get; set; }

		public decimal DiscountPercent { get; set; }

		public bool IsVattable { get; set; }

		public bool IsNew { get; set; }


		// ============================================================
		// SUBTOTAL
		// ============================================================

		public decimal Subtotal =>
			Quantity *
			CostPrice;


		// ============================================================
		// DISCOUNT
		// ============================================================

		public decimal Discount =>
			Subtotal *
			DiscountPercent /
			100m;


		// ============================================================
		// AFTER DISCOUNT
		// ============================================================

		public decimal AfterDiscount =>
			Subtotal -
			Discount;


		// ============================================================
		// VAT
		// ============================================================

		public decimal VAT =>
			TaxCalculator.VatOnAmount(
				AfterDiscount,
				IsVattable);


		// ============================================================
		// FINAL TOTAL
		// ============================================================

		public decimal Total =>
			AfterDiscount +
			VAT;


		// ============================================================
		// DISPLAY
		// ============================================================

		public string SubtotalText =>
			$"Rs. {Subtotal:N2}";


		public string VATText =>
			$"Rs. {VAT:N2}";


		public string TotalText =>
			$"Rs. {Total:N2}";
	}
}