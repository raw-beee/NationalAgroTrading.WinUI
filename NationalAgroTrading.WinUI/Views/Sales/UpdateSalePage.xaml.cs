using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NationalAgroTrading.WinUI.Data;
using NationalAgroTrading.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace NationalAgroTrading.WinUI.Views.Sales
{
	public sealed partial class UpdateSalePage : Page
	{
		private const decimal VatRate = TaxCalculator.VatRate;

		private readonly ObservableCollection<UpdateSaleItem> items =
			new();

		private SalesBillItem? bill;

		private bool loading;

		private bool saving;

		private DataTable? productTable;

		private long? selectedProductId;


		// ============================================================
		// CONSTRUCTOR
		// ============================================================

		public UpdateSalePage()
		{
			InitializeComponent();

			SaleItemsListView.ItemsSource =
				items;
		}


		// ============================================================
		// NAVIGATION
		// ============================================================

		protected override void OnNavigatedTo(
			NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			if (e.Parameter is not SalesBillItem selectedBill)
				return;

			bill =
				selectedBill;

			BillTextBlock.Text =
				$"Bill: {selectedBill.BillNumber}";

			CompanyTextBlock.Text =
				$"Company: {selectedBill.CompanyName}";

			BillNumberTextBlock.Text =
				selectedBill.BillNumber;

			/*
			 * IMPORTANT:
			 *
			 * Sale date is DISPLAY ONLY.
			 *
			 * We deliberately do not use NepaliDatePicker here.
			 */
			DateTextBlock.Text =
				$"Nepali Date: {selectedBill.NepaliDate}";

			LoadProducts();

			LoadSaleItems();
		}


		// ============================================================
		// LOAD PRODUCTS
		// ============================================================

		private void LoadProducts()
		{
			try
			{
				const string query =
					"""
					SELECT
						ProductID,
						ProductName
					FROM Product
					ORDER BY
						ProductName COLLATE NOCASE ASC
					""";

				productTable =
					DatabaseHelper.GetData(
						query,
						new Dictionary<string, object>());
			}
			catch (Exception ex)
			{
				productTable =
					null;

				_ = ShowMessageAsync(
					"Product Error",
					"Unable to load products.\n\n" +
					ex.Message);
			}
		}


		// ============================================================
		// LOAD EXISTING SALE ITEMS
		// ============================================================

		private void LoadSaleItems()
		{
			if (bill == null)
				return;

			loading =
				true;

			try
			{
				/*
				 * Load BOTH retail and wholesale prices.
				 *
				 * The Sales table stores the actual price used.
				 * We determine which price type was used by comparing
				 * the stored price with the Product table prices.
				 */

				const string query =
					"""
					SELECT
						s.SaleID,
						s.ProductID,
						p.ProductName,

						p.RetailPrice,
						p.WholesalePrice,

						s.Quantity,
						s.Price,
						s.Discount,
						s.DPercentage,
						s.VATAmount

					FROM Sales s

					INNER JOIN Product p
						ON s.ProductID = p.ProductID

					WHERE
						s.CompanyID = @CompanyID
						AND s.BillNumber = @BillNumber

					ORDER BY
						s.SaleID ASC
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

				items.Clear();

				decimal discountPercent =
					0m;

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

					decimal retailPrice =
						ToDecimal(
							row["RetailPrice"]);

					decimal wholesalePrice =
						ToDecimal(
							row["WholesalePrice"]);

					decimal discount =
						ToDecimal(
							row["Discount"]);

					decimal rowDiscountPercent =
						ToDecimal(
							row["DPercentage"]);

					if (rowDiscountPercent > 0m)
					{
						discountPercent =
							rowDiscountPercent;
					}

					/*
					 * Determine the current price type.
					 *
					 * If the saved price equals wholesale price,
					 * show Wholesale.
					 *
					 * Otherwise, show Retail.
					 *
					 * The actual saved price is still preserved.
					 */

					string priceType;

					if (IsSamePrice(
						price,
						wholesalePrice))
					{
						priceType =
							"Wholesale";
					}
					else
					{
						priceType =
							"Retail";
					}

					items.Add(
						new UpdateSaleItem
						{
							SaleID =
								saleId,

							ProductID =
								productId,

							ProductName =
								ToStringValue(
									row["ProductName"]),

							OriginalQuantity =
								quantity,

							Quantity =
								quantity,

							OriginalPrice =
								price,

							Price =
								price,

							RetailPrice =
								retailPrice,

							WholesalePrice =
								wholesalePrice,

							PriceType =
								priceType,

							OriginalDiscount =
								discount,

							OriginalDiscountPercent =
								rowDiscountPercent,

							OriginalVAT =
								ToDecimal(
									row["VATAmount"]),

							IsVattable =
								GetProductVatStatus(
									productId)
						});
				}

				DiscountTextBox.Text =
					discountPercent.ToString(
						"0.##");

				UpdateTotals();
			}
			catch (Exception ex)
			{
				items.Clear();

				_ = ShowMessageAsync(
					"Sales Error",
					"Unable to load the sale.\n\n" +
					ex.Message);
			}
			finally
			{
				loading =
					false;
			}
		}


		// ============================================================
		// PRODUCT VAT STATUS
		// ============================================================

		private bool GetProductVatStatus(
			long productId)
		{
			try
			{
				object? result =
					DatabaseHelper.ExecuteScalar(
						"""
						SELECT
							IsVattable
						FROM Product
						WHERE
							ProductID = @ProductID
						LIMIT 1
						""",
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
			catch
			{
				return false;
			}
		}


		// ============================================================
		// PRODUCT SEARCH
		// ============================================================

		private void ProductAutoSuggestBox_TextChanged(
			AutoSuggestBox sender,
			AutoSuggestBoxTextChangedEventArgs args)
		{
			if (loading)
				return;

			if (args.Reason !=
				AutoSuggestionBoxTextChangeReason.UserInput)
			{
				return;
			}

			if (productTable == null)
				return;

			string search =
				ProductAutoSuggestBox.Text?.Trim()
				?? string.Empty;

			if (string.IsNullOrWhiteSpace(search))
			{
				ProductAutoSuggestBox.ItemsSource =
					null;

				return;
			}

			var results =
				productTable.AsEnumerable()
					.Where(
						row =>
							ToStringValue(
								row["ProductName"])
							.Contains(
								search,
								StringComparison.OrdinalIgnoreCase))
					.Take(20)
					.Select(
						row =>
							new ProductSuggestion
							{
								ProductID =
									ToInt64(
										row["ProductID"]),

								ProductName =
									ToStringValue(
										row["ProductName"])
							})
					.ToList();

			ProductAutoSuggestBox.ItemsSource =
				results;
		}


		// ============================================================
		// PRODUCT SELECTED
		// ============================================================

		private void ProductAutoSuggestBox_SuggestionChosen(
			AutoSuggestBox sender,
			AutoSuggestBoxSuggestionChosenEventArgs args)
		{
			if (args.SelectedItem
				is not ProductSuggestion product)
			{
				return;
			}

			selectedProductId =
				product.ProductID;

			ProductAutoSuggestBox.Text =
				product.ProductName;

			LoadSelectedProductStock(
				product.ProductID);

			LoadSelectedProductPrice(
				product.ProductID);
		}


		// ============================================================
		// LOAD SELECTED PRODUCT STOCK
		// ============================================================

		private void LoadSelectedProductStock(
			long productId)
		{
			decimal stock =
				GetStock(productId);

			/*
			 * Current inventory already excludes the original sale.
			 *
			 * Therefore, if this product is already part of the
			 * existing invoice, add its original quantity back.
			 */

			UpdateSaleItem? existing =
				items.FirstOrDefault(
					x => x.ProductID == productId);

			if (existing != null)
			{
				stock +=
					existing.OriginalQuantity;
			}

			StockTextBlock.Text =
				$"Stock: {stock:N0}";
		}


		// ============================================================
		// LOAD SELECTED PRODUCT PRICE
		// ============================================================

		private void LoadSelectedProductPrice(
			long productId)
		{
			try
			{
				const string query =
					"""
					SELECT
						RetailPrice,
						WholesalePrice
					FROM Product
					WHERE
						ProductID = @ProductID
					LIMIT 1
					""";

				DataTable table =
					DatabaseHelper.GetData(
						query,
						new Dictionary<string, object>
						{
							["@ProductID"] =
								productId
						});

				if (table.Rows.Count == 0)
					return;

				DataRow row =
					table.Rows[0];

				decimal retailPrice =
					ToDecimal(
						row["RetailPrice"]);

				decimal wholesalePrice =
					ToDecimal(
						row["WholesalePrice"]);

				/*
				 * New items use Retail as the default.
				 */
				PriceTextBox.Text =
					retailPrice.ToString(
						"0.##");
			}
			catch
			{
				/*
				 * Manual price entry remains available.
				 */
			}
		}


		// ============================================================
		// EXISTING ITEM QUANTITY CHANGED
		// ============================================================

		private void SaleItemQuantity_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			if (loading)
				return;

			if (sender is not TextBox textBox)
				return;

			if (textBox.Tag
				is not UpdateSaleItem item)
			{
				return;
			}

			string text =
				textBox.Text?.Trim()
				?? string.Empty;

			/*
			 * Allow the user to temporarily type an incomplete
			 * value. Final validation happens before saving.
			 */
			if (string.IsNullOrWhiteSpace(text))
				return;

			if (!long.TryParse(
				text,
				out long quantity))
			{
				return;
			}

			if (quantity < 0)
				return;

			item.Quantity =
				quantity;

			UpdateTotals();
		}


		// ============================================================
		// EXISTING ITEM PRICE TYPE CHANGED
		// ============================================================

		private void SaleItemPriceType_SelectionChanged(
			object sender,
			SelectionChangedEventArgs e)
		{
			if (loading)
				return;

			if (sender is not ComboBox comboBox)
				return;

			if (comboBox.Tag
				is not UpdateSaleItem item)
			{
				return;
			}

			string? selectedType =
				comboBox.SelectedValue
					?.ToString();

			if (string.IsNullOrWhiteSpace(
				selectedType))
			{
				return;
			}

			if (selectedType.Equals(
				"Wholesale",
				StringComparison.OrdinalIgnoreCase))
			{
				item.PriceType =
					"Wholesale";

				item.Price =
					item.WholesalePrice;
			}
			else
			{
				item.PriceType =
					"Retail";

				item.Price =
					item.RetailPrice;
			}

			UpdateTotals();
		}


		// ============================================================
		// ADD ITEM INPUT CHANGED
		// ============================================================

		private void ItemInput_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			if (loading)
				return;

			UpdateTotals();
		}


		// ============================================================
		// DISCOUNT CHANGED
		// ============================================================

		private void DiscountTextBox_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			if (loading)
				return;

			UpdateTotals();
		}


		// ============================================================
		// ADD ITEM
		// ============================================================

		private async void AddItemButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (saving)
				return;

			if (selectedProductId == null)
			{
				await ShowMessageAsync(
					"Select Product",
					"Please select a product.");

				return;
			}

			if (!long.TryParse(
				QuantityTextBox.Text?.Trim(),
				out long quantity) ||
				quantity <= 0)
			{
				await ShowMessageAsync(
					"Invalid Quantity",
					"Please enter a valid quantity greater than zero.");

				return;
			}

			if (!decimal.TryParse(
				PriceTextBox.Text?.Trim(),
				out decimal price) ||
				price < 0m)
			{
				await ShowMessageAsync(
					"Invalid Price",
					"Please enter a valid selling price.");

				return;
			}

			long productId =
				selectedProductId.Value;

			string productName =
				ProductAutoSuggestBox.Text?.Trim()
				?? string.Empty;

			if (string.IsNullOrWhiteSpace(
				productName))
			{
				await ShowMessageAsync(
					"Select Product",
					"Please select a product.");

				return;
			}

			ProductInfo? product =
				GetProductInfo(productId);

			if (product == null)
			{
				await ShowMessageAsync(
					"Product Error",
					"The selected product could not be found.");

				return;
			}

			decimal availableStock =
				GetStock(productId);

			UpdateSaleItem? existing =
				items.FirstOrDefault(
					x => x.ProductID == productId);

			if (existing != null)
			{
				availableStock +=
					existing.OriginalQuantity;

				long newQuantity =
					existing.Quantity +
					quantity;

				if (newQuantity >
					availableStock)
				{
					await ShowMessageAsync(
						"Insufficient Stock",
						$"{productName}\n\n" +
						$"Available: {availableStock:N0}\n" +
						$"Current quantity: {existing.Quantity:N0}\n" +
						$"Additional quantity: {quantity:N0}\n" +
						$"New quantity: {newQuantity:N0}");

					return;
				}

				existing.Quantity =
					newQuantity;

				existing.Price =
					price;

				existing.IsVattable =
					product.IsVattable;

				/*
				 * If adding an existing product again,
				 * keep the current PriceType.
				 */
			}
			else
			{
				if (quantity >
					availableStock)
				{
					await ShowMessageAsync(
						"Insufficient Stock",
						$"{productName}\n\n" +
						$"Available: {availableStock:N0}\n" +
						$"Required: {quantity:N0}");

					return;
				}

				decimal retailPrice =
					GetProductPrice(
						productId,
						"RetailPrice");

				decimal wholesalePrice =
					GetProductPrice(
						productId,
						"WholesalePrice");

				string priceType =
					IsSamePrice(
						price,
						wholesalePrice)
						? "Wholesale"
						: "Retail";

				items.Add(
					new UpdateSaleItem
					{
						SaleID =
							0,

						ProductID =
							productId,

						ProductName =
							productName,

						OriginalQuantity =
							0,

						Quantity =
							quantity,

						OriginalPrice =
							0m,

						Price =
							price,

						RetailPrice =
							retailPrice,

						WholesalePrice =
							wholesalePrice,

						PriceType =
							priceType,

						OriginalDiscount =
							0m,

						OriginalDiscountPercent =
							0m,

						OriginalVAT =
							0m,

						IsVattable =
							product.IsVattable
					});
			}

			ClearItemEntry();

			UpdateTotals();
		}


		// ============================================================
		// GET PRODUCT PRICE
		// ============================================================

		private decimal GetProductPrice(
			long productId,
			string columnName)
		{
			if (columnName != "RetailPrice" &&
				columnName != "WholesalePrice")
			{
				return 0m;
			}

			try
			{
				object? result =
					DatabaseHelper.ExecuteScalar(
						$"""
						SELECT
							{columnName}
						FROM Product
						WHERE
							ProductID = @ProductID
						LIMIT 1
						""",
						new Dictionary<string, object>
						{
							["@ProductID"] =
								productId
						});

				return ToDecimal(result);
			}
			catch
			{
				return 0m;
			}
		}


		// ============================================================
		// REMOVE ITEM
		// ============================================================

		private async void RemoveItemButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (sender is not Button button)
				return;

			if (button.Tag
				is not UpdateSaleItem item)
			{
				return;
			}

			ContentDialog dialog =
				new ContentDialog
				{
					Title =
						"Remove Item",

					Content =
						$"Remove '{item.ProductName}' from this sale?",

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
		// CLEAR NEW ITEM INPUT
		// ============================================================

		private void ClearItemEntry()
		{
			selectedProductId =
				null;

			ProductAutoSuggestBox.Text =
				string.Empty;

			ProductAutoSuggestBox.ItemsSource =
				null;

			QuantityTextBox.Text =
				string.Empty;

			PriceTextBox.Text =
				string.Empty;

			StockTextBlock.Text =
				"Stock: -";
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

			decimal totalDiscount =
				CalculateDiscount(
					subtotal,
					discountPercent);

			decimal vat =
				CalculateVAT(
					subtotal,
					totalDiscount);

			decimal grandTotal =
				subtotal -
				totalDiscount +
				vat;

			/*
			 * Update item-level discount and VAT so the
			 * editable grid reflects the current totals.
			 */

			foreach (UpdateSaleItem item in items)
			{
				decimal itemSubtotal =
					item.Subtotal;

				decimal itemDiscount =
					TaxCalculator.ItemDiscount(
						itemSubtotal,
						subtotal,
						totalDiscount);

				decimal afterDiscount =
					TaxCalculator.ItemAfterDiscount(
						itemSubtotal,
						subtotal,
						totalDiscount);

				decimal itemVAT =
					TaxCalculator.VatOnAmount(
						afterDiscount,
						item.IsVattable);

				item.Discount =
					itemDiscount;

				item.VAT =
					itemVAT;
			}

			SubtotalTextBlock.Text =
				$"Subtotal: Rs. {subtotal:N2}";

			DiscountAmountTextBlock.Text =
				$"Discount: Rs. {totalDiscount:N2}";

			VATTextBlock.Text =
				$"VAT: Rs. {vat:N2}";

			GrandTotalTextBlock.Text =
				$"Grand Total: Rs. {grandTotal:N2}";
		}


		// ============================================================
		// DISCOUNT
		// ============================================================

		private decimal GetDiscountPercent()
		{
			if (!decimal.TryParse(
				DiscountTextBox.Text?.Trim(),
				out decimal value))
			{
				return 0m;
			}

			return Math.Clamp(
				value,
				0m,
				100m);
		}


		private decimal CalculateDiscount(
			decimal subtotal,
			decimal discountPercent)
		{
			if (subtotal <= 0m ||
				discountPercent <= 0m)
			{
				return 0m;
			}

			return
				subtotal *
				discountPercent /
				100m;
		}


		// ============================================================
		// CALCULATE VAT AFTER DISCOUNT
		// ============================================================

		private decimal CalculateVAT(
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
		// UPDATE BUTTON
		// ============================================================

		private async void UpdateButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (saving)
				return;

			if (bill == null)
			{
				await ShowMessageAsync(
					"Sales Error",
					"Sale information is missing.");

				return;
			}

			if (items.Count == 0)
			{
				await ShowMessageAsync(
					"Validation",
					"At least one product is required.");

				return;
			}

			/*
			 * Validate discount text itself.
			 */

			string discountText =
				DiscountTextBox.Text?.Trim()
				?? string.Empty;

			if (!string.IsNullOrWhiteSpace(
				discountText) &&
				!decimal.TryParse(
					discountText,
					out _))
			{
				await ShowMessageAsync(
					"Validation",
					"Please enter a valid discount percentage.");

				return;
			}

			decimal discountPercent =
				GetDiscountPercent();

			if (discountPercent < 0m ||
				discountPercent > 100m)
			{
				await ShowMessageAsync(
					"Validation",
					"Discount must be between 0 and 100.");

				return;
			}

			/*
			 * No duplicate products.
			 */

			if (items
				.GroupBy(
					x => x.ProductID)
				.Any(
					g => g.Count() > 1))
			{
				await ShowMessageAsync(
					"Validation",
					"The same product cannot appear more than once.");

				return;
			}


			// ========================================================
			// VALIDATE EVERY ITEM
			// ========================================================

			foreach (UpdateSaleItem item in items)
			{
				if (item.ProductID <= 0)
				{
					await ShowMessageAsync(
						"Validation",
						$"Invalid product: {item.ProductName}");

					return;
				}

				if (item.Quantity <= 0)
				{
					await ShowMessageAsync(
						"Validation",
						$"Quantity for '{item.ProductName}' must be greater than zero.");

					return;
				}

				if (item.Price < 0m)
				{
					await ShowMessageAsync(
						"Validation",
						$"Price for '{item.ProductName}' cannot be negative.");

					return;
				}

				/*
				 * The original quantity becomes available again
				 * while editing the invoice.
				 */

				decimal availableStock =
					GetStock(
						item.ProductID);

				availableStock +=
					item.OriginalQuantity;

				if (item.Quantity >
					availableStock)
				{
					await ShowMessageAsync(
						"Insufficient Stock",
						$"{item.ProductName}\n\n" +
						$"Available: {availableStock:N0}\n" +
						$"Required: {item.Quantity:N0}");

					return;
				}
			}


			// ========================================================
			// CALCULATE TOTALS
			// ========================================================

			decimal subtotal =
				items.Sum(
					x => x.Subtotal);

			decimal totalDiscount =
				CalculateDiscount(
					subtotal,
					discountPercent);

			decimal vat =
				CalculateVAT(
					subtotal,
					totalDiscount);

			decimal grandTotal =
				subtotal -
				totalDiscount +
				vat;


			// ========================================================
			// CONFIRM
			// ========================================================

			ContentDialog confirmation =
				new ContentDialog
				{
					Title =
						"Update Sale",

					Content =
						$"Update bill {bill.BillNumber}?\n\n" +
						$"Items: {items.Count}\n" +
						$"Subtotal: Rs. {subtotal:N2}\n" +
						$"Discount: Rs. {totalDiscount:N2}\n" +
						$"VAT: Rs. {vat:N2}\n" +
						$"Grand Total: Rs. {grandTotal:N2}",

					PrimaryButtonText =
						"Update",

					CloseButtonText =
						"Cancel",

					DefaultButton =
						ContentDialogButton.Primary,

					XamlRoot =
						XamlRoot
				};

			ContentDialogResult confirmationResult =
				await confirmation.ShowAsync();

			if (confirmationResult !=
				ContentDialogResult.Primary)
			{
				return;
			}

			saving =
				true;

			try
			{
				await PerformDatabaseUpdateAsync(
					discountPercent,
					subtotal,
					totalDiscount);
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					"Update Failed",
					"The sale could not be updated.\n\n" +
					ex.Message);
			}
			finally
			{
				saving =
					false;
			}
		}


		// ============================================================
		// DATABASE UPDATE
		// ============================================================

		private async Task PerformDatabaseUpdateAsync(
			decimal discountPercent,
			decimal subtotal,
			decimal totalDiscount)
		{
			if (bill == null)
			{
				throw new InvalidOperationException(
					"Sale information is missing.");
			}

			using var connection =
				DatabaseHelper.GetConnection();

			connection.Open();

			using var transaction =
				connection.BeginTransaction();

			try
			{
				// ====================================================
				// STEP 1
				// READ ORIGINAL BILL ROWS
				// ====================================================

				const string originalQuery =
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

				var originalRows =
					new List<OriginalSaleRow>();

				using (
					var command =
						new System.Data.SQLite.SQLiteCommand(
							originalQuery,
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
						originalRows.Add(
							new OriginalSaleRow
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

				if (originalRows.Count == 0)
				{
					throw new InvalidOperationException(
						"The original sale could not be found.");
				}


				// ====================================================
				// STEP 2
				// RESTORE ORIGINAL INVENTORY
				// ====================================================

				foreach (
					var group in originalRows.GroupBy(
						x => x.ProductID))
				{
					long quantityToRestore =
						group.Sum(
							x => x.Quantity);

					const string restoreQuery =
						"""
						UPDATE Inventory
						SET
							TotalStockLeft =
								TotalStockLeft + @Quantity
						WHERE
							ProductID = @ProductID
						""";

					using var restoreCommand =
						new System.Data.SQLite.SQLiteCommand(
							restoreQuery,
							connection,
							transaction);

					restoreCommand.Parameters.AddWithValue(
						"@Quantity",
						quantityToRestore);

					restoreCommand.Parameters.AddWithValue(
						"@ProductID",
						group.Key);

					int affected =
						restoreCommand.ExecuteNonQuery();

					if (affected == 0)
					{
						throw new InvalidOperationException(
							$"Inventory record not found for ProductID {group.Key}.");
					}
				}


				// ====================================================
				// STEP 3
				// REMOVE OLD SYNC QUEUE RECORDS
				// ====================================================

				const string deleteSyncQuery =
					"""
					DELETE FROM SyncQueue
					WHERE
						TableName = 'Sales'
						AND RowId IN
						(
							SELECT
								SaleID
							FROM Sales
							WHERE
								CompanyID = @CompanyID
								AND BillNumber = @BillNumber
						)
					""";

				using (
					var deleteSyncCommand =
						new System.Data.SQLite.SQLiteCommand(
							deleteSyncQuery,
							connection,
							transaction))
				{
					deleteSyncCommand.Parameters.AddWithValue(
						"@CompanyID",
						bill.CompanyID);

					deleteSyncCommand.Parameters.AddWithValue(
						"@BillNumber",
						bill.BillNumber);

					deleteSyncCommand.ExecuteNonQuery();
				}


				// ====================================================
				// STEP 4
				// DELETE OLD SALES ROWS
				// ====================================================

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

					deleteSalesCommand.ExecuteNonQuery();
				}


				// ====================================================
				// STEP 5
				// GET ORIGINAL SALE DATE
				// ====================================================

				string saleDate =
					GetOriginalSaleDate(
						originalRows,
						bill);


				string nepaliDate =
					bill.NepaliDate;


				// ====================================================
				// STEP 6
				// INSERT FINAL SALE ROWS
				// ====================================================

				foreach (UpdateSaleItem item in items)
				{
					decimal itemSubtotal =
						item.Subtotal;

					decimal itemDiscount =
						TaxCalculator.ItemDiscount(
							itemSubtotal,
							subtotal,
							totalDiscount);

					decimal afterDiscount =
						TaxCalculator.ItemAfterDiscount(
							itemSubtotal,
							subtotal,
							totalDiscount);

					decimal itemVAT =
						TaxCalculator.VatOnAmount(
							afterDiscount,
							item.IsVattable);

					decimal totalWithVAT =
						afterDiscount +
						itemVAT;


					const string insertQuery =
						"""
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
							FiscalYear,
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
							@FiscalYear,
							@VATAmount,
							@TotalAmountWithVAT
						);

						SELECT last_insert_rowid();
						""";


					using var insertCommand =
						new System.Data.SQLite.SQLiteCommand(
							insertQuery,
							connection,
							transaction);


					insertCommand.Parameters.AddWithValue(
						"@ProductID",
						item.ProductID);

					insertCommand.Parameters.AddWithValue(
						"@CompanyID",
						bill.CompanyID);

					insertCommand.Parameters.AddWithValue(
						"@Quantity",
						item.Quantity);

					insertCommand.Parameters.AddWithValue(
						"@Price",
						item.Price);

					insertCommand.Parameters.AddWithValue(
						"@Discount",
						itemDiscount);

					insertCommand.Parameters.AddWithValue(
						"@DPercentage",
						discountPercent);

					insertCommand.Parameters.AddWithValue(
						"@SaleDate",
						saleDate);

					insertCommand.Parameters.AddWithValue(
						"@TotalSalesAmount",
						itemSubtotal);

					insertCommand.Parameters.AddWithValue(
						"@TotalAmountAfterDiscount",
						afterDiscount);

					insertCommand.Parameters.AddWithValue(
						"@BillNumber",
						bill.BillNumber);

					insertCommand.Parameters.AddWithValue(
						"@NepaliDate",
						nepaliDate);

					insertCommand.Parameters.AddWithValue(
						"@FiscalYear",
						DatabaseHelper.FiscalYearOf(nepaliDate) ?? (object)DBNull.Value);

					insertCommand.Parameters.AddWithValue(
						"@VATAmount",
						itemVAT);

					insertCommand.Parameters.AddWithValue(
						"@TotalAmountWithVAT",
						totalWithVAT);


					long newSaleId =
						ToInt64(
							insertCommand.ExecuteScalar());

					if (newSaleId <= 0)
					{
						throw new InvalidOperationException(
							$"Unable to create sale row for {item.ProductName}.");
					}


					// =================================================
					// STEP 7
					// DEDUCT FINAL INVENTORY
					// =================================================

					const string stockQuery =
						"""
						UPDATE Inventory
						SET
							TotalStockLeft =
								TotalStockLeft - @Quantity
						WHERE
							ProductID = @ProductID
							AND TotalStockLeft >= @Quantity
						""";

					using var stockCommand =
						new System.Data.SQLite.SQLiteCommand(
							stockQuery,
							connection,
							transaction);

					stockCommand.Parameters.AddWithValue(
						"@Quantity",
						item.Quantity);

					stockCommand.Parameters.AddWithValue(
						"@ProductID",
						item.ProductID);

					int stockAffected =
						stockCommand.ExecuteNonQuery();

					if (stockAffected == 0)
					{
						throw new InvalidOperationException(
							$"Insufficient stock for {item.ProductName}.");
					}


					// =================================================
					// STEP 8
					// SYNC QUEUE
					// =================================================

					InsertSyncQueue(
						connection,
						transaction,
						newSaleId,
						"INSERT");
				}


				// ====================================================
				// STEP 9
				// COMMIT
				// ====================================================

				transaction.Commit();


				await ShowMessageAsync(
					"Sale Updated",
					$"Bill {bill.BillNumber} has been updated successfully.");

				if (Frame?.CanGoBack == true)
				{
					Frame.GoBack();
				}
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
		// ORIGINAL SALE DATE
		// ============================================================

		private string GetOriginalSaleDate(
			List<OriginalSaleRow> originalRows,
			SalesBillItem selectedBill)
		{
			try
			{
				const string query =
					"""
					SELECT
						SaleDate
					FROM Sales
					WHERE
						CompanyID = @CompanyID
						AND BillNumber = @BillNumber
					LIMIT 1
					""";

				object? result =
					DatabaseHelper.ExecuteScalar(
						query,
						new Dictionary<string, object>
						{
							["@CompanyID"] =
								selectedBill.CompanyID,

							["@BillNumber"] =
								selectedBill.BillNumber
						});

				if (result != null &&
					result != DBNull.Value)
				{
					return result.ToString()
						?? DateTime.Now.ToString(
							"yyyy-MM-dd");
				}
			}
			catch
			{
				// Fall through to current date.
			}

			return DateTime.Now.ToString(
				"yyyy-MM-dd");
		}


		// ============================================================
		// SYNC QUEUE
		// ============================================================

		private void InsertSyncQueue(
			System.Data.SQLite.SQLiteConnection connection,
			System.Data.SQLite.SQLiteTransaction transaction,
			long rowId,
			string action)
		{
			const string query =
				"""
				INSERT INTO SyncQueue
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
					@Action,
					0,
					datetime('now')
				)
				""";

			using var command =
				new System.Data.SQLite.SQLiteCommand(
					query,
					connection,
					transaction);

			command.Parameters.AddWithValue(
				"@RowId",
				rowId);

			command.Parameters.AddWithValue(
				"@Action",
				action);

			command.ExecuteNonQuery();
		}


		// ============================================================
		// GET PRODUCT INFORMATION
		// ============================================================

		private ProductInfo? GetProductInfo(
			long productId)
		{
			const string query =
				"""
				SELECT
					ProductID,
					ProductName,
					IsVattable,
					HSCode
				FROM Product
				WHERE
					ProductID = @ProductID
				LIMIT 1
				""";

			using var reader =
				DatabaseHelper.ExecuteReader(
					query,
					new Dictionary<string, object>
					{
						["@ProductID"] =
							productId
					});

			if (!reader.Read())
				return null;

			return new ProductInfo
			{
				ProductID =
					ToInt64(
						reader["ProductID"]),

				ProductName =
					ToStringValue(
						reader["ProductName"]),

				IsVattable =
					reader["IsVattable"] != DBNull.Value &&
					Convert.ToInt32(
						reader["IsVattable"]) == 1,

				HSCode =
					ToStringValue(
						reader["HSCode"])
			};
		}


		// ============================================================
		// GET STOCK
		// ============================================================

		private decimal GetStock(
			long productId)
		{
			object? result =
				DatabaseHelper.ExecuteScalar(
					"""
					SELECT
						TotalStockLeft
					FROM Inventory
					WHERE
						ProductID = @ProductID
					""",
					new Dictionary<string, object>
					{
						["@ProductID"] =
							productId
					});

			if (result == null ||
				result == DBNull.Value)
			{
				return 0m;
			}

			return ToDecimal(result);
		}


		// ============================================================
		// BACK
		// ============================================================

		private void BackButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (Frame?.CanGoBack == true)
			{
				Frame.GoBack();
			}
		}


		// ============================================================
		// MESSAGE
		// ============================================================

		private async Task ShowMessageAsync(
			string title,
			string message)
		{
			if (XamlRoot == null)
				return;

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
		// HELPERS
		// ============================================================

		private static string ToStringValue(
			object? value)
		{
			if (value == null ||
				value == DBNull.Value)
			{
				return string.Empty;
			}

			return value.ToString()
				?? string.Empty;
		}


		private static long ToInt64(
			object? value)
		{
			if (value == null ||
				value == DBNull.Value)
			{
				return 0;
			}

			return Convert.ToInt64(value);
		}


		private static decimal ToDecimal(
			object? value)
		{
			if (value == null ||
				value == DBNull.Value)
			{
				return 0m;
			}

			return Convert.ToDecimal(value);
		}


		private static bool IsSamePrice(
			decimal first,
			decimal second)
		{
			return Math.Abs(
				first - second) < 0.01m;
		}


		// ============================================================
		// PRODUCT SUGGESTION
		// ============================================================

		private sealed class ProductSuggestion
		{
			public long ProductID { get; set; }

			public string ProductName { get; set; }
				= string.Empty;

			public override string ToString()
			{
				return ProductName;
			}
		}


		// ============================================================
		// PRODUCT INFORMATION
		// ============================================================

		private sealed class ProductInfo
		{
			public long ProductID { get; set; }

			public string ProductName { get; set; }
				= string.Empty;

			public bool IsVattable { get; set; }

			public string HSCode { get; set; }
				= string.Empty;
		}


		// ============================================================
		// ORIGINAL SALE ROW
		// ============================================================

		private sealed class OriginalSaleRow
		{
			public long SaleID { get; set; }

			public long ProductID { get; set; }

			public long Quantity { get; set; }
		}


		// ============================================================
		// UPDATE SALE ITEM
		// ============================================================

		private sealed class UpdateSaleItem :
			INotifyPropertyChanged
		{
			public long SaleID { get; set; }

			public long ProductID { get; set; }

			public string ProductName { get; set; }
				= string.Empty;


			// --------------------------------------------------------
			// ORIGINAL VALUES
			// --------------------------------------------------------

			public long OriginalQuantity { get; set; }

			public decimal OriginalPrice { get; set; }

			public decimal OriginalDiscount { get; set; }

			public decimal OriginalDiscountPercent { get; set; }

			public decimal OriginalVAT { get; set; }


			// --------------------------------------------------------
			// CURRENT QUANTITY
			// --------------------------------------------------------

			private long quantity;

			public long Quantity
			{
				get =>
					quantity;

				set
				{
					if (quantity == value)
						return;

					quantity =
						value;

					OnPropertyChanged();

					OnPropertyChanged(
						nameof(Subtotal));
				}
			}


			// --------------------------------------------------------
			// CURRENT PRICE
			// --------------------------------------------------------

			private decimal price;

			public decimal Price
			{
				get =>
					price;

				set
				{
					if (price == value)
						return;

					price =
						value;

					OnPropertyChanged();

					OnPropertyChanged(
						nameof(Subtotal));
				}
			}


			// --------------------------------------------------------
			// RETAIL PRICE
			// --------------------------------------------------------

			public decimal RetailPrice { get; set; }


			// --------------------------------------------------------
			// WHOLESALE PRICE
			// --------------------------------------------------------

			public decimal WholesalePrice { get; set; }


			// --------------------------------------------------------
			// PRICE TYPE
			// --------------------------------------------------------

			private string priceType =
				"Retail";

			public string PriceType
			{
				get =>
					priceType;

				set
				{
					if (priceType == value)
						return;

					priceType =
						value;

					OnPropertyChanged();
				}
			}


			// --------------------------------------------------------
			// VAT STATUS
			// --------------------------------------------------------

			public bool IsVattable { get; set; }


			// --------------------------------------------------------
			// CALCULATED VALUES
			// --------------------------------------------------------

			public decimal Subtotal =>
				Quantity * Price;


			private decimal discount;

			public decimal Discount
			{
				get =>
					discount;

				set
				{
					if (discount == value)
						return;

					discount =
						value;

					OnPropertyChanged();
				}
			}


			private decimal vat;

			public decimal VAT
			{
				get =>
					vat;

				set
				{
					if (vat == value)
						return;

					vat =
						value;

					OnPropertyChanged();
				}
			}


			// --------------------------------------------------------
			// PROPERTY CHANGED
			// --------------------------------------------------------

			public event PropertyChangedEventHandler?
				PropertyChanged;


			private void OnPropertyChanged(
				[CallerMemberName]
				string? propertyName = null)
			{
				PropertyChanged?.Invoke(
					this,
					new PropertyChangedEventArgs(
						propertyName));
			}
		}
	}
}