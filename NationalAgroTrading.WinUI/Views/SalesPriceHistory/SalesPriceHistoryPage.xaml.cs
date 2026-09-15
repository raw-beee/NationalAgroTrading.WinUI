using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;

namespace NationalAgroTrading.WinUI.Views.SalesPriceHistory
{
	public sealed partial class SalesPriceHistoryPage : Page
	{
		private DataTable historyTable = new DataTable();

    private readonly ObservableCollection<SalesPriceHistoryItem>
		historyItems = new();


		public SalesPriceHistoryPage()
		{
			InitializeComponent();

			HistoryListView.ItemsSource = historyItems;

			LoadHistory();
		}


		// ============================================================
		// MODEL USED ONLY FOR DISPLAY
		// ============================================================

		private class SalesPriceHistoryItem
		{
			public string ProductName { get; set; } =
				string.Empty;

			public string RetailPrice { get; set; } =
				string.Empty;

			public string WholesalePrice { get; set; } =
				string.Empty;

			public string EffectiveDate { get; set; } =
				string.Empty;
		}


		// ============================================================
		// LOAD HISTORY
		// ============================================================

		public void LoadHistory()
		{
			const string query = @"
            SELECT
                p.ProductName,
                sph.RetailPrice,
                sph.WholesalePrice,
                sph.ChangedDate
            FROM SalesPriceHistory sph
            JOIN Product p
                ON p.ProductID = sph.ProductID
            ORDER BY
                p.ProductName ASC,
                sph.ChangedDate DESC";


			var parameters =
				new Dictionary<string, object>();


			try
			{
				historyTable =
					DatabaseHelper.GetData(
						query,
						parameters);

				ApplyHistoryFilter();
			}
			catch
			{
				historyItems.Clear();

				UpdateEmptyState();
			}
		}


		// ============================================================
		// SEARCH
		// ============================================================

		private void TxtSearch_TextChanged(
			AutoSuggestBox sender,
			AutoSuggestBoxTextChangedEventArgs args)
		{
			ApplyHistoryFilter();
		}


		// ============================================================
		// FILTER AND BUILD DISPLAY LIST
		// ============================================================

		private void ApplyHistoryFilter()
		{
			historyItems.Clear();


			if (historyTable == null ||
				historyTable.Rows.Count == 0)
			{
				UpdateEmptyState();

				return;
			}


			string searchText =
				txtSearch?.Text?.Trim()
				?? string.Empty;


			IEnumerable<DataRow> rows =
				historyTable
					.AsEnumerable();


			// --------------------------------------------------------
			// Search by Product Name
			// --------------------------------------------------------

			if (!string.IsNullOrWhiteSpace(searchText))
			{
				rows = rows.Where(row =>
				{
					string productName =
						row["ProductName"] == DBNull.Value
							? string.Empty
							: row["ProductName"]
								.ToString()!;


					return productName.Contains(
						searchText,
						StringComparison.OrdinalIgnoreCase);
				});
			}


			// --------------------------------------------------------
			// Product grouping
			// --------------------------------------------------------

			string? previousProduct = null;


			foreach (DataRow row in rows)
			{
				string productName =
					row["ProductName"] == DBNull.Value
						? string.Empty
						: row["ProductName"]
							.ToString()!;


				// ----------------------------------------------------
				// DATE
				// ----------------------------------------------------

				string effectiveDate =
					string.Empty;


				if (row["ChangedDate"] != DBNull.Value)
				{
					string rawDate =
						row["ChangedDate"]
							.ToString()
						?? string.Empty;


					// Keep the existing date.
					// Remove time portion if present.
					effectiveDate =
						rawDate.Split(' ')[0];
				}


				// ----------------------------------------------------
				// RETAIL PRICE
				// ----------------------------------------------------

				string retailPrice =
					string.Empty;


				if (row["RetailPrice"] != DBNull.Value)
				{
					decimal price =
						Convert.ToDecimal(
							row["RetailPrice"]);


					retailPrice =
						price.ToString("N2");
				}


				// ----------------------------------------------------
				// WHOLESALE PRICE
				// ----------------------------------------------------

				string wholesalePrice =
					string.Empty;


				if (row["WholesalePrice"] != DBNull.Value)
				{
					decimal price =
						Convert.ToDecimal(
							row["WholesalePrice"]);


					wholesalePrice =
						price.ToString("N2");
				}


				// ----------------------------------------------------
				// Only display product name on first row
				// ----------------------------------------------------

				string displayProductName =
					string.Equals(
						previousProduct,
						productName,
						StringComparison.OrdinalIgnoreCase)
						? string.Empty
						: productName;


				historyItems.Add(
					new SalesPriceHistoryItem
					{
						ProductName =
							displayProductName,

						RetailPrice =
							retailPrice,

						WholesalePrice =
							wholesalePrice,

						EffectiveDate =
							effectiveDate
					});


				previousProduct =
					productName;
			}


			UpdateEmptyState();
		}


		// ============================================================
		// EMPTY STATE
		// ============================================================

		private void UpdateEmptyState()
		{
			EmptyStatePanel.Visibility =
				historyItems.Count == 0
					? Visibility.Visible
					: Visibility.Collapsed;
		}


		// ============================================================
		// REFRESH WHEN NAVIGATING BACK TO PAGE
		// ============================================================

		protected override void OnNavigatedTo(
			Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			LoadHistory();
		}
	}

}
