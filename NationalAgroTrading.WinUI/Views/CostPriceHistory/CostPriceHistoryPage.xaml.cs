using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;

namespace NationalAgroTrading.WinUI.Views.CostPriceHistory
{
	public sealed partial class CostPriceHistoryPage : Page
	{
		private DataTable historyTable = new DataTable();

		private readonly ObservableCollection<CostPriceHistoryItem>
			historyItems = new();


		public CostPriceHistoryPage()
		{
			InitializeComponent();

			HistoryListView.ItemsSource = historyItems;

			LoadHistory();
		}


		// ============================================================
		// MODEL USED ONLY FOR DISPLAY
		// ============================================================

		private class CostPriceHistoryItem
		{
			public string ProductName { get; set; } = string.Empty;

			public string CostPrice { get; set; } = string.Empty;

			public string EffectiveDate { get; set; } = string.Empty;
		}


		// ============================================================
		// LOAD HISTORY
		// ============================================================

		public void LoadHistory()
		{
			const string query = @"
                SELECT 
                    p.ProductName,
                    cph.CostPrice,
                    cph.EffectiveDate
                FROM CostPriceHistory cph
                JOIN Product p 
                    ON p.ProductID = cph.ProductID
                ORDER BY 
                    p.ProductName ASC,
                    cph.EffectiveDate DESC";


			var parameters = new Dictionary<string, object>();


			historyTable = DatabaseHelper.GetData(
				query,
				parameters);


			ApplyHistoryFilter();
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
				txtSearch?.Text?.Trim() ?? string.Empty;


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
							: row["ProductName"].ToString()!;


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
						: row["ProductName"].ToString()!;


				string effectiveDate = string.Empty;

				if (row["EffectiveDate"] != DBNull.Value)
				{
					string rawDate = row["EffectiveDate"].ToString() ?? string.Empty;

					// Keep the existing Nepali date.
					// Only remove the time portion if it exists.
					effectiveDate = rawDate.Split(' ')[0];
				}


				string costPrice = string.Empty;


				if (row["CostPrice"] != DBNull.Value)
				{
					decimal price =
						Convert.ToDecimal(row["CostPrice"]);


					costPrice =
						price.ToString("N2");
				}


				// ----------------------------------------------------
				// Only display product name on its first row
				// ----------------------------------------------------

				string displayProductName =
					string.Equals(
						previousProduct,
						productName,
						StringComparison.OrdinalIgnoreCase)
						? string.Empty
						: productName;


				historyItems.Add(
					new CostPriceHistoryItem
					{
						ProductName =
							displayProductName,

						CostPrice =
							costPrice,

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
	}
}