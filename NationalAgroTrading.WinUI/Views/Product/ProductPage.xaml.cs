using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using NationalAgroTrading.WinUI.Data;

namespace NationalAgroTrading.WinUI.Views.Product
{
	public sealed partial class ProductPage : Page
	{
		private DataTable? productData;

		private readonly ObservableCollection<ProductItem> products =
			new();

		public ProductPage()
		{
			InitializeComponent();

			ProductListView.ItemsSource = products;

			LoadProducts();
		}

		private void LoadProducts()
		{
			const string query = @"
                SELECT
                    ProductID,
                    ProductName,
                    CostPrice,
                    RetailPrice,
                    WholesalePrice,
                    IsVattable,
                    HSCode
                FROM Product
                ORDER BY ProductName";

			try
			{
				productData =
					DatabaseHelper.GetData(
						query,
						new Dictionary<string, object>());

				DisplayProducts(productData.Select());
			}
			catch (Exception ex)
			{
				_ = ShowMessageAsync(
					"Unable to load products.\n\n" + ex.Message,
					"Product Error");
			}
		}

		private void DisplayProducts(DataRow[] rows)
		{
			products.Clear();

			foreach (DataRow row in rows)
			{
				products.Add(
					new ProductItem
					{
						ProductID =
							Convert.ToInt64(row["ProductID"]),

						ProductName =
							row["ProductName"]?.ToString()
							?? string.Empty,

						CostPrice =
							GetDecimal(row["CostPrice"]),

						RetailPrice =
							GetDecimal(row["RetailPrice"]),

						WholesalePrice =
							GetDecimal(row["WholesalePrice"]),

						IsVattable =
							Convert.ToInt64(row["IsVattable"]) != 0,

						HSCode =
							row["HSCode"] == DBNull.Value
								? string.Empty
								: row["HSCode"]?.ToString()
								  ?? string.Empty
					});
			}
		}

		private static decimal GetDecimal(object value)
		{
			if (value == DBNull.Value || value == null)
				return 0m;

			return Convert.ToDecimal(value);
		}

		private void SearchTextBox_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			if (productData == null)
				return;

			string search =
				SearchTextBox.Text
					.Trim()
					.Replace("'", "''");

			if (string.IsNullOrWhiteSpace(search))
			{
				DisplayProducts(productData.Select());
				return;
			}

			DataRow[] rows =
				productData.Select(
					$"[ProductName] LIKE '%{search}%'");

			DisplayProducts(rows);
		}

		private void AddProductButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			Frame.Navigate(typeof(AddProductPage));
		}

		private void ProductListView_DoubleTapped(
			object sender,
			DoubleTappedRoutedEventArgs e)
		{
			if (ProductListView.SelectedItem
				is not ProductItem product)
			{
				return;
			}

			Frame.Navigate(
				typeof(UpdateProductPage),
				product.ProductID);
		}

		protected override void OnNavigatedTo(
			Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			LoadProducts();
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

	public sealed class ProductItem
	{
		public long ProductID { get; set; }

		public string ProductName { get; set; } =
			string.Empty;

		public decimal CostPrice { get; set; }

		public decimal RetailPrice { get; set; }

		public decimal WholesalePrice { get; set; }

		public bool IsVattable { get; set; }

		public string VatText =>
			IsVattable ? "Yes" : "No";

		public string HSCode { get; set; } =
			string.Empty;
	}
}