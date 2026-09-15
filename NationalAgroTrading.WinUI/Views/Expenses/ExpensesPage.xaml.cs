using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NationalAgroTrading.WinUI.Data;
using NepDate;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NationalAgroTrading.WinUI.Views.Expenses
{
	public sealed partial class ExpensesPage : Page
	{
// ============================================================
// FIELDS
// ============================================================

    private readonly ObservableCollection<ExpenseRow> allExpenses =
		new ObservableCollection<ExpenseRow>();

		private readonly ObservableCollection<ExpenseRow> displayedExpenses =
			new ObservableCollection<ExpenseRow>();

		private DataTable? categoryTable;

		private int? editingExpenseId = null;

		private int currentFiscalYearStartBs;

		private bool loadingFiscalYear = false;


		// ============================================================
		// CONSTRUCTOR
		// ============================================================

		public ExpensesPage()
		{
			InitializeComponent();

			InitializePage();
		}


		// ============================================================
		// INITIALIZE PAGE
		// ============================================================

		private void InitializePage()
		{
			LoadCategories();

			LoadFiscalYears();

			cmbType.SelectedIndex = 0;

			dtExpenseDate.SetADDate(DateTime.Today);
			dtFrom.SetADDate(DateTime.Today);
			dtTo.SetADDate(DateTime.Today);

			LoadExpensesForFiscalYear(
				currentFiscalYearStartBs);
		}


		// ============================================================
		// LOAD CATEGORIES
		// ============================================================

		private void LoadCategories()
		{
			try
			{
				const string query = @"
					SELECT
						CategoryID,
						CategoryName,
						CategoryType
					FROM ExpenseCategories
					ORDER BY
						CASE
							WHEN CategoryType = 'Admin' THEN 1
							WHEN CategoryType = 'Home' THEN 2
							ELSE 3
						END,
						CategoryName COLLATE NOCASE
					";

				DataTable table =
					DatabaseHelper.GetData(query);

				var categories =
					new List<ExpenseCategory>();

				foreach (DataRow row in table.Rows)
				{
					if (row["CategoryID"] == DBNull.Value)
						continue;

					categories.Add(
						new ExpenseCategory
						{
							CategoryID =
								Convert.ToInt32(
									row["CategoryID"]),

							CategoryName =
								row["CategoryName"]?.ToString()
								?? string.Empty,

							CategoryType =
								row["CategoryType"]?.ToString()
								?? string.Empty
						});
				}

				if (categories.Count == 0)
				{
					cmbCategory.ItemsSource = null;

					_ = ShowMessageAsync(
						"No expense categories are available.");

					return;
				}

				cmbCategory.ItemsSource =
					categories;

				cmbCategory.DisplayMemberPath =
					nameof(ExpenseCategory.DisplayName);

				cmbCategory.SelectedValuePath =
					nameof(ExpenseCategory.CategoryID);

				cmbCategory.SelectedIndex = 0;
			}
			catch (Exception ex)
			{
				_ = ShowMessageAsync(
					$"Unable to load expense categories.\n\n{ex.Message}");
			}
		}


		// ============================================================
		// LOAD FISCAL YEARS
		// ============================================================

		private void LoadFiscalYears()
		{
			loadingFiscalYear = true;

			try
			{
				cmbFiscalYear.Items.Clear();

				var todayBs =
					NepaliDate.Now;

				int currentNepaliYear =
					todayBs.Year;

				int currentNepaliMonth =
					todayBs.Month;

				for (
					int year = currentNepaliYear - 2;
					year <= currentNepaliYear + 25;
					year++)
				{
					cmbFiscalYear.Items.Add(
						$"{year}-{year + 1}");
				}

				string currentFiscalYear;

				if (currentNepaliMonth >= 4)
				{
					currentFiscalYear =
						$"{currentNepaliYear}-{currentNepaliYear + 1}";
				}
				else
				{
					currentFiscalYear =
						$"{currentNepaliYear - 1}-{currentNepaliYear}";
				}

				int index =
					cmbFiscalYear.Items.IndexOf(
						currentFiscalYear);

				if (index >= 0)
				{
					cmbFiscalYear.SelectedIndex =
						index;
				}
				else if (cmbFiscalYear.Items.Count > 0)
				{
					cmbFiscalYear.SelectedIndex = 0;
				}

				if (cmbFiscalYear.SelectedItem is string selectedFiscalYear)
				{
					string[] parts =
						selectedFiscalYear.Split('-');

					if (parts.Length == 2 &&
						int.TryParse(
							parts[0],
							out int startYear))
					{
						currentFiscalYearStartBs =
							startYear;
					}
				}
			}
			finally
			{
				loadingFiscalYear = false;
			}
		}


		// ============================================================
		// LOAD EXPENSES FOR FISCAL YEAR
		// ============================================================

		private void LoadExpensesForFiscalYear(
			int bsStartYear)
		{
			try
			{
				string fromNepDate =
					$"{bsStartYear}-04-01";

				string toNepDate =
					$"{bsStartYear + 1}-03-31";

				DataTable dt =
					DatabaseHelper.GetExpenses(
						null,
						null,
						fromNepDate,
						toNepDate);

				LoadDataTable(dt);
			}
			catch (Exception ex)
			{
				_ = ShowMessageAsync(
					$"Unable to load expenses.\n\n{ex.Message}");
			}
		}


		// ============================================================
		// LOAD DATATABLE
		// ============================================================

		private void LoadDataTable(
			DataTable dt)
		{
			allExpenses.Clear();

			foreach (DataRow row in dt.Rows)
			{
				if (row["ExpenseID"] == DBNull.Value)
					continue;

				int expenseId =
					Convert.ToInt32(
						row["ExpenseID"]);

				string category =
					row["CategoryName"]?.ToString()
					?? string.Empty;

				decimal amount = 0m;

				if (row["Amount"] != DBNull.Value)
				{
					amount =
						Convert.ToDecimal(
							row["Amount"],
							CultureInfo.InvariantCulture);
				}

				string nepaliDate =
					row["NepaliDate"]?.ToString()
					?? string.Empty;

				string description =
					row["Description"]?.ToString()
					?? string.Empty;

				allExpenses.Add(
					new ExpenseRow
					{
						ExpenseID = expenseId,

						CategoryName = category,

						Amount = amount,

						AmountText =
							amount.ToString(
								"N2",
								CultureInfo.InvariantCulture),

						NepaliDate = nepaliDate,

						Description = description
					});
			}

			ApplySearchAndDisplay();
		}


		// ============================================================
		// SEARCH + DISPLAY
		// ============================================================

		private void ApplySearchAndDisplay()
		{
			displayedExpenses.Clear();

			string search =
				txtSearch?.Text?.Trim()
				?? string.Empty;

			IEnumerable<ExpenseRow> query =
				allExpenses;

			if (!string.IsNullOrWhiteSpace(search))
			{
				string searchLower =
					search.ToLowerInvariant();

				query =
					query.Where(
						x =>
							x.CategoryName
								.ToLowerInvariant()
								.Contains(searchLower)
							||
							x.Description
								.ToLowerInvariant()
								.Contains(searchLower)
							||
							x.NepaliDate
								.ToLowerInvariant()
								.Contains(searchLower));
			}

			foreach (ExpenseRow expense in query)
			{
				displayedExpenses.Add(expense);
			}

			lvExpenses.ItemsSource =
				displayedExpenses;

			UpdateTotal();
		}


		// ============================================================
		// UPDATE TOTAL
		// ============================================================

		private void UpdateTotal()
		{
			decimal total =
				displayedExpenses.Sum(
					x => x.Amount);

			txtTotal.Text =
				total.ToString(
					"N2",
					CultureInfo.InvariantCulture);
		}


		// ============================================================
		// SEARCH TEXT CHANGED
		// ============================================================

		private void TxtSearch_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			ApplySearchAndDisplay();
		}


		// ============================================================
		// ADD / UPDATE EXPENSE
		// ============================================================

		private void BtnAdd_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (cmbCategory.SelectedValue == null)
			{
				_ = ShowMessageAsync(
					"Please select a valid expense category.");

				return;
			}

			if (!int.TryParse(
					cmbCategory.SelectedValue.ToString(),
					out int categoryId))
			{
				_ = ShowMessageAsync(
					"Please select a valid expense category.");

				return;
			}

			if (!decimal.TryParse(
					txtAmount.Text.Trim(),
					NumberStyles.Number,
					CultureInfo.InvariantCulture,
					out decimal amount))
			{
				_ = ShowMessageAsync(
					"Invalid expense amount.");

				txtAmount.Focus(
					Microsoft.UI.Xaml.FocusState.Programmatic);

				return;
			}

			if (amount <= 0)
			{
				_ = ShowMessageAsync(
					"Amount must be greater than zero.");

				txtAmount.Focus(
					Microsoft.UI.Xaml.FocusState.Programmatic);

				return;
			}

			if (!dtExpenseDate.IsDateValid)
			{
				_ = ShowMessageAsync(
					"Please enter a valid Nepali expense date.");

				return;
			}

			DateTime expenseAdDate =
				dtExpenseDate.SelectedADDate;

			string nepaliDate =
				dtExpenseDate.SelectedBSDate;

			string description =
				txtDescription.Text.Trim();

			try
			{
				if (editingExpenseId.HasValue)
				{
					DatabaseHelper.UpdateExpense(
						editingExpenseId.Value,
						categoryId,
						amount,
						expenseAdDate,
						nepaliDate,
						description);
				}
				else
				{
					DatabaseHelper.AddExpense(
						categoryId,
						amount,
						expenseAdDate,
						nepaliDate,
						description);
				}

				LoadExpensesForFiscalYear(
					currentFiscalYearStartBs);

				ClearInputs();
			}
			catch (Exception ex)
			{
				_ = ShowMessageAsync(
					$"Unable to save expense.\n\n{ex.Message}");
			}
		}


		// ============================================================
		// EDIT EXPENSE
		// ============================================================

		private void EditExpense_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (sender is not Button button)
				return;

			if (button.Tag is not ExpenseRow expense)
				return;

			StartEditingExpense(
				expense.ExpenseID);
		}


		// ============================================================
		// START EDITING EXPENSE
		// ============================================================

		private void StartEditingExpense(
			int expenseId)
		{
			try
			{
				const string query = @"
                SELECT
                    e.ExpenseID,
                    e.CategoryID,
                    e.Amount,
                    e.ExpenseDate,
                    e.NepaliDate,
                    e.Description
                FROM Expenses e
                WHERE e.ExpenseID = @expenseId
                LIMIT 1";

				var parameters =
					new Dictionary<string, object>
					{
					{
						"@expenseId",
						expenseId
					}
					};

				DataTable dt =
					DatabaseHelper.GetData(
						query,
						parameters);

				if (dt.Rows.Count == 0)
				{
					_ = ShowMessageAsync(
						"Expense could not be found.");

					return;
				}

				DataRow row =
					dt.Rows[0];

				editingExpenseId =
					Convert.ToInt32(
						row["ExpenseID"]);

				int categoryId =
					Convert.ToInt32(
						row["CategoryID"]);

				cmbCategory.SelectedValue =
					categoryId;

				if (row["Amount"] != DBNull.Value)
				{
					decimal amount =
						Convert.ToDecimal(
							row["Amount"],
							CultureInfo.InvariantCulture);

					txtAmount.Text =
						amount.ToString(
							"0.##",
							CultureInfo.InvariantCulture);
				}
				else
				{
					txtAmount.Text = string.Empty;
				}

				txtDescription.Text =
					row["Description"]?.ToString()
					?? string.Empty;

				// ----------------------------------------------------
				// Preserve the existing stored date.
				// ----------------------------------------------------

				if (row["ExpenseDate"] != DBNull.Value)
				{
					if (DateTime.TryParse(
							row["ExpenseDate"].ToString(),
							CultureInfo.InvariantCulture,
							DateTimeStyles.None,
							out DateTime expenseDate))
					{
						dtExpenseDate.SetADDate(
							expenseDate);
					}
				}
				else
				{
					string bsDate =
						row["NepaliDate"]?.ToString()
						?? string.Empty;

					TrySetNepaliDate(
						dtExpenseDate,
						bsDate);
				}

				btnAdd.Content =
					"Update";

				txtEntryTitle.Text =
					"Update Expense";

				btnCancelEdit.Visibility =
					Visibility.Visible;
			}
			catch (Exception ex)
			{
				_ = ShowMessageAsync(
					$"Unable to load expense.\n\n{ex.Message}");
			}
		}


		// ============================================================
		// SET NEPALI DATE
		// ============================================================

		private static bool TrySetNepaliDate(
			NationalAgroTrading.WinUI.Controls.NepaliDatePicker picker,
			string bsDate)
		{
			if (string.IsNullOrWhiteSpace(bsDate))
				return false;

			string[] parts =
				bsDate.Split(
					'-',
					StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length != 3)
				return false;

			if (!int.TryParse(
					parts[0],
					out int year))
				return false;

			if (!int.TryParse(
					parts[1],
					out int month))
				return false;

			if (!int.TryParse(
					parts[2],
					out int day))
				return false;

			picker.SetBSDate(
				year,
				month,
				day);

			return true;
		}


		// ============================================================
		// CANCEL EDIT
		// ============================================================

		private void BtnCancelEdit_Click(
			object sender,
			RoutedEventArgs e)
		{
			ClearInputs();
		}


		// ============================================================
		// DELETE EXPENSE
		// ============================================================

		private async void DeleteExpense_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (sender is not Button button)
				return;

			if (button.Tag is not ExpenseRow expense)
				return;

			ContentDialog dialog =
				new ContentDialog
				{
					Title = "Confirm Delete",

					Content =
						"Are you sure you want to delete this expense?",

					PrimaryButtonText = "Delete",

					CloseButtonText = "Cancel",

					DefaultButton =
						ContentDialogButton.Close,

					XamlRoot = XamlRoot
				};

			ContentDialogResult result =
				await dialog.ShowAsync();

			if (result !=
				ContentDialogResult.Primary)
			{
				return;
			}

			try
			{
				DatabaseHelper.DeleteExpense(
					expense.ExpenseID);

				LoadExpensesForFiscalYear(
					currentFiscalYearStartBs);

				if (editingExpenseId ==
					expense.ExpenseID)
				{
					ClearInputs();
				}
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					$"Unable to delete expense.\n\n{ex.Message}");
			}
		}


		// ============================================================
		// CLEAR INPUTS
		// ============================================================

		private void ClearInputs()
		{
			txtAmount.Text =
				string.Empty;

			txtDescription.Text =
				string.Empty;

			dtExpenseDate.SetADDate(
				DateTime.Today);

			if (cmbCategory.Items.Count > 0)
			{
				cmbCategory.SelectedIndex = 0;
			}

			editingExpenseId = null;

			btnAdd.Content =
				"Add";

			txtEntryTitle.Text =
				"Add Expense";

			btnCancelEdit.Visibility =
				Visibility.Collapsed;
		}


		// ============================================================
		// PREVIOUS FISCAL YEAR
		// ============================================================

		private void BtnPrevious_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (cmbFiscalYear.SelectedIndex > 0)
			{
				cmbFiscalYear.SelectedIndex--;
			}
		}


		// ============================================================
		// NEXT FISCAL YEAR
		// ============================================================

		private void BtnNext_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (cmbFiscalYear.SelectedIndex <
				cmbFiscalYear.Items.Count - 1)
			{
				cmbFiscalYear.SelectedIndex++;
			}
		}


		// ============================================================
		// FISCAL YEAR CHANGED
		// ============================================================

		private void CmbFiscalYear_SelectionChanged(
			object sender,
			SelectionChangedEventArgs e)
		{
			if (loadingFiscalYear)
				return;

			if (cmbFiscalYear.SelectedItem
				is not string fiscalYear)
			{
				return;
			}

			string[] parts =
				fiscalYear.Split('-');

			if (parts.Length != 2)
				return;

			if (!int.TryParse(
					parts[0],
					out int startYear))
			{
				return;
			}

			currentFiscalYearStartBs =
				startYear;

			LoadExpensesForFiscalYear(
				currentFiscalYearStartBs);
		}


		// ============================================================
		// TYPE CHANGED
		// ============================================================

		private void CmbType_SelectionChanged(
			object sender,
			SelectionChangedEventArgs e)
		{
			// Filtering is performed when the
			// Filter button is pressed.
		}


		// ============================================================
		// FILTER CHECKBOX
		// ============================================================

		private void FilterCheckBox_Changed(
			object sender,
			RoutedEventArgs e)
		{
			// Filtering is performed when the
			// Filter button is pressed.
		}


		// ============================================================
		// FILTER
		// ============================================================

		private void BtnFilter_Click(
			object sender,
			RoutedEventArgs e)
		{
			string? type = null;

			if (cmbType.SelectedItem
				is ComboBoxItem item)
			{
				string selectedType =
					item.Content?.ToString()
					?? "All";

				if (selectedType != "All")
				{
					type = selectedType;
				}
			}

			string? fromNepDate =
				chkFrom.IsChecked == true
					? dtFrom.SelectedBSDate
					: null;

			string? toNepDate =
				chkTo.IsChecked == true
					? dtTo.SelectedBSDate
					: null;

			try
			{
				DataTable dt =
					DatabaseHelper.GetExpenses(
						null,
						type,
						fromNepDate,
						toNepDate);

				LoadDataTable(dt);
			}
			catch (Exception ex)
			{
				_ = ShowMessageAsync(
					$"Unable to filter expenses.\n\n{ex.Message}");
			}
		}


		// ============================================================
		// EXCEL EXPORT
		// ============================================================

		private async void BtnExcel_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (displayedExpenses.Count == 0)
			{
				await ShowMessageAsync(
					"No data to export.");

				return;
			}

			try
			{
				FileSavePicker picker =
					new FileSavePicker
					{
						SuggestedStartLocation =
							PickerLocationId.DocumentsLibrary,

						SuggestedFileName =
							"ExpensesReport.xlsx"
					};

				picker.FileTypeChoices.Add(
					"Excel Files",
					new List<string>
					{
					".xlsx"
					});

				IntPtr hwnd =
					GetMainWindowHandle();

				if (hwnd == IntPtr.Zero)
				{
					await ShowMessageAsync(
						"Unable to access the application window for Excel export.");

					return;
				}

				InitializeWithWindow.Initialize(
					picker,
					hwnd);

				var file =
					await picker.PickSaveFileAsync();

				if (file == null)
					return;

				ExportToExcel(
					file.Path);

				await ShowMessageAsync(
					"Excel exported successfully!");
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					$"Unable to export Excel file.\n\n{ex.Message}");
			}
		}


		// ============================================================
		// GET MAIN WINDOW HANDLE
		// ============================================================
		//
		// This intentionally does NOT require:
		//
		//     App.MainWindow
		//
		// so it works with the existing App.xaml.cs structure.
		//
		// ============================================================

		private static IntPtr GetMainWindowHandle()
		{
			try
			{
				object app =
					Application.Current;

				Type appType =
					app.GetType();

				// ----------------------------------------------------
				// Look for a public/private property containing Window.
				// ----------------------------------------------------

				PropertyInfo[] properties =
					appType.GetProperties(
						BindingFlags.Instance |
						BindingFlags.Public |
						BindingFlags.NonPublic);

				foreach (PropertyInfo property
					in properties)
				{
					if (!typeof(Window)
						.IsAssignableFrom(property.PropertyType))
					{
						continue;
					}

					object? value =
						property.GetValue(app);

					if (value is Window window)
					{
						return WindowNative.GetWindowHandle(
							window);
					}
				}

				// ----------------------------------------------------
				// Look for a private/public field containing Window.
				// Standard WinUI templates commonly use m_window
				// or _window.
				// ----------------------------------------------------

				FieldInfo[] fields =
					appType.GetFields(
						BindingFlags.Instance |
						BindingFlags.Public |
						BindingFlags.NonPublic);

				foreach (FieldInfo field
					in fields)
				{
					if (!typeof(Window)
						.IsAssignableFrom(field.FieldType))
					{
						continue;
					}

					object? value =
						field.GetValue(app);

					if (value is Window window)
					{
						return WindowNative.GetWindowHandle(
							window);
					}
				}
			}
			catch
			{
				// Return zero below.
			}

			return IntPtr.Zero;
		}


		// ============================================================
		// EXPORT TO EXCEL
		// ============================================================

		private void ExportToExcel(
			string filePath)
		{
			using var workbook =
				new ClosedXML.Excel.XLWorkbook();

			var worksheet =
				workbook.Worksheets.Add(
					"Expenses");

			worksheet.Cell(
				1,
				1).Value =
				"ExpenseID";

			worksheet.Cell(
				1,
				2).Value =
				"CategoryName";

			worksheet.Cell(
				1,
				3).Value =
				"Amount";

			worksheet.Cell(
				1,
				4).Value =
				"NepaliDate";

			worksheet.Cell(
				1,
				5).Value =
				"Description";

			int rowNumber = 2;

			foreach (ExpenseRow expense
				in displayedExpenses)
			{
				worksheet.Cell(
					rowNumber,
					1).Value =
					expense.ExpenseID;

				worksheet.Cell(
					rowNumber,
					2).Value =
					expense.CategoryName;

				worksheet.Cell(
					rowNumber,
					3).Value =
					expense.Amount;

				worksheet.Cell(
					rowNumber,
					4).Value =
					expense.NepaliDate;

				worksheet.Cell(
					rowNumber,
					5).Value =
					expense.Description;

				rowNumber++;
			}

			int totalRow =
				rowNumber;

			worksheet.Cell(
				totalRow,
				2).Value =
				"Total";

			worksheet.Cell(
				totalRow,
				3).Value =
				displayedExpenses.Sum(
					x => x.Amount);

			worksheet.Columns()
				.AdjustToContents();

			workbook.SaveAs(
				filePath);
		}


		// ============================================================
		// PDF EXPORT
		// ============================================================

		private async void BtnPDF_Click(
			object sender,
			RoutedEventArgs e)
		{
			await ShowMessageAsync(
				"The Expenses PDF exporter has not been migrated yet. " +
				"Please provide the existing WinForms ExpensesPdfExporter.cs " +
				"so its original report logic can be preserved.");
		}


		// ============================================================
		// SHOW MESSAGE
		// ============================================================

		private async Task ShowMessageAsync(
			string message)
		{
			if (XamlRoot == null)
				return;

			ContentDialog dialog =
				new ContentDialog
				{
					Title = "Expenses",

					Content = message,

					CloseButtonText = "OK",

					XamlRoot = XamlRoot
				};

			await dialog.ShowAsync();
		}


		// ============================================================
		// EXPENSE ROW
		// ============================================================

		private sealed class ExpenseRow
		{
			public int ExpenseID
			{
				get;
				set;
			}

			public string CategoryName
			{
				get;
				set;
			} = string.Empty;

			public decimal Amount
			{
				get;
				set;
			}

			public string AmountText
			{
				get;
				set;
			} = string.Empty;

			public string NepaliDate
			{
				get;
				set;
			} = string.Empty;

			public string Description
			{
				get;
				set;
			} = string.Empty;
		}
		private sealed class ExpenseCategory
		{
			public int CategoryID
			{
				get;
				set;
			}

			public string CategoryName
			{
				get;
				set;
			} = string.Empty;

			public string CategoryType
			{
				get;
				set;
			} = string.Empty;

			public string DisplayName
			{
				get
				{
					if (string.IsNullOrWhiteSpace(CategoryType))
						return CategoryName;

					return $"{CategoryName} ({CategoryType})";
				}
			}
		}
	}
}
