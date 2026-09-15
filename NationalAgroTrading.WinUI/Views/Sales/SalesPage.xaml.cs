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

namespace NationalAgroTrading.WinUI.Views.Sales
{
	public sealed partial class SalesPage : Page
	{
		private readonly ObservableCollection<SalesCompanyItem> companies = new();
		private readonly ObservableCollection<SalesBillItem> bills = new();

		private DataTable? companyData;

		private long selectedCompanyId = -1;

		private bool pageLoaded;


		// ============================================================
		// CONSTRUCTOR
		// ============================================================

		public SalesPage()
		{
			InitializeComponent();

			CompanyListView.ItemsSource = companies;
			BillListView.ItemsSource = bills;

			Loaded += SalesPage_Loaded;
		}


		// ============================================================
		// PAGE LOADED
		// ============================================================

		private void SalesPage_Loaded(
			object sender,
			RoutedEventArgs e)
		{
			if (pageLoaded)
				return;

			pageLoaded = true;

			RefreshSalesPage();
		}


		// ============================================================
		// NAVIGATION RETURN
		// ============================================================

		protected override void OnNavigatedTo(
			NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			/*
			 * Whenever we return to SalesPage after adding,
			 * editing or deleting a bill, reload the database.
			 */
			if (pageLoaded)
			{
				RefreshSalesPage();
			}
		}


		// ============================================================
		// REFRESH
		// ============================================================

		private void RefreshSalesPage()
		{
			LoadCompanies();

			if (selectedCompanyId > 0)
			{
				LoadBills(selectedCompanyId);
			}
			else
			{
				ClearBillList();
			}
		}


		// ============================================================
		// COMPANIES
		// ============================================================

		private void LoadCompanies()
		{
			try
			{
				const string query =
					"""
                    SELECT
                        c.CompanyID,
                        c.CompanyName,

                        COUNT(DISTINCT s.BillNumber)
                            AS BillCount,

                        COALESCE(
                            SUM(
                                COALESCE(
                                    s.TotalAmountWithVAT,
                                    s.TotalAmountAfterDiscount,
                                    s.TotalSalesAmount,
                                    0
                                )
                            ),
                            0
                        ) AS TotalAmount

                    FROM Company c

                    INNER JOIN Sales s
                        ON s.CompanyID = c.CompanyID

                    GROUP BY
                        c.CompanyID,
                        c.CompanyName

                    ORDER BY
                        c.CompanyName COLLATE NOCASE ASC
                    """;

				companyData =
					DatabaseHelper.GetData(
						query,
						new Dictionary<string, object>());

				DisplayCompanies(companyData);

				RestoreSelectedCompany();
			}
			catch (Exception ex)
			{
				companyData = null;

				companies.Clear();

				ClearBillList();

				_ = ShowMessageAsync(
					"Unable to load sales companies.\n\n" +
					ex.Message,
					"Sales Error");
			}
		}


		// ============================================================
		// DISPLAY COMPANIES
		// ============================================================

		private void DisplayCompanies(
			DataTable table)
		{
			companies.Clear();

			foreach (DataRow row in table.Rows)
			{
				if (row["CompanyID"] == DBNull.Value)
					continue;

				long companyId =
					Convert.ToInt64(
						row["CompanyID"]);

				string companyName =
					row["CompanyName"] == DBNull.Value
						? string.Empty
						: row["CompanyName"]?.ToString()
							?? string.Empty;

				if (string.IsNullOrWhiteSpace(companyName))
					continue;

				companies.Add(
					new SalesCompanyItem
					{
						CompanyID = companyId,

						CompanyName = companyName,

						BillCount =
							ToInt32(
								row["BillCount"]),

						TotalAmount =
							ToDecimal(
								row["TotalAmount"])
					});
			}

			/*
			 * If the previously selected company no longer has
			 * sales, clear the selection.
			 */
			if (selectedCompanyId > 0 &&
				!companies.Any(
					x => x.CompanyID == selectedCompanyId))
			{
				selectedCompanyId = -1;

				SelectedCompanyTextBlock.Text =
					"No company selected";

				ClearBillList();
			}
		}


		// ============================================================
		// RESTORE SELECTED COMPANY
		// ============================================================

		private void RestoreSelectedCompany()
		{
			if (selectedCompanyId <= 0)
				return;

			SalesCompanyItem? company =
				companies.FirstOrDefault(
					x => x.CompanyID == selectedCompanyId);

			if (company == null)
				return;

			CompanyListView.SelectedItem =
				company;

			SelectedCompanyTextBlock.Text =
				company.CompanyName;
		}


		// ============================================================
		// SEARCH COMPANY
		// ============================================================

		private void SearchTextBox_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			if (companyData == null)
				return;

			string search =
				SearchTextBox.Text?.Trim()
				?? string.Empty;

			if (string.IsNullOrWhiteSpace(search))
			{
				DisplayCompanies(companyData);
				RestoreSelectedCompany();
				return;
			}

			string escaped =
				search.Replace(
					"'",
					"''");

			DataRow[] rows =
				companyData.Select(
					$"CompanyName LIKE '%{escaped}%'");

			DataTable filtered =
				companyData.Clone();

			foreach (DataRow row in rows)
			{
				filtered.ImportRow(row);
			}

			DisplayCompanies(filtered);

			RestoreSelectedCompany();
		}


		// ============================================================
		// COMPANY SELECTION
		// ============================================================

		private void CompanyListView_SelectionChanged(
			object sender,
			SelectionChangedEventArgs e)
		{
			if (CompanyListView.SelectedItem
				is not SalesCompanyItem company)
			{
				selectedCompanyId = -1;

				SelectedCompanyTextBlock.Text =
					"No company selected";

				ClearBillList();

				return;
			}

			selectedCompanyId =
				company.CompanyID;

			SelectedCompanyTextBlock.Text =
				company.CompanyName;

			LoadBills(
				company.CompanyID);
		}


		// ============================================================
		// LOAD BILLS
		// ============================================================

		private void LoadBills(
			long companyId)
		{
			if (companyId <= 0)
			{
				ClearBillList();
				return;
			}

			try
			{
				const string query =
					"""
                    SELECT
                        s.CompanyID,
                        c.CompanyName,
                        s.BillNumber,

                        MAX(s.NepaliDate)
                            AS NepaliDate,

                        MAX(s.SaleDate)
                            AS SaleDate,

                        COUNT(s.SaleID)
                            AS ItemCount,

                        COALESCE(
                            SUM(
                                COALESCE(
                                    s.TotalSalesAmount,
                                    0
                                )
                            ),
                            0
                        ) AS Subtotal,

                        COALESCE(
                            SUM(
                                COALESCE(
                                    s.Discount,
                                    0
                                )
                            ),
                            0
                        ) AS Discount,

                        COALESCE(
                            SUM(
                                COALESCE(
                                    s.VATAmount,
                                    0
                                )
                            ),
                            0
                        ) AS VAT,

                        COALESCE(
                            SUM(
                                COALESCE(
                                    s.TotalAmountWithVAT,
                                    s.TotalAmountAfterDiscount
                                        + COALESCE(
                                            s.VATAmount,
                                            0
                                        ),
                                    s.TotalSalesAmount,
                                    0
                                )
                            ),
                            0
                        ) AS Amount

                    FROM Sales s

                    INNER JOIN Company c
                        ON s.CompanyID = c.CompanyID

                    WHERE
                        s.CompanyID = @CompanyID

                    GROUP BY
                        s.CompanyID,
                        c.CompanyName,
                        s.BillNumber

                    ORDER BY
                        MAX(s.SaleDate) DESC,
                        MAX(s.NepaliDate) DESC,
                        s.BillNumber DESC
                    """;

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
						new SalesBillItem
						{
							CompanyID =
								ToInt64(
									row["CompanyID"]),

							CompanyName =
								ToStringValue(
									row["CompanyName"]),

							BillNumber =
								ToStringValue(
									row["BillNumber"]),

							NepaliDate =
								ToStringValue(
									row["NepaliDate"]),

							SaleDate =
								ToStringValue(
									row["SaleDate"]),

							ItemCount =
								ToInt32(
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

				UpdateBillSummary();
			}
			catch (Exception ex)
			{
				ClearBillList();

				_ = ShowMessageAsync(
					"Unable to load sales bills.\n\n" +
					ex.Message,
					"Sales Error");
			}
		}


		// ============================================================
		// CLEAR BILL LIST
		// ============================================================

		private void ClearBillList()
		{
			bills.Clear();

			UpdateBillSummary();
		}


		// ============================================================
		// BILL SUMMARY
		// ============================================================

		private void UpdateBillSummary()
		{
			BillCountTextBlock.Text =
				$"{bills.Count} bill(s)";
		}


		// ============================================================
		// BILL DOUBLE CLICK → UPDATE
		// ============================================================

		private void BillListView_DoubleTapped(
			object sender,
			DoubleTappedRoutedEventArgs e)
		{
			if (BillListView.SelectedItem
				is not SalesBillItem bill)
			{
				return;
			}

			Frame.Navigate(
				typeof(BillDetailsPage),
				bill);
		}


		// ============================================================
		// BILL RIGHT CLICK → DETAILS
		// ============================================================

		private void BillListView_RightTapped(
			object sender,
			RightTappedRoutedEventArgs e)
		{
			/*
			 * RightTapped does not always update ListView selection
			 * before this event fires.
			 *
			 * Try to determine the clicked item from the pointer
			 * position first, then fall back to the selected item.
			 */

			SalesBillItem? bill =
				BillListView.SelectedItem
				as SalesBillItem;

			if (bill == null)
				return;

			Frame.Navigate(
				typeof(BillDetailsPage),
				bill);
		}


		// ============================================================
		// ADD SALE
		// ============================================================

		private void AddSaleButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			Frame.Navigate(
				typeof(AddSalePage));
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
		// NAVIGATION FROM UPDATED/DELETED BILL
		// ============================================================

		protected override void OnNavigatedFrom(
			NavigationEventArgs e)
		{
			base.OnNavigatedFrom(e);
		}


		// ============================================================
		// SAFE CONVERSION HELPERS
		// ============================================================

		private static string ToStringValue(
			object value)
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
			object value)
		{
			if (value == null ||
				value == DBNull.Value)
			{
				return 0;
			}

			try
			{
				return Convert.ToInt64(value);
			}
			catch
			{
				return 0;
			}
		}


		private static int ToInt32(
			object value)
		{
			if (value == null ||
				value == DBNull.Value)
			{
				return 0;
			}

			try
			{
				return Convert.ToInt32(value);
			}
			catch
			{
				return 0;
			}
		}


		private static decimal ToDecimal(
			object value)
		{
			if (value == null ||
				value == DBNull.Value)
			{
				return 0m;
			}

			try
			{
				return Convert.ToDecimal(value);
			}
			catch
			{
				return 0m;
			}
		}


		// ============================================================
		// MESSAGE
		// ============================================================

		private async Task ShowMessageAsync(
			string message,
			string title)
		{
			if (XamlRoot == null)
				return;

			ContentDialog dialog =
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