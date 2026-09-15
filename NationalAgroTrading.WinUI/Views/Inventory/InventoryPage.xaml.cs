using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using NationalAgroTrading.WinUI.Data;
using NationalAgroTrading.WinUI.Views.Product;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;

namespace NationalAgroTrading.WinUI.Views.Inventory
{
	public sealed partial class InventoryPage : Page
	{
		private DataTable? inventoryData;

		private readonly ObservableCollection<InventoryItem> inventoryItems =
			new();

		public InventoryPage()
		{
			InitializeComponent();

			InventoryListView.ItemsSource =
				inventoryItems;

			LoadInventoryData();
		}

		private void LoadInventoryData()
		{
			/*
             * EXACTLY follows the existing WinForms implementation.
             */

			const string query = @"
                SELECT
                    p.ProductID,
                    p.ProductName,
                    p.CostPrice,
                    p.RetailPrice,
                    p.WholesalePrice,
                    i.InventoryID,
                    i.Quantity,
                    i.TotalStock,
                    i.TotalStockLeft
                FROM Inventory i
                INNER JOIN Product p
                    ON i.ProductID = p.ProductID
                ORDER BY p.ProductName";

			try
			{
				inventoryData =
					DatabaseHelper.GetData(
						query,
						new Dictionary<string, object>());

				DisplayInventory(
					inventoryData.Select());
			}
			catch (Exception ex)
			{
				_ = ShowMessageAsync(
					"Unable to load inventory.\n\n" +
					ex.Message,
					"Inventory Error");
			}
		}

		private void DisplayInventory(
			DataRow[] rows)
		{
			inventoryItems.Clear();

			foreach (DataRow row in rows)
			{
				inventoryItems.Add(
					new InventoryItem
					{
						ProductID =
							Convert.ToInt64(
								row["ProductID"]),

						InventoryID =
							Convert.ToInt64(
								row["InventoryID"]),

						ProductName =
							row["ProductName"]?.ToString()
							?? string.Empty,

						CostPrice =
							GetDecimal(row["CostPrice"]),

						RetailPrice =
							GetDecimal(row["RetailPrice"]),

						WholesalePrice =
							GetDecimal(row["WholesalePrice"]),

						Quantity =
							Convert.ToInt64(
								row["Quantity"]),

						TotalStock =
							Convert.ToInt64(
								row["TotalStock"]),

						TotalStockLeft =
							Convert.ToInt64(
								row["TotalStockLeft"])
					});
			}

			EditInventoryButton.IsEnabled = false;
		}

		private void SearchTextBox_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			if (inventoryData == null)
				return;

			string search =
				SearchTextBox.Text
					.Trim()
					.Replace("'", "''");

			if (string.IsNullOrWhiteSpace(search))
			{
				DisplayInventory(
					inventoryData.Select());

				return;
			}

			DataRow[] rows =
				inventoryData.Select(
					$"[ProductName] LIKE '%{search}%'");

			DisplayInventory(rows);
		}

		private void InventoryListView_SelectionChanged(
			object sender,
			SelectionChangedEventArgs e)
		{
			EditInventoryButton.IsEnabled =
				InventoryListView.SelectedItem
				is InventoryItem;
		}

		private void AddProductButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			Frame.Navigate(
				typeof(AddProductPage));
		}

		private void EditInventoryButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			OpenSelectedInventory();
		}

		private void InventoryListView_DoubleTapped(
			object sender,
			DoubleTappedRoutedEventArgs e)
		{
			OpenSelectedInventory();
		}

		private void OpenSelectedInventory()
		{
			if (InventoryListView.SelectedItem
				is not InventoryItem item)
			{
				return;
			}

			Frame.Navigate(
				typeof(UpdateInventoryPage),
				item);
		}

		protected override void OnNavigatedTo(
			NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			LoadInventoryData();
		}

		private static decimal GetDecimal(
			object value)
		{
			if (value == null ||
				value == DBNull.Value)
				return 0m;

			return Convert.ToDecimal(value);
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

	public sealed class InventoryItem
	{
		public long ProductID { get; set; }

		public long InventoryID { get; set; }

		public string ProductName { get; set; } =
			string.Empty;

		public decimal CostPrice { get; set; }

		public decimal RetailPrice { get; set; }

		public decimal WholesalePrice { get; set; }

		public long Quantity { get; set; }

		public long TotalStock { get; set; }

		public long TotalStockLeft { get; set; }
	}
}