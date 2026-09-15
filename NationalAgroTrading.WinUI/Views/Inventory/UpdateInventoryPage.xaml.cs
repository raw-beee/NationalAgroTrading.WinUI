using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;

namespace NationalAgroTrading.WinUI.Views.Inventory
{
	public sealed partial class UpdateInventoryPage : Page
	{
		private InventoryItem? inventoryItem;

		public UpdateInventoryPage()
		{
			InitializeComponent();
		}

		protected override void OnNavigatedTo(
			NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			if (e.Parameter is not InventoryItem item)
				return;

			inventoryItem = item;

			ProductNameTextBlock.Text =
				item.ProductName;

			OpeningQuantityTextBox.Text =
				item.Quantity.ToString();

			QuantityTextBox.Text =
				item.Quantity.ToString();

			TotalStockTextBox.Text =
				item.TotalStock.ToString();

			TotalStockLeftTextBox.Text =
				item.TotalStockLeft.ToString();
		}

		private void OpeningQuantityTextBox_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			if (!long.TryParse(
					OpeningQuantityTextBox.Text,
					out long quantity))
			{
				return;
			}

			if (quantity < 0)
				return;

			QuantityTextBox.Text =
				quantity.ToString();

			TotalStockTextBox.Text =
				quantity.ToString();

			TotalStockLeftTextBox.Text =
				quantity.ToString();
		}

		private async void SaveButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (inventoryItem == null)
				return;

			if (!long.TryParse(
					OpeningQuantityTextBox.Text.Trim(),
					out long openingQuantity))
			{
				await ShowMessageAsync(
					"Please enter a valid whole-number quantity.",
					"Validation");

				return;
			}

			if (openingQuantity < 0)
			{
				await ShowMessageAsync(
					"Opening quantity cannot be negative.",
					"Validation");

				return;
			}

			try
			{
				/*
                 * Opening stock is NOT a purchase.
                 * Opening stock is NOT a sale.
                 * Opening stock is NOT a payment.
                 *
                 * We simply establish the starting inventory state.
                 */

				const string query = @"
                    UPDATE Inventory
                    SET
                        Quantity = @Quantity,
                        TotalStock = @TotalStock,
                        TotalStockLeft = @TotalStockLeft
                    WHERE InventoryID = @InventoryID
                      AND ProductID = @ProductID";

				DatabaseHelper.ExecuteQuery(
					query,
					new Dictionary<string, object>
					{
						["@Quantity"] =
							openingQuantity,

						["@TotalStock"] =
							openingQuantity,

						["@TotalStockLeft"] =
							openingQuantity,

						["@InventoryID"] =
							inventoryItem.InventoryID,

						["@ProductID"] =
							inventoryItem.ProductID
					});

				await ShowMessageAsync(
					"Opening inventory saved successfully.",
					"Success");

				if (Frame.CanGoBack)
					Frame.GoBack();
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					"Error while saving inventory:\n\n" +
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