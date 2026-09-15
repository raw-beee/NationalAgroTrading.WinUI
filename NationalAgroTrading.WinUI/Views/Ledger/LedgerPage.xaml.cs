using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace NationalAgroTrading.WinUI.Views.Ledger
{
	public sealed partial class LedgerPage : Page
	{
		private readonly ObservableCollection<LedgerRow> ledgerRows =
			new ObservableCollection<LedgerRow>();

		private DataTable? currentLedgerTable;

		private bool isLoadingCompanies;
		private bool isLoadingLedger;


		// ============================================================
		// CONSTRUCTOR
		// ============================================================

		public LedgerPage()
		{
			InitializeComponent();

			LedgerListView.ItemsSource = ledgerRows;

			LoadCompanies();
			LoadFiscalYears();

			UpdateEmptyState();
		}


		// ============================================================
		// LOAD COMPANIES
		// ============================================================

		private void LoadCompanies()
		{
			isLoadingCompanies = true;

			try
			{
				CompanyComboBox.Items.Clear();

				List<string> companies =
					DatabaseHelper.GetCompanyNames();

				foreach (string company in companies)
				{
					if (!string.IsNullOrWhiteSpace(company))
					{
						CompanyComboBox.Items.Add(company);
					}
				}

				if (CompanyComboBox.Items.Count == 1)
				{
					CompanyComboBox.SelectedIndex = 0;
				}
			}
			catch (Exception ex)
			{
				_ = ShowMessageAsync(
					$"Failed to load companies:\n\n{ex.Message}");
			}
			finally
			{
				isLoadingCompanies = false;
			}
		}


		// ============================================================
		// LOAD FISCAL YEARS
		// ============================================================

		private void LoadFiscalYears()
		{
			try
			{
				FiscalYearComboBox.Items.Clear();

				var today = NepDate.NepaliDate.Now;

				int currentYear = today.Year;
				int currentMonth = today.Month;

				for (
					int year = currentYear - 5;
					year <= currentYear + 5;
					year++)
				{
					FiscalYearComboBox.Items.Add(
						$"{year}/{year + 1}");
				}

				string currentFiscalYear;

				if (currentMonth >= 4)
				{
					currentFiscalYear =
						$"{currentYear}/{currentYear + 1}";
				}
				else
				{
					currentFiscalYear =
						$"{currentYear - 1}/{currentYear}";
				}

				for (
					int i = 0;
					i < FiscalYearComboBox.Items.Count;
					i++)
				{
					if (
						FiscalYearComboBox.Items[i]
							?.ToString() ==
						currentFiscalYear)
					{
						FiscalYearComboBox.SelectedIndex = i;
						break;
					}
				}
			}
			catch (Exception ex)
			{
				_ = ShowMessageAsync(
					$"Failed to load fiscal years:\n\n{ex.Message}");
			}
		}


		// ============================================================
		// COMPANY CHANGED
		// ============================================================

		private async void CompanyComboBox_SelectionChanged(
			object sender,
			SelectionChangedEventArgs e)
		{
			if (isLoadingCompanies)
				return;

			await LoadLedgerAsync();
		}


		// ============================================================
		// FISCAL YEAR CHANGED
		// ============================================================

		private async void FiscalYearComboBox_SelectionChanged(
			object sender,
			SelectionChangedEventArgs e)
		{
			await LoadLedgerAsync();
		}


		// ============================================================
		// LOAD LEDGER
		// ============================================================

		private async Task LoadLedgerAsync()
		{
			if (isLoadingLedger)
				return;

			string? companyName =
				CompanyComboBox.SelectedItem?.ToString();

			string? fiscalYear =
				FiscalYearComboBox.SelectedItem?.ToString();

			if (string.IsNullOrWhiteSpace(companyName) ||
				string.IsNullOrWhiteSpace(fiscalYear))
			{
				ledgerRows.Clear();
				currentLedgerTable = null;

				UpdateEmptyState();

				return;
			}

			isLoadingLedger = true;

			try
			{
				int companyId =
					DatabaseHelper.GetCompanyIdByName(
						companyName);

				if (companyId == -1)
				{
					ledgerRows.Clear();
					currentLedgerTable = null;

					UpdateEmptyState();

					await ShowMessageAsync(
						"The selected company could not be found.");

					return;
				}


				// ==================================================
				// FISCAL YEAR
				// ==================================================

				string[] parts =
					fiscalYear.Split('/');

				if (parts.Length != 2)
				{
					await ShowMessageAsync(
						"Invalid fiscal year.");

					return;
				}

				string fromNepaliDate =
					$"{parts[0]}-04-01";

				string toNepaliDate =
					$"{parts[1]}-03-31";


				// ==================================================
				// OPENING BALANCE
				// ==================================================

				decimal openingBalance =
					DatabaseHelper
						.GetOpeningBalanceBeforeNepaliDate(
							companyId,
							fromNepaliDate);


				// ==================================================
				// TRANSACTIONS
				// ==================================================

				DataTable ledgerData =
					GetCombinedLedgerByNepaliDate(
						companyId,
						fromNepaliDate,
						toNepaliDate);


				// ==================================================
				// SORT
				// ==================================================

				DataView view =
					ledgerData.DefaultView;

				view.Sort =
					"Date ASC";

				ledgerData =
					view.ToTable();


				// ==================================================
				// RUNNING BALANCE
				// ==================================================

				DataTable ledgerWithBalance =
					InsertRunningBalanceWithHeaders(
						ledgerData,
						openingBalance);


				currentLedgerTable =
					ledgerWithBalance;


				// ==================================================
				// DISPLAY
				// ==================================================

				ledgerRows.Clear();

				foreach (
					DataRow row
					in ledgerWithBalance.Rows)
				{
					ledgerRows.Add(
						LedgerRow.FromDataRow(row));
				}


				LedgerCompanyTextBlock.Text =
					$"{companyName}  •  FY {fiscalYear}";

				UpdateEmptyState();
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					$"Failed to load ledger:\n\n{ex.Message}");
			}
			finally
			{
				isLoadingLedger = false;
			}
		}


		// ============================================================
		// COMBINED LEDGER
		// ============================================================

		public static DataTable GetCombinedLedgerByNepaliDate(
			int companyId,
			string fromNepaliDate,
			string toNepaliDate)
		{
			string query = @"
                SELECT
                    NepaliDate AS Date,
                    'Purchase Invoice #' || BillNumber AS Particular,
                    SUM(TotalPurchasePrice) AS Debit,
                    0 AS Credit,
                    BillNumber,
                    1 AS OrderType
                FROM Purchase
                WHERE CompanyID = @companyId
                  AND NepaliDate BETWEEN @from AND @to
                GROUP BY BillNumber, NepaliDate

                UNION ALL

                SELECT
                    NepaliDate AS Date,
                    'Discount on Invoice #' || BillNumber AS Particular,
                    0 AS Debit,
                    SUM(Discount) AS Credit,
                    BillNumber,
                    2 AS OrderType
                FROM Purchase
                WHERE CompanyID = @companyId
                  AND NepaliDate BETWEEN @from AND @to
                  AND Discount > 0
                GROUP BY BillNumber, NepaliDate

                UNION ALL

                SELECT
                    NepaliDate AS Date,
                    'Payment Made (ID ' || PaymentID || ')' AS Particular,
                    0 AS Debit,
                    Amount AS Credit,
                    '' AS BillNumber,
                    3 AS OrderType
                FROM Payment
                WHERE CompanyID = @companyId
                  AND NepaliDate BETWEEN @from AND @to
                  AND PaymentType = 'Made'

                UNION ALL

                SELECT
                    NepaliDate AS Date,
                    'Payment Received (ID ' || PaymentID || ')' AS Particular,
                    Amount AS Debit,
                    0 AS Credit,
                    '' AS BillNumber,
                    3 AS OrderType
                FROM Payment
                WHERE CompanyID = @companyId
                  AND NepaliDate BETWEEN @from AND @to
                  AND PaymentType = 'Received'

                UNION ALL

                SELECT
                    NepaliDate AS Date,
                    'Sales Invoice #' || BillNumber AS Particular,
                    0 AS Debit,
                    SUM(TotalSalesAmount) AS Credit,
                    BillNumber,
                    4 AS OrderType
                FROM Sales
                WHERE CompanyID = @companyId
                  AND NepaliDate BETWEEN @from AND @to
                GROUP BY BillNumber, NepaliDate

                UNION ALL

                SELECT
                    NepaliDate AS Date,
                    'Discount for Invoice #' || BillNumber AS Particular,
                    SUM(Discount) AS Debit,
                    0 AS Credit,
                    BillNumber,
                    5 AS OrderType
                FROM Sales
                WHERE CompanyID = @companyId
                  AND NepaliDate BETWEEN @from AND @to
                  AND Discount > 0
                GROUP BY BillNumber, NepaliDate

                ORDER BY Date, BillNumber, OrderType;
            ";

			return DatabaseHelper.GetData(
				query,
				new Dictionary<string, object>
				{
					{
						"@companyId",
						companyId
					},
					{
						"@from",
						fromNepaliDate
					},
					{
						"@to",
						toNepaliDate
					}
				});
		}


		// ============================================================
		// RUNNING BALANCE
		// ============================================================

		public static DataTable InsertRunningBalanceWithHeaders(
			DataTable ledgerData,
			decimal openingBalance)
		{
			if (!ledgerData.Columns.Contains("Balance"))
			{
				ledgerData.Columns.Add(
					"Balance",
					typeof(decimal));
			}

			DataTable result =
				ledgerData.Clone();

			decimal runningBalance =
				openingBalance;

			decimal totalDebit = 0m;
			decimal totalCredit = 0m;


			// ========================================================
			// OPENING BALANCE
			// ========================================================

			DataRow openingRow =
				result.NewRow();

			openingRow["Date"] = "";

			if (openingBalance > 0)
			{
				openingRow["Particular"] =
					"Opening Balance (Cr)";

				openingRow["Credit"] =
					openingBalance;

				openingRow["Debit"] =
					0;
			}
			else if (openingBalance < 0)
			{
				openingRow["Particular"] =
					"Opening Balance (Dr)";

				openingRow["Debit"] =
					Math.Abs(openingBalance);

				openingRow["Credit"] =
					0;
			}
			else
			{
				openingRow["Particular"] =
					"Opening Balance";

				openingRow["Debit"] =
					0;

				openingRow["Credit"] =
					0;
			}

			openingRow["Balance"] =
				Math.Abs(openingBalance);

			result.Rows.Add(openingRow);


			// ========================================================
			// TRANSACTIONS
			// ========================================================

			foreach (DataRow row in ledgerData.Rows)
			{
				decimal debit =
					row["Debit"] != DBNull.Value
						? Convert.ToDecimal(row["Debit"])
						: 0m;

				decimal credit =
					row["Credit"] != DBNull.Value
						? Convert.ToDecimal(row["Credit"])
						: 0m;

				runningBalance +=
					credit - debit;

				totalDebit += debit;
				totalCredit += credit;


				DataRow newRow =
					result.NewRow();

				newRow["Date"] =
					row["Date"];

				newRow["Particular"] =
					row["Particular"];

				newRow["Debit"] =
					debit;

				newRow["Credit"] =
					credit;

				newRow["Balance"] =
					runningBalance;

				result.Rows.Add(newRow);
			}


			// ========================================================
			// TOTAL
			// ========================================================

			DataRow totalRow =
				result.NewRow();

			totalRow["Date"] = "";

			totalRow["Particular"] =
				"Total";

			totalRow["Debit"] =
				totalDebit;

			totalRow["Credit"] =
				totalCredit;

			totalRow["Balance"] =
				runningBalance;

			result.Rows.Add(totalRow);


			// ========================================================
			// CLOSING BALANCE
			// ========================================================

			DataRow closingRow =
				result.NewRow();

			closingRow["Date"] = "";

			if (runningBalance > 0)
			{
				closingRow["Particular"] =
					"Closing Balance (Cr)";

				closingRow["Credit"] =
					runningBalance;

				closingRow["Debit"] =
					0;
			}
			else if (runningBalance < 0)
			{
				closingRow["Particular"] =
					"Closing Balance (Dr)";

				closingRow["Debit"] =
					Math.Abs(runningBalance);

				closingRow["Credit"] =
					0;
			}
			else
			{
				closingRow["Particular"] =
					"Closing Balance";

				closingRow["Debit"] =
					0;

				closingRow["Credit"] =
					0;
			}

			closingRow["Balance"] =
				Math.Abs(runningBalance);

			result.Rows.Add(closingRow);

			return result;
		}


		// ============================================================
		// OPENING BALANCE
		// ============================================================

		private void OpeningBalanceButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			Frame?.Navigate(
				typeof(OpeningBalancePage));
		}


		// ============================================================
		// EXCEL
		// ============================================================

		private async void ExcelButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (!HasLedgerData())
			{
				await ShowMessageAsync(
					"No ledger data available.");

				return;
			}

			try
			{
				string company =
					CompanyComboBox.SelectedItem
						?.ToString()
					?? "Company";

				string fiscalYear =
					FiscalYearComboBox.SelectedItem
						?.ToString()
					?? "FiscalYear";

				FileSavePicker picker =
					new FileSavePicker();

				picker.SuggestedStartLocation =
					PickerLocationId.DocumentsLibrary;

				picker.FileTypeChoices.Add(
					"Excel Files",
					new List<string>
					{
						".xlsx"
					});

				picker.SuggestedFileName =
					$"Ledger_{company}_{fiscalYear}";

				InitializePicker(picker);

				StorageFile? file =
					await picker.PickSaveFileAsync();

				if (file == null)
					return;

				ExcelExporter.ExportDataTableToExcel(
					currentLedgerTable!,
					file.Path);

				await ShowMessageAsync(
					"Ledger exported to Excel successfully.");
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					$"Failed to export Excel:\n\n{ex.Message}");
			}
		}


		// ============================================================
		// PDF
		// ============================================================

		private async void PdfButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (!HasLedgerData())
			{
				await ShowMessageAsync(
					"No ledger data available.");

				return;
			}

			try
			{
				string company =
					CompanyComboBox.SelectedItem
						?.ToString()
					?? "Company";

				string fiscalYear =
					FiscalYearComboBox.SelectedItem
						?.ToString()
					?? "FiscalYear";

				FileSavePicker picker =
					new FileSavePicker();

				picker.SuggestedStartLocation =
					PickerLocationId.DocumentsLibrary;

				picker.FileTypeChoices.Add(
					"PDF Files",
					new List<string>
					{
						".pdf"
					});

				picker.SuggestedFileName =
					$"Ledger_{company}_{fiscalYear}";

				InitializePicker(picker);

				StorageFile? file =
					await picker.PickSaveFileAsync();

				if (file == null)
					return;

				LedgerPdfExporter exporter =
					new LedgerPdfExporter(
						company,
						fiscalYear,
						currentLedgerTable!);

				exporter.ExportToPdf(
					file.Path);

				await ShowMessageAsync(
					"Ledger exported to PDF successfully.");
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					$"Failed to export PDF:\n\n{ex.Message}");
			}
		}


		// ============================================================
		// PRINT
		// ============================================================

		private async void PrintButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (!HasLedgerData())
			{
				await ShowMessageAsync(
					"No ledger data available.");

				return;
			}

			try
			{
				string company =
					CompanyComboBox.SelectedItem
						?.ToString()
					?? "Company";

				string fiscalYear =
					FiscalYearComboBox.SelectedItem
						?.ToString()
					?? "";

				LedgerPrinter printer =
					new LedgerPrinter(
						currentLedgerTable!,
						$"Ledger - {company} - {fiscalYear}");

				printer.Print();
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					$"Printing error:\n\n{ex.Message}");
			}
		}


		// ============================================================
		// FILE PICKER
		// ============================================================

		private void InitializePicker(
			FileSavePicker picker)
		{
			throw new NotImplementedException(
				"Connect the FileSavePicker to your existing MainWindow " +
				"instance before using export.");
		}


		// ============================================================
		// DATA CHECK
		// ============================================================

		private bool HasLedgerData()
		{
			return currentLedgerTable != null &&
				   currentLedgerTable.Rows.Count > 0;
		}


		// ============================================================
		// EMPTY STATE
		// ============================================================

		private void UpdateEmptyState()
		{
			bool hasData =
				ledgerRows.Count > 0;

			EmptyStatePanel.Visibility =
				hasData
					? Visibility.Collapsed
					: Visibility.Visible;
		}


		// ============================================================
		// MESSAGE
		// ============================================================

		private async Task ShowMessageAsync(
			string message)
		{
			if (Content?.XamlRoot == null)
				return;

			ContentDialog dialog =
				new ContentDialog
				{
					Title = "Ledger",
					Content = message,
					CloseButtonText = "OK",
					XamlRoot = Content.XamlRoot
				};

			await dialog.ShowAsync();
		}
	}


	// ================================================================
	// LEDGER ROW
	// ================================================================

	public sealed class LedgerRow
	{
		public string Date { get; set; } = "";

		public string Particular { get; set; } = "";

		public decimal Debit { get; set; }

		public decimal Credit { get; set; }

		public decimal Balance { get; set; }


		public string DebitText =>
			Debit == 0
				? ""
				: Debit.ToString(
					"N2",
					CultureInfo.InvariantCulture);


		public string CreditText =>
			Credit == 0
				? ""
				: Credit.ToString(
					"N2",
					CultureInfo.InvariantCulture);


		public string BalanceText =>
			Balance.ToString(
				"N2",
				CultureInfo.InvariantCulture);


		public static LedgerRow FromDataRow(
			DataRow row)
		{
			decimal debit =
				row["Debit"] != DBNull.Value
					? Convert.ToDecimal(row["Debit"])
					: 0m;

			decimal credit =
				row["Credit"] != DBNull.Value
					? Convert.ToDecimal(row["Credit"])
					: 0m;

			decimal balance =
				row["Balance"] != DBNull.Value
					? Convert.ToDecimal(row["Balance"])
					: 0m;

			return new LedgerRow
			{
				Date =
					row["Date"]?.ToString()
					?? "",

				Particular =
					row["Particular"]?.ToString()
					?? "",

				Debit = debit,

				Credit = credit,

				Balance = balance
			};
		}
	}
}