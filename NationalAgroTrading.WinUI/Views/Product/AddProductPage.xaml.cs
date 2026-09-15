using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;

namespace NationalAgroTrading.WinUI.Views.Product
{
	public sealed partial class AddProductPage : Page
	{
		public AddProductPage()
		{
			InitializeComponent();
		}

		private async void SaveButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			string productName =
				ProductNameTextBox.Text.Trim();

			if (string.IsNullOrWhiteSpace(productName))
			{
				await ShowMessageAsync(
					"Product name is required.",
					"Validation");

				return;
			}

			if (!decimal.TryParse(
					CostPriceTextBox.Text.Trim(),
					out decimal costPrice))
			{
				await ShowMessageAsync(
					"Please enter a valid cost price.",
					"Validation");

				return;
			}

			if (!decimal.TryParse(
					RetailPriceTextBox.Text.Trim(),
					out decimal retailPrice))
			{
				await ShowMessageAsync(
					"Please enter a valid retail price.",
					"Validation");

				return;
			}

			if (!decimal.TryParse(
					WholesalePriceTextBox.Text.Trim(),
					out decimal wholesalePrice))
			{
				await ShowMessageAsync(
					"Please enter a valid wholesale price.",
					"Validation");

				return;
			}

			int isVattable =
				VatRadioButtons.SelectedIndex == 1
					? 1
					: 0;

			string hsCode =
				HSCodeTextBox.Text.Trim();

			try
			{
				// Check duplicate product.
				const string duplicateQuery =
					"SELECT COUNT(*) " +
					"FROM Product " +
					"WHERE ProductName = @ProductName";

				int count =
					Convert.ToInt32(
						DatabaseHelper.ExecuteScalar(
							duplicateQuery,
							new Dictionary<string, object>
							{
								["@ProductName"] =
									productName
							}));

				if (count > 0)
				{
					await ShowMessageAsync(
						"Product already exists.",
						"Duplicate Product");

					return;
				}

				// Insert Product.
				const string insertProductQuery = @"
                    INSERT INTO Product
                    (
                        ProductName,
                        CostPrice,
                        RetailPrice,
                        WholesalePrice,
                        IsVattable,
                        HSCode
                    )
                    VALUES
                    (
                        @ProductName,
                        @CostPrice,
                        @RetailPrice,
                        @WholesalePrice,
                        @IsVattable,
                        @HSCode
                    )";

				DatabaseHelper.ExecuteQuery(
					insertProductQuery,
					new Dictionary<string, object>
					{
						["@ProductName"] =
							productName,

						["@CostPrice"] =
							costPrice,

						["@RetailPrice"] =
							retailPrice,

						["@WholesalePrice"] =
							wholesalePrice,

						["@IsVattable"] =
							isVattable,

						["@HSCode"] =
							string.IsNullOrWhiteSpace(hsCode)
								? DBNull.Value
								: hsCode
					});

				// Get ProductID.
				const string getProductIdQuery =
					"SELECT ProductID " +
					"FROM Product " +
					"WHERE ProductName = @ProductName";

				long productId =
					Convert.ToInt64(
						DatabaseHelper.ExecuteScalar(
							getProductIdQuery,
							new Dictionary<string, object>
							{
								["@ProductName"] =
									productName
							}));

				// Add price history.
				const string historyQuery = @"
                    INSERT INTO SalesPriceHistory
                    (
                        ProductID,
                        RetailPrice,
                        WholesalePrice,
                        ChangedDate
                    )
                    VALUES
                    (
                        @ProductID,
                        @RetailPrice,
                        @WholesalePrice,
                        CURRENT_DATE
                    )";

				DatabaseHelper.ExecuteQuery(
					historyQuery,
					new Dictionary<string, object>
					{
						["@ProductID"] =
							productId,

						["@RetailPrice"] =
							retailPrice,

						["@WholesalePrice"] =
							wholesalePrice
					});

				/*
                 * Every Product must have an Inventory row.
                 *
                 * New products start with zero stock.
                 * Opening stock is entered later from
                 * Edit Inventory.
                 */

				const string inventoryQuery = @"
                    INSERT INTO Inventory
                    (
                        InventoryID,
                        ProductID,
                        Quantity,
                        TotalStock,
                        TotalStockLeft
                    )
                    VALUES
                    (
                        @InventoryID,
                        @ProductID,
                        0,
                        0,
                        0
                    )";

				/*
                 * Your schema defines InventoryID as NOT NULL.
                 * We therefore generate a safe ID from the current
                 * maximum value.
                 */

				object? maxInventoryId =
					DatabaseHelper.ExecuteScalar(
						"SELECT COALESCE(MAX(InventoryID), 0) + 1 " +
						"FROM Inventory");

				long inventoryId =
					Convert.ToInt64(maxInventoryId);

				DatabaseHelper.ExecuteQuery(
					inventoryQuery,
					new Dictionary<string, object>
					{
						["@InventoryID"] =
							inventoryId,

						["@ProductID"] =
							productId
					});

				await ShowMessageAsync(
					"Product added successfully.",
					"Success");

				if (Frame.CanGoBack)
				{
					Frame.GoBack();
				}
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					"Error while saving product:\n\n" +
					ex.Message,
					"Database Error");
			}
		}

		private void CancelButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (Frame.CanGoBack)
				Frame.GoBack();
		}

		private async System.Threading.Tasks.Task ShowMessageAsync(
			string message,
			string title)
		{
			var dialog = new ContentDialog
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