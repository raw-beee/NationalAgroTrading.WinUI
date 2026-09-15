using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NationalAgroTrading.WinUI.Data;
using NationalAgroTrading.WinUI.Models;
using NationalAgroTrading.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NationalAgroTrading.WinUI.Views.Sales
{
	public sealed partial class AddSalePage : Page
	{

		private void ItemInput_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			UpdateTotals();
		}
		// ============================================================
		// PRICE MODE
		// ============================================================

		private void RetailButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (saving)
				return;

			wholesale = false;

			SetPriceMode();

			ReloadCurrentProduct();
		}


		private void WholesaleButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (saving)
				return;

			wholesale = true;

			SetPriceMode();

			ReloadCurrentProduct();
		}


		private void SetPriceMode()
		{
			if (RetailButton == null ||
				WholesaleButton == null)
			{
				return;
			}

			RetailButton.Opacity =
				wholesale ? 0.55 : 1.0;

			WholesaleButton.Opacity =
				wholesale ? 1.0 : 0.55;
		}


		private void ReloadCurrentProduct()
		{
			string productName =
				ProductAutoSuggestBox.Text?.Trim()
				?? string.Empty;

			if (string.IsNullOrWhiteSpace(productName))
				return;

			LoadProductInformation(productName);
		}
		// ============================================================
		// CONSTANTS
		// ============================================================

		private const decimal VatRate = TaxCalculator.VatRate;


		// ============================================================
		// COLLECTIONS
		// ============================================================

		private readonly ObservableCollection<SaleEntryItem> items =
			new();


		private List<string> companyNames =
			new();

		private List<string> productNames =
			new();


		// ============================================================
		// STATE
		// ============================================================

		private bool wholesale;

		private bool saving;

		private long? selectedProductId;


		// ============================================================
		// CONSTRUCTOR
		// ============================================================

		public AddSalePage()
		{
			InitializeComponent();

			SaleItemsListView.ItemsSource =
				items;

			LoadCompanies();

			LoadProducts();

			SetPriceMode();

			UpdateTotals();
		}


		// ============================================================
		// COMPANIES
		// ============================================================

		private void LoadCompanies()
		{
			try
			{
				companyNames =
					DatabaseHelper.GetCompanyNames()
					?? new List<string>();

				CompanyAutoSuggestBox.ItemsSource =
					companyNames;
			}
			catch
			{
				companyNames =
					new List<string>();

				CompanyAutoSuggestBox.ItemsSource =
					null;
			}
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
				sender.Text?.Trim()
				?? string.Empty;

			sender.ItemsSource =
				string.IsNullOrWhiteSpace(search)
					? companyNames
					: companyNames
						.Where(
							x => x.Contains(
								search,
								StringComparison.OrdinalIgnoreCase))
						.Take(20)
						.ToList();
		}


		private void CompanyAutoSuggestBox_SuggestionChosen(
			AutoSuggestBox sender,
			AutoSuggestBoxSuggestionChosenEventArgs args)
		{
			if (args.SelectedItem == null)
				return;

			sender.Text =
				args.SelectedItem.ToString()
				?? string.Empty;
		}


		// ============================================================
		// PRODUCTS
		// ============================================================

		private void LoadProducts()
		{
			try
			{
				productNames =
					DatabaseHelper.GetProductNames()
					?? new List<string>();

				ProductAutoSuggestBox.ItemsSource =
					productNames;
			}
			catch
			{
				productNames =
					new List<string>();

				ProductAutoSuggestBox.ItemsSource =
					null;
			}
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
				sender.Text?.Trim()
				?? string.Empty;

			if (string.IsNullOrWhiteSpace(search))
			{
				sender.ItemsSource =
					productNames;

				return;
			}

			sender.ItemsSource =
				productNames
					.Where(
						x => x.Contains(
							search,
							StringComparison.OrdinalIgnoreCase))
					.Take(20)
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

			LoadProductInformation(
				productName);
		}


		// ============================================================
		// PRODUCT INFORMATION
		// ============================================================

		private void LoadProductInformation(
			string productName)
		{
			if (string.IsNullOrWhiteSpace(productName))
				return;

			try
			{
				string priceColumn =
					wholesale
						? "WholesalePrice"
						: "RetailPrice";

				const string query =
					"""
                    SELECT
                        ProductID,
                        RetailPrice,
                        WholesalePrice,
                        IsVattable,
                        HSCode
                    FROM Product
                    WHERE ProductName = @ProductName
                    LIMIT 1
                    """;

				using var reader =
					DatabaseHelper.ExecuteReader(
						query,
						new Dictionary<string, object>
						{
							["@ProductName"] =
								productName
						});

				if (!reader.Read())
				{
					selectedProductId =
						null;

					StockTextBlock.Text =
						"Stock: -";

					VATStatusTextBlock.Text =
						"VAT: -";

					PriceTextBox.Text =
						string.Empty;

					return;
				}

				long productId =
					Convert.ToInt64(
						reader["ProductID"]);

				selectedProductId =
					productId;

				decimal price =
					wholesale
						? ToDecimal(
							reader["WholesalePrice"])
						: ToDecimal(
							reader["RetailPrice"]);

				bool isVattable =
					reader["IsVattable"] != DBNull.Value &&
					Convert.ToInt32(
						reader["IsVattable"]) == 1;

				PriceTextBox.Text =
					price.ToString("0.00");

				VATStatusTextBlock.Text =
					isVattable
						? "VAT: 13%"
						: "VAT: Non-VAT";

				decimal stock =
					GetStock(productId);

				StockTextBlock.Text =
					$"Stock: {stock:N0}";
			}
			catch (Exception ex)
			{
				selectedProductId =
					null;

				_ = ShowMessageAsync(
					"Unable to load product information.\n\n" +
					ex.Message,
					"Product Error");
			}
		}


		// ============================================================
		// PRICE MODE
		// ============================================================

		private void RetailRadioButton_Checked(
			object sender,
			RoutedEventArgs e)
		{
			if (saving)
				return;

			wholesale =
				false;

			SetPriceMode();

			ReloadSelectedProduct();
		}


		private void WholesaleRadioButton_Checked(
			object sender,
			RoutedEventArgs e)
		{
			if (saving)
				return;

			wholesale =
				true;

			SetPriceMode();

			ReloadSelectedProduct();
		}


		private void ReloadSelectedProduct()
		{
			string productName =
				ProductAutoSuggestBox.Text?.Trim()
				?? string.Empty;

			if (string.IsNullOrWhiteSpace(productName))
				return;

			LoadProductInformation(
				productName);
		}


		// ============================================================
		// STOCK
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
                    WHERE ProductID = @ProductID
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
					"Validation",
					"Please select a product.");

				return;
			}

			if (!long.TryParse(
				QuantityTextBox.Text?.Trim(),
				out long quantity) ||
				quantity <= 0)
			{
				await ShowMessageAsync(
					"Validation",
					"Quantity must be greater than zero.");

				return;
			}

			if (!decimal.TryParse(
				PriceTextBox.Text?.Trim(),
				out decimal price) ||
				price < 0m)
			{
				await ShowMessageAsync(
					"Validation",
					"Please enter a valid selling price.");

				return;
			}

			long productId =
				selectedProductId.Value;

			string productName =
				ProductAutoSuggestBox.Text?.Trim()
				?? string.Empty;

			if (string.IsNullOrWhiteSpace(productName))
			{
				await ShowMessageAsync(
					"Validation",
					"Please select a product.");

				return;
			}

			// --------------------------------------------------------
			// Make sure product still exists.
			// --------------------------------------------------------

			ProductSaleInfo? product =
				GetProductInfo(productId);

			if (product == null)
			{
				await ShowMessageAsync(
					"Product Error",
					"The selected product could not be found.");

				return;
			}

			// --------------------------------------------------------
			// Check current inventory.
			// --------------------------------------------------------

			decimal stock =
				GetStock(productId);

			// --------------------------------------------------------
			// Prevent duplicate product rows.
			// --------------------------------------------------------

			SaleEntryItem? existing =
				items.FirstOrDefault(
					x => x.ProductID == productId);

			if (existing != null)
			{
				await ShowMessageAsync(
					"Duplicate Product",
					$"'{productName}' is already added to this sale.\n\n" +
					"Remove the existing row and add it again if you want " +
					"to change its quantity or price.");

				return;
			}

			if (quantity > stock)
			{
				await ShowMessageAsync(
					"Insufficient Stock",
					$"{productName}\n\n" +
					$"Available: {stock:N0}\n" +
					$"Required: {quantity:N0}");

				return;
			}

			items.Add(
				new SaleEntryItem
				{
					ProductID =
						productId,

					ProductName =
						productName,

					Quantity =
						quantity,

					Price =
						price,

					IsVattable =
						product.IsVattable,

					HSCode =
						product.HSCode,

					PriceType =
						wholesale
							? "Wholesale"
							: "Retail"
				});

			UpdateTotals();

			ClearItemEntry();
		}


		// ============================================================
		// PRODUCT INFORMATION MODEL
		// ============================================================

		private sealed class ProductSaleInfo
		{
			public long ProductID { get; set; }

			public bool IsVattable { get; set; }

			public string HSCode { get; set; } =
				string.Empty;
		}


		private ProductSaleInfo? GetProductInfo(
			long productId)
		{
			const string query =
				"""
                SELECT
                    ProductID,
                    IsVattable,
                    HSCode
                FROM Product
                WHERE ProductID = @ProductID
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

			return new ProductSaleInfo
			{
				ProductID =
					Convert.ToInt64(
						reader["ProductID"]),

				IsVattable =
					reader["IsVattable"] != DBNull.Value &&
					Convert.ToInt32(
						reader["IsVattable"]) == 1,

				HSCode =
					reader["HSCode"] == DBNull.Value
						? string.Empty
						: reader["HSCode"]?.ToString()
							?? string.Empty
			};
		}


		// ============================================================
		// CLEAR ITEM ENTRY
		// ============================================================

		private void ClearItemEntry()
		{
			selectedProductId =
				null;

			ProductAutoSuggestBox.Text =
				string.Empty;

			ProductAutoSuggestBox.ItemsSource =
				productNames;

			QuantityTextBox.Text =
				string.Empty;

			PriceTextBox.Text =
				string.Empty;

			StockTextBlock.Text =
				"Stock: -";

			VATStatusTextBlock.Text =
				"VAT: -";
		}


		// ============================================================
		// REMOVE ITEM
		// ============================================================

		private void RemoveItemButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (sender is not Button button)
				return;

			if (button.Tag is not SaleEntryItem item)
				return;

			items.Remove(item);

			UpdateTotals();
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
				subtotal *
				discountPercent /
				100m;

			decimal vat =
				CalculateVatAfterDiscount(
					subtotal,
					totalDiscount);

			decimal grandTotal =
				subtotal -
				totalDiscount +
				vat;

			SubtotalTextBlock.Text =
				$"Subtotal: Rs. {subtotal:N2}";

			DiscountAmountTextBlock.Text =
				$"Discount: Rs. {totalDiscount:N2}";

			VATTextBlock.Text =
				$"VAT: Rs. {vat:N2}";

			GrandTotalTextBlock.Text =
				$"Grand Total: Rs. {grandTotal:N2}";
		}


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
		// SAVE
		// ============================================================

		private async void SaveButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (saving)
				return;

			saving = true;

			try
			{
				await SaveSaleAsync();
			}
			finally
			{
				saving = false;
			}
		}


		private async Task SaveSaleAsync()
		{
			// --------------------------------------------------------
			// Company
			// --------------------------------------------------------

			string companyName =
				CompanyAutoSuggestBox.Text?.Trim()
				?? string.Empty;

			if (string.IsNullOrWhiteSpace(companyName))
			{
				await ShowMessageAsync(
					"Validation",
					"Please select a company.");

				return;
			}

			// --------------------------------------------------------
			// Bill number
			// --------------------------------------------------------

			string billNumber =
				BillNumberTextBox.Text?.Trim()
				?? string.Empty;

			if (string.IsNullOrWhiteSpace(billNumber))
			{
				await ShowMessageAsync(
					"Validation",
					"Please enter the bill number.");

				return;
			}

			// --------------------------------------------------------
			// Items
			// --------------------------------------------------------

			if (items.Count == 0)
			{
				await ShowMessageAsync(
					"Validation",
					"Please add at least one product.");

				return;
			}

			// --------------------------------------------------------
			// Duplicate products
			// --------------------------------------------------------

			if (items
				.GroupBy(x => x.ProductID)
				.Any(g => g.Count() > 1))
			{
				await ShowMessageAsync(
					"Validation",
					"The same product cannot appear more than once.");

				return;
			}

			// --------------------------------------------------------
			// Discount
			// --------------------------------------------------------

			string discountText =
				DiscountTextBox.Text?.Trim()
				?? string.Empty;

			if (!string.IsNullOrWhiteSpace(discountText) &&
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

			// --------------------------------------------------------
			// Nepali date
			// --------------------------------------------------------

			if (!SaleDatePicker.IsDateValid ||
				!SaleDatePicker.TryGetSelectedDate(
					out DateTime adDate,
					out string bsDate))
			{
				await ShowMessageAsync(
					"Validation",
					"Please select a valid Nepali date.");

				return;
			}

			// --------------------------------------------------------
			// Company ID
			// --------------------------------------------------------

			long companyId =
				GetCompanyId(companyName);

			if (companyId <= 0)
			{
				await ShowMessageAsync(
					"Validation",
					"Company was not found.");

				return;
			}

			// --------------------------------------------------------
			// Check duplicate bill number.
			// --------------------------------------------------------

			if (BillExists(
				companyId,
				billNumber,
				DatabaseHelper.FiscalYearOf(bsDate)))
			{
				await ShowMessageAsync(
					"Duplicate Bill",
					$"Bill {billNumber} already exists for {companyName} " +
					$"in fiscal year {DatabaseHelper.FiscalYearOf(bsDate)}.\n" +
					"Please use a different bill number.");

				return;
			}

			// --------------------------------------------------------
			// Validate every item.
			// --------------------------------------------------------

			foreach (SaleEntryItem item in items)
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

				decimal stock =
					GetStock(item.ProductID);

				if (item.Quantity > stock)
				{
					await ShowMessageAsync(
						"Insufficient Stock",
						$"{item.ProductName}\n\n" +
						$"Available: {stock:N0}\n" +
						$"Required: {item.Quantity:N0}");

					return;
				}
			}

			// --------------------------------------------------------
			// Totals
			// --------------------------------------------------------

			decimal subtotal =
				items.Sum(
					x => x.Subtotal);

			decimal totalDiscount =
				subtotal *
				discountPercent /
				100m;

			// --------------------------------------------------------
			// Confirmation
			// --------------------------------------------------------

			ContentDialog confirmation =
				new ContentDialog
				{
					Title =
						"Save Sale",

					Content =
						$"Save bill {billNumber}?\n\n" +
						$"Company: {companyName}\n" +
						$"Date: {bsDate}\n" +
						$"Items: {items.Count}\n" +
						$"Grand Total: Rs. {CalculateGrandTotal(subtotal, totalDiscount):N2}",

					PrimaryButtonText =
						"Save",

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

			// --------------------------------------------------------
			// Database transaction
			// --------------------------------------------------------

			try
			{
				using var conn =
					DatabaseHelper.GetConnection();

				conn.Open();

				using var transaction =
					conn.BeginTransaction();

				try
				{
					foreach (SaleEntryItem item in items)
					{
						// --------------------------------------------
						// Recheck inventory inside transaction.
						// --------------------------------------------

						decimal currentStock =
							GetStock(
								conn,
								transaction,
								item.ProductID);

						if (item.Quantity >
							currentStock)
						{
							throw new InvalidOperationException(
								$"Insufficient stock for {item.ProductName}.\n\n" +
								$"Available: {currentStock:N0}\n" +
								$"Required: {item.Quantity:N0}");
						}

						// --------------------------------------------
						// Item calculations
						// --------------------------------------------

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

						decimal vat =
							TaxCalculator.VatOnAmount(
								afterDiscount,
								item.IsVattable);

						decimal totalWithVat =
							afterDiscount +
							vat;

						// --------------------------------------------
						// Insert Sales row
						// --------------------------------------------

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

						using var saleCommand =
							new System.Data.SQLite.SQLiteCommand(
								insertQuery,
								conn,
								transaction);

						saleCommand.Parameters.AddWithValue(
							"@ProductID",
							item.ProductID);

						saleCommand.Parameters.AddWithValue(
							"@CompanyID",
							companyId);

						saleCommand.Parameters.AddWithValue(
							"@Quantity",
							item.Quantity);

						saleCommand.Parameters.AddWithValue(
							"@Price",
							item.Price);

						saleCommand.Parameters.AddWithValue(
							"@Discount",
							itemDiscount);

						saleCommand.Parameters.AddWithValue(
							"@DPercentage",
							discountPercent);

						saleCommand.Parameters.AddWithValue(
							"@SaleDate",
							adDate.ToString("yyyy-MM-dd"));

						saleCommand.Parameters.AddWithValue(
							"@TotalSalesAmount",
							itemSubtotal);

						saleCommand.Parameters.AddWithValue(
							"@TotalAmountAfterDiscount",
							afterDiscount);

						saleCommand.Parameters.AddWithValue(
							"@BillNumber",
							$"Bill {billNumber}");

						saleCommand.Parameters.AddWithValue(
							"@NepaliDate",
							bsDate);

						saleCommand.Parameters.AddWithValue(
							"@FiscalYear",
							DatabaseHelper.FiscalYearOf(bsDate) ?? (object)DBNull.Value);

						saleCommand.Parameters.AddWithValue(
							"@VATAmount",
							vat);

						saleCommand.Parameters.AddWithValue(
							"@TotalAmountWithVAT",
							totalWithVat);

						long saleId =
							Convert.ToInt64(
								saleCommand.ExecuteScalar());

						if (saleId <= 0)
						{
							throw new InvalidOperationException(
								$"Unable to create sale row for {item.ProductName}.");
						}

						// --------------------------------------------
						// Deduct inventory
						// --------------------------------------------

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
								conn,
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
								$"Unable to update inventory for {item.ProductName}.");
						}

						// --------------------------------------------
						// SyncQueue
						// --------------------------------------------

						const string syncQuery =
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
                                'INSERT',
                                0,
                                datetime('now')
                            )
                            """;

						using var syncCommand =
							new System.Data.SQLite.SQLiteCommand(
								syncQuery,
								conn,
								transaction);

						syncCommand.Parameters.AddWithValue(
							"@RowId",
							saleId);

						syncCommand.ExecuteNonQuery();
					}

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
			catch (Exception ex)
			{
				string reason =
					ex.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase)
						? $"Bill {billNumber} already exists in fiscal year {DatabaseHelper.FiscalYearOf(bsDate)}.\n" +
						  "Please use a different bill number.\n\n"
						: string.Empty;

				await ShowMessageAsync(
					"Save Failed",
					"The sale could not be saved.\n\n" +
					reason +
					ex.Message);

				return;
			}

			// --------------------------------------------------------
			// Success
			//
			// The bill PDF is generated automatically. A failure to
			// generate it must not fail the saved sale, so any error
			// is only reported in the confirmation dialog.
			// --------------------------------------------------------

			string? pdfPath =
				null;

			string pdfError =
				string.Empty;

			try
			{
				pdfPath =
					await SalesBillPdfGenerator.GenerateBillPdfAsync(
						companyId,
						companyName,
						billNumber,
						bsDate,
						adDate,
						items.ToList(),
						discountPercent);
			}
			catch (Exception ex)
			{
				pdfError =
					ex.Message;
			}

			await ShowSaleSavedDialogAsync(
				billNumber,
				pdfPath,
				pdfError);

			if (Frame?.CanGoBack == true)
			{
				Frame.GoBack();
			}
		}


		// ============================================================
		// SALE SAVED DIALOG
		// ============================================================

		private async Task ShowSaleSavedDialogAsync(
			string billNumber,
			string? pdfPath,
			string pdfError)
		{
			if (XamlRoot == null)
				return;

			string message;

			if (pdfPath != null)
			{
				message =
					$"Bill {billNumber} has been saved successfully.\n\n" +
					$"Bill PDF generated:\n{pdfPath}";
			}
			else
			{
				message =
					$"Bill {billNumber} has been saved successfully.\n\n" +
					"The bill PDF could not be generated.\n\n" +
					pdfError;
			}

			ContentDialog dialog =
				new ContentDialog
				{
					Title =
						"Sale Saved",

					Content =
						message,

					PrimaryButtonText =
						pdfPath != null
							? "Open PDF"
							: null,

					CloseButtonText =
						"OK",

					DefaultButton =
						pdfPath != null
							? ContentDialogButton.Primary
							: ContentDialogButton.Close,

					XamlRoot =
						XamlRoot
				};

			ContentDialogResult result =
				await dialog.ShowAsync();

			if (result ==
					ContentDialogResult.Primary &&
				pdfPath != null)
			{
				await TryOpenPdfAsync(
					pdfPath);
			}
		}


		// ============================================================
		// OPEN PDF
		// ============================================================

		private async Task TryOpenPdfAsync(
			string pdfPath)
		{
			try
			{
				Windows.Storage.StorageFile file =
					await Windows.Storage.StorageFile
						.GetFileFromPathAsync(pdfPath);

				_ =
					await Windows.System.Launcher
						.LaunchFileAsync(file);
			}
			catch
			{
				await ShowMessageAsync(
					"Bill PDF",
					$"The PDF was generated but could not be opened.\n\n{pdfPath}");
			}
		}


		// ============================================================
		// BILL EXISTS
		// ============================================================

		private bool BillExists(
			long companyId,
			string billNumber,
			string? fiscalYear)
		{
			object? result =
				DatabaseHelper.ExecuteScalar(
					"""
                    SELECT COUNT(*)
                    FROM Sales
                    WHERE
                        CompanyID = @CompanyID
                        AND BillNumber = @BillNumber
                        AND FiscalYear = @FiscalYear
                    """,
					new Dictionary<string, object>
					{
						["@CompanyID"] =
							companyId,

						["@BillNumber"] =
							$"Bill {billNumber}",

						["@FiscalYear"] =
							fiscalYear ?? (object)DBNull.Value
					});

			if (result == null ||
				result == DBNull.Value)
			{
				return false;
			}

			return Convert.ToInt32(result) > 0;
		}


		// ============================================================
		// COMPANY ID
		// ============================================================

		private long GetCompanyId(
			string companyName)
		{
			object? result =
				DatabaseHelper.ExecuteScalar(
					"""
                    SELECT CompanyID
                    FROM Company
                    WHERE CompanyName = @CompanyName
                    LIMIT 1
                    """,
					new Dictionary<string, object>
					{
						["@CompanyName"] =
							companyName
					});

			if (result == null ||
				result == DBNull.Value)
			{
				return -1;
			}

			return Convert.ToInt64(result);
		}


		// ============================================================
		// STOCK USING TRANSACTION CONNECTION
		// ============================================================

		private decimal GetStock(
			System.Data.SQLite.SQLiteConnection connection,
			System.Data.SQLite.SQLiteTransaction transaction,
			long productId)
		{
			const string query =
				"""
                SELECT
                    TotalStockLeft
                FROM Inventory
                WHERE ProductID = @ProductID
                """;

			using var command =
				new System.Data.SQLite.SQLiteCommand(
					query,
					connection,
					transaction);

			command.Parameters.AddWithValue(
				"@ProductID",
				productId);

			object? result =
				command.ExecuteScalar();

			if (result == null ||
				result == DBNull.Value)
			{
				return 0m;
			}

			return ToDecimal(result);
		}


		// ============================================================
		// GRAND TOTAL
		// ============================================================

		private decimal CalculateGrandTotal(
			decimal subtotal,
			decimal totalDiscount)
		{
			decimal vat =
				CalculateVatAfterDiscount(
					subtotal,
					totalDiscount);

			return
				subtotal -
				totalDiscount +
				vat;
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
	}
}