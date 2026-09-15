using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;

namespace NationalAgroTrading.WinUI.Views.Product
{
	public sealed partial class UpdateProductPage : Page
	{
		private long productId;

		public UpdateProductPage()
		{
			InitializeComponent();
		}

		protected override void OnNavigatedTo(
			NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			if (e.Parameter == null)
				return;

			productId =
				Convert.ToInt64(e.Parameter);

			LoadProduct();
		}

		private void LoadProduct()
		{
			const string query = @"
                SELECT
                    ProductName,
                    CostPrice,
                    RetailPrice,
                    WholesalePrice
                FROM Product
                WHERE ProductID = @ProductID";

			DataTableHelper(query);
		}

		private void DataTableHelper(string query)
		{
			var table =
				DatabaseHelper.GetData(
					query,
					new Dictionary<string, object>
					{
						["@ProductID"] =
							productId
					});

			if (table.Rows.Count == 0)
				return;

			var row = table.Rows[0];

			ProductNameTextBox.Text =
				row["ProductName"]?.ToString()
				?? string.Empty;

			CostPriceTextBox.Text =
				GetDecimal(row["CostPrice"])
					.ToString("0.##");

			RetailPriceTextBox.Text =
				GetDecimal(row["RetailPrice"])
					.ToString("0.##");

			WholesalePriceTextBox.Text =
				GetDecimal(row["WholesalePrice"])
					.ToString("0.##");
		}

		private async void SaveButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (!decimal.TryParse(
					CostPriceTextBox.Text,
					out decimal costPrice))
			{
				await ShowMessageAsync(
					"Invalid cost price.",
					"Validation");
				return;
			}

			if (!decimal.TryParse(
					RetailPriceTextBox.Text,
					out decimal retailPrice))
			{
				await ShowMessageAsync(
					"Invalid retail price.",
					"Validation");
				return;
			}

			if (!decimal.TryParse(
					WholesalePriceTextBox.Text,
					out decimal wholesalePrice))
			{
				await ShowMessageAsync(
					"Invalid wholesale price.",
					"Validation");
				return;
			}

			try
			{
				const string updateQuery = @"
                    UPDATE Product
                    SET
                        CostPrice = @CostPrice,
                        RetailPrice = @RetailPrice,
                        WholesalePrice = @WholesalePrice
                    WHERE ProductID = @ProductID";

				DatabaseHelper.ExecuteQuery(
					updateQuery,
					new Dictionary<string, object>
					{
						["@CostPrice"] =
							costPrice,

						["@RetailPrice"] =
							retailPrice,

						["@WholesalePrice"] =
							wholesalePrice,

						["@ProductID"] =
							productId
					});

				/*
                 * Keep SalesPriceHistory consistent with the
                 * existing application.
                 */

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

				await ShowMessageAsync(
					"Product updated successfully.",
					"Success");

				if (Frame.CanGoBack)
					Frame.GoBack();
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					"Error while updating product:\n\n" +
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
}