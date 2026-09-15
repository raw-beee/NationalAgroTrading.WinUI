using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace NationalAgroTrading.WinUI.Views.Purchase
{
	public sealed partial class PurchasePage : Page
	{
		private readonly ObservableCollection<PurchaseCompanyItem> companies = new();

		private readonly ObservableCollection<PurchaseBillItem> bills = new();

		private DataTable? companyData;

		private long selectedCompanyId = -1;

		public PurchasePage()
		{
			InitializeComponent();

			CompanyListView.ItemsSource = companies;
			BillListView.ItemsSource = bills;

			LoadCompanies();
		}

		// ============================================================
		// COMPANIES
		// ============================================================

		private void LoadCompanies()
		{
			try
			{
				const string query = @"
                    SELECT
                        c.CompanyID,
                        c.CompanyName,

                        COUNT(DISTINCT p.BillNumber)
                            AS PurchaseCount,

                        COALESCE(
                            SUM(
                                COALESCE(
                                    p.TotalAmountWithVat,
                                    p.TotalPurchaseAfterDiscount,
                                    p.TotalPurchasePrice
                                )
                            ),
                            0
                        ) AS TotalAmount

                    FROM Company c

                    INNER JOIN Purchase p
                        ON p.CompanyID = c.CompanyID

                    GROUP BY
                        c.CompanyID,
                        c.CompanyName

                    ORDER BY
                        c.CompanyName COLLATE NOCASE ASC";

				companyData =
					DatabaseHelper.GetData(
						query,
						new Dictionary<string, object>());

				DisplayCompanies(companyData);
			}
			catch (Exception ex)
			{
				_ = ShowMessageAsync(
					"Unable to load purchase companies.\n\n" +
					ex.Message,
					"Purchase Error");
			}
		}

		private void DisplayCompanies(DataTable table)
		{
			companies.Clear();

			foreach (DataRow row in table.Rows)
			{
				companies.Add(
					new PurchaseCompanyItem
					{
						CompanyID =
							Convert.ToInt64(
								row["CompanyID"]),

						CompanyName =
							row["CompanyName"]?.ToString()
							?? string.Empty,

						PurchaseCount =
							Convert.ToInt32(
								row["PurchaseCount"]),

						TotalAmount =
							ToDecimal(
								row["TotalAmount"])
					});
			}
		}

		// ============================================================
		// COMPANY SEARCH
		// ============================================================

		private void SearchTextBox_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			if (companyData == null)
				return;

			string search =
				SearchTextBox.Text.Trim();

			if (string.IsNullOrWhiteSpace(search))
			{
				DisplayCompanies(companyData);
				return;
			}

			string escapedSearch =
				search.Replace("'", "''");

			DataRow[] rows =
				companyData.Select(
					$"CompanyName LIKE '%{escapedSearch}%'");

			DataTable filtered =
				companyData.Clone();

			foreach (DataRow row in rows)
			{
				filtered.ImportRow(row);
			}

			DisplayCompanies(filtered);
		}

		// ============================================================
		// COMPANY SELECTED
		// ============================================================

		private void CompanyListView_SelectionChanged(
			object sender,
			SelectionChangedEventArgs e)
		{
			if (CompanyListView.SelectedItem
				is not PurchaseCompanyItem company)
			{
				return;
			}

			selectedCompanyId =
				company.CompanyID;

			SelectedCompanyTextBlock.Text =
				company.CompanyName;

			LoadBills(company.CompanyID);
		}

		// ============================================================
		// BILLS
		// ============================================================

		private void LoadBills(long companyId)
		{
			try
			{
				const string query = @"
                    SELECT
                        p.CompanyID,
                        c.CompanyName,
                        p.BillNumber,

                        MAX(p.NepaliDate)
                            AS NepaliDate,

                        MAX(p.PurchaseDate)
                            AS PurchaseDate,

                        COUNT(p.PurchaseID)
                            AS ItemCount,

                        COALESCE(
                            SUM(p.TotalPurchasePrice),
                            0
                        ) AS Subtotal,

                        COALESCE(
                            SUM(p.Discount),
                            0
                        ) AS Discount,

                        COALESCE(
                            SUM(p.VATAmount),
                            0
                        ) AS VAT,

                        COALESCE(
                            SUM(
                                COALESCE(
                                    p.TotalAmountWithVat,
                                    p.TotalPurchaseAfterDiscount
                                    + COALESCE(p.VATAmount, 0),
                                    p.TotalPurchasePrice
                                )
                            ),
                            0
                        ) AS Amount

                    FROM Purchase p

                    INNER JOIN Company c
                        ON p.CompanyID = c.CompanyID

                    WHERE p.CompanyID = @CompanyID

                    GROUP BY
                        p.CompanyID,
                        c.CompanyName,
                        p.BillNumber

                    ORDER BY
                        MAX(p.PurchaseDate) DESC,
                        MAX(p.NepaliDate) DESC,
                        p.BillNumber DESC";

				DataTable table =
					DatabaseHelper.GetData(
						query,
						new Dictionary<string, object>
						{
							["@CompanyID"] =
								companyId
						});

				bills.Clear();

				foreach (DataRow row in table.Rows)
				{
					bills.Add(
						new PurchaseBillItem
						{
							CompanyID =
								Convert.ToInt64(
									row["CompanyID"]),

							CompanyName =
								row["CompanyName"]?.ToString()
								?? string.Empty,

							BillNumber =
								row["BillNumber"]?.ToString()
								?? string.Empty,

							NepaliDate =
								row["NepaliDate"]?.ToString()
								?? string.Empty,

							PurchaseDate =
								row["PurchaseDate"]?.ToString()
								?? string.Empty,

							ItemCount =
								Convert.ToInt32(
									row["ItemCount"]),

							Subtotal =
								ToDecimal(
									row["Subtotal"]),

							Discount =
								ToDecimal(
									row["Discount"]),

							VAT =
								ToDecimal(
									row["VAT"]),

							Amount =
								ToDecimal(
									row["Amount"])
						});
				}

				BillCountTextBlock.Text =
					$"{bills.Count} bill(s)";
			}
			catch (Exception ex)
			{
				_ = ShowMessageAsync(
					"Unable to load purchase bills.\n\n" +
					ex.Message,
					"Purchase Error");
			}
		}

		// ============================================================
		// BILL DOUBLE CLICK
		// ============================================================

		private void BillListView_DoubleTapped(
			object sender,
			DoubleTappedRoutedEventArgs e)
		{
			if (BillListView.SelectedItem
				is not PurchaseBillItem bill)
			{
				return;
			}

			Frame.Navigate(
				typeof(BillDetailsPage),
				bill);
		}

		// ============================================================
		// ADD PURCHASE
		// ============================================================

		private void AddPurchaseButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			Frame.Navigate(
				typeof(AddPurchasePage));
		}

		// ============================================================
		// REFRESH AFTER RETURN
		// ============================================================

		protected override void OnNavigatedTo(
			NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			if (e.NavigationMode ==
				NavigationMode.Back)
			{
				LoadCompanies();

				if (selectedCompanyId > 0)
				{
					LoadBills(selectedCompanyId);
				}
			}
		}

		// ============================================================
		// HELPERS
		// ============================================================

		private static decimal ToDecimal(object value)
		{
			if (value == null ||
				value == DBNull.Value)
			{
				return 0m;
			}

			return Convert.ToDecimal(value);
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
					Title = title,
					Content = message,
					CloseButtonText = "OK",
					XamlRoot = XamlRoot
				};

			await dialog.ShowAsync();
		}
	}
}