using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace NationalAgroTrading.WinUI.Views.Dashboard
{
	public sealed partial class DashboardPage : Page
	{
		private int _currentFiscalYear;

		public DashboardPage()
		{
			InitializeComponent();

			_currentFiscalYear = GetCurrentFiscalYearStart();

			UpdateFiscalYearDisplay();
			UpdatePlaceholderDashboard();
			UpdateNepaliDateDisplay();
		}

		// ============================================================
		// FISCAL YEAR
		// ============================================================

		private int GetCurrentFiscalYearStart()
		{
			/*
             * Existing WinForms rule:
             *
             * BS month >= 4
             *      current BS year is fiscal-year start
             *
             * BS month < 4
             *      previous BS year is fiscal-year start
             *
             * Example:
             *
             * 2082/04/01 -> FY 2082/2083
             * 2082/03/31 -> FY 2081/2082
             *
             * The actual Nepali date should eventually come from
             * the existing NepDate implementation.
             */

			// Temporary Phase 7 placeholder.
			// Replace only the date source when the reusable
			// NepaliDatePicker/date service is migrated.

			int nepaliYear = 2082;
			int nepaliMonth = 5;

			return nepaliMonth >= 4
				? nepaliYear
				: nepaliYear - 1;
		}


		private void UpdateFiscalYearDisplay()
		{
			int fiscalYearEnd = _currentFiscalYear + 1;

			FiscalYearTextBlock.Text =
				$"{_currentFiscalYear}/{fiscalYearEnd}";

			FiscalYearDateRangeTextBlock.Text =
				$"{_currentFiscalYear}/04/01 - {fiscalYearEnd}/03/31";

			ChartFiscalYearTextBlock.Text =
				$"{_currentFiscalYear}/{fiscalYearEnd}";
		}


		private void PreviousFiscalYearButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			_currentFiscalYear--;

			UpdateFiscalYearDisplay();

			// Real chart/database loading will be added later.
			RefreshFiscalYearPlaceholder();
		}


		private void NextFiscalYearButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			_currentFiscalYear++;

			UpdateFiscalYearDisplay();

			// Real chart/database loading will be added later.
			RefreshFiscalYearPlaceholder();
		}


		private void RefreshFiscalYearPlaceholder()
		{
			ProfitLossStatusTextBlock.Text =
				$"Selected fiscal year: {_currentFiscalYear}/{_currentFiscalYear + 1}";
		}


		// ============================================================
		// PLACEHOLDER DASHBOARD DATA
		// ============================================================

		private void UpdatePlaceholderDashboard()
		{
			TodaySalesTextBlock.Text = "Rs. 0.00";
			MonthlySalesTextBlock.Text = "Rs. 0.00";

			TodayPurchaseTextBlock.Text = "Rs. 0.00";
			MonthlyPurchaseTextBlock.Text = "Rs. 0.00";

			TodayExpenseTextBlock.Text = "Rs. 0.00";
			MonthlyExpenseTextBlock.Text = "Rs. 0.00";

			ProfitLossTextBlock.Text = "Rs. 0.00";

			ProfitLossStatusTextBlock.Text =
				$"Selected fiscal year: {_currentFiscalYear}/{_currentFiscalYear + 1}";
		}


		// ============================================================
		// NEPALI DATE DISPLAY
		// ============================================================

		private void UpdateNepaliDateDisplay()
		{
			/*
             * Do not introduce a new Nepali date conversion system here.
             *
             * The WinForms reference uses:
             *
             *     NepaliDate.Now
             *     NepaliDateHelper.GetNepaliMonth(...)
             *     NepaliDateHelper.GetNepaliDayOfWeek(...)
             *
             * Once the existing NepDate implementation is available
             * in the WinUI project, this method should use that same
             * implementation.
             */

			NepaliDateTextBlock.Text =
				"Nepali Date";
		}
	}
}