using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace NationalAgroTrading.WinUI.Views.Ledger
{
	public sealed partial class OpeningBalancePage : Page
	{
		public OpeningBalancePage()
		{
			InitializeComponent();
		}


		// ============================================================
		// SAVE
		// ============================================================

		private async void SaveButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			string companyName =
				CompanyTextBox.Text.Trim();

			if (string.IsNullOrWhiteSpace(companyName))
			{
				await ShowMessageAsync(
					"Please enter a company name.");

				return;
			}


			// ========================================================
			// COMPANY ID
			// ========================================================

			int companyId =
				DatabaseHelper.GetCompanyIdByName(
					companyName);

			if (companyId == -1)
			{
				await ShowMessageAsync(
					"Company not found.");

				return;
			}


			// ========================================================
			// NEPALI DATE
			// ========================================================

			string nepaliDate =
				OpeningDatePicker.SelectedBSDate;

			if (
				string.IsNullOrWhiteSpace(nepaliDate) ||
				!OpeningDatePicker.IsDateValid)
			{
				await ShowMessageAsync(
					"Please enter a valid Nepali date.");

				return;
			}


			// ========================================================
			// AMOUNT
			// ========================================================

			string amountText =
				AmountTextBox.Text.Trim();

			if (!decimal.TryParse(
					amountText,
					NumberStyles.Number,
					CultureInfo.InvariantCulture,
					out decimal amount)
				|| amount <= 0)
			{
				await ShowMessageAsync(
					"Please enter a valid amount.");

				return;
			}


			// ========================================================
			// DEBIT / CREDIT
			// ========================================================

			decimal finalAmount;

			if (CreditRadioButton.IsChecked == true)
			{
				finalAmount =
					amount;
			}
			else
			{
				finalAmount =
					-amount;
			}


			// ========================================================
			// NOTE
			// ========================================================

			string note =
				NoteTextBox.Text.Trim();


			// ========================================================
			// SAVE
			// ========================================================

			try
			{
				DatabaseHelper.SaveOpeningBalance(
					companyId,
					nepaliDate,
					finalAmount,
					note);

				await ShowMessageAsync(
					"Opening balance saved successfully.");

				ClearForm();
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					$"Error saving opening balance:\n\n{ex.Message}");
			}
		}


		// ============================================================
		// CLOSE
		// ============================================================

		private void CloseButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (Frame != null &&
				Frame.CanGoBack)
			{
				Frame.GoBack();
			}
		}


		// ============================================================
		// CLEAR
		// ============================================================

		private void ClearForm()
		{
			CompanyTextBox.Text =
				string.Empty;

			AmountTextBox.Text =
				string.Empty;

			NoteTextBox.Text =
				string.Empty;

			DebitRadioButton.IsChecked =
				true;
		}


		// ============================================================
		// MESSAGE
		// ============================================================

		private async Task ShowMessageAsync(
			string message)
		{
			ContentDialog dialog =
				new ContentDialog
				{
					Title = "Opening Balance",
					Content = message,
					CloseButtonText = "OK",
					XamlRoot = this.Content.XamlRoot
				};

			await dialog.ShowAsync();
		}
	}
}
