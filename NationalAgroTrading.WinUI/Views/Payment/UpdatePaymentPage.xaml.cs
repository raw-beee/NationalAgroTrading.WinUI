using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace NationalAgroTrading.WinUI.Views.Payment
{
	public sealed partial class UpdatePaymentPage : Page
	{
		private int _paymentId;

		private int _originalCompanyId;
		private string _originalPaymentType = "Received";

		private readonly List<string> _companyNames = new();

		private string _selectedPaymentType = "Received";

		public UpdatePaymentPage()
		{
			InitializeComponent();
		}

		protected override void OnNavigatedTo(
			NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			if (e.Parameter == null)
				return;

			if (int.TryParse(
				e.Parameter.ToString(),
				out int paymentId))
			{
				_paymentId = paymentId;
				LoadPayment();
				LoadCompanyNames();
			}
		}

		private void LoadCompanyNames()
		{
			const string query = @"
                SELECT CompanyName
                FROM Company
                ORDER BY CompanyName ASC";

			DataTable table = DatabaseHelper.GetData(
				query,
				new Dictionary<string, object>());

			_companyNames.Clear();

			foreach (DataRow row in table.Rows)
			{
				string name =
					row["CompanyName"]?.ToString() ?? "";

				if (!string.IsNullOrWhiteSpace(name))
					_companyNames.Add(name);
			}
		}

		private void LoadPayment()
		{
			const string query = @"
                SELECT
                    p.PaymentID,
                    p.CompanyID,
                    c.CompanyName,
                    p.PaymentDate,
                    p.Amount,
                    p.PaymentType,
                    p.Receipt,
                    p.PaymentMethod,
                    p.NepaliDate
                FROM Payment p
                JOIN Company c
                    ON p.CompanyID = c.CompanyID
                WHERE p.PaymentID = @PaymentID
                LIMIT 1";

			DataTable table = DatabaseHelper.GetData(
				query,
				new Dictionary<string, object>
				{
					{ "@PaymentID", _paymentId }
				});

			if (table.Rows.Count == 0)
			{
				ShowError("Payment was not found.");
				return;
			}

			DataRow row = table.Rows[0];

			_originalCompanyId =
				Convert.ToInt32(row["CompanyID"]);

			_originalPaymentType =
				row["PaymentType"]?.ToString()
				?? "Received";

			_selectedPaymentType =
				_originalPaymentType;

			CompanyBox.Text =
				row["CompanyName"]?.ToString() ?? "";

			AmountBox.Value =
				Convert.ToDouble(row["Amount"]);

			ReceiptBox.Text =
				row["Receipt"]?.ToString() ?? "";

			string method =
				row["PaymentMethod"]?.ToString()
				?? "Cash";

			SelectPaymentMethod(method);

			PaymentIdText.Text =
				$"Payment ID: {_paymentId}";

			SetPaymentType(_selectedPaymentType);

			/*
             * The existing NepaliDatePicker remains responsible
             * for the AD/BS date behavior.
             *
             * Do not replace it with a separate date conversion.
             *
             * If your current NepaliDatePicker exposes a date
             * setter, use that existing setter here.
             */
		}

		private void SelectPaymentMethod(string method)
		{
			foreach (object item in PaymentMethodBox.Items)
			{
				if (item is ComboBoxItem comboItem &&
					string.Equals(
						comboItem.Content?.ToString(),
						method,
						StringComparison.OrdinalIgnoreCase))
				{
					PaymentMethodBox.SelectedItem =
						comboItem;

					return;
				}
			}

			PaymentMethodBox.SelectedIndex = 0;
		}

		private void CompanyBox_TextChanged(
			AutoSuggestBox sender,
			AutoSuggestBoxTextChangedEventArgs args)
		{
			if (args.Reason !=
				AutoSuggestionBoxTextChangeReason.UserInput)
				return;

			string text =
				sender.Text.Trim();

			if (string.IsNullOrWhiteSpace(text))
			{
				sender.ItemsSource = null;
				return;
			}

			sender.ItemsSource =
				_companyNames
					.Where(x =>
						x.Contains(
							text,
							StringComparison.OrdinalIgnoreCase))
					.Take(20)
					.ToList();
		}

		private void CompanyBox_SuggestionChosen(
			AutoSuggestBox sender,
			AutoSuggestBoxSuggestionChosenEventArgs args)
		{
			if (args.SelectedItem != null)
				sender.Text =
					args.SelectedItem.ToString();
		}

		private void CompanyBox_QuerySubmitted(
			AutoSuggestBox sender,
			AutoSuggestBoxQuerySubmittedEventArgs args)
		{
			if (args.ChosenSuggestion != null)
				sender.Text =
					args.ChosenSuggestion.ToString();
		}

		private void ReceivedButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			SetPaymentType("Received");
		}

		private void MadeButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			SetPaymentType("Made");
		}

		private void SetPaymentType(string type)
		{
			_selectedPaymentType = type;

			if (type == "Received")
			{
				ReceivedButton.Style =
					(Style)Application.Current.Resources[
						"AppPrimaryButtonStyle"];

				MadeButton.Style =
					(Style)Application.Current.Resources[
						"AppSecondaryButtonStyle"];

				PaymentTypeDescription.Text =
					"Money received from the company";
			}
			else
			{
				MadeButton.Style =
					(Style)Application.Current.Resources[
						"AppPrimaryButtonStyle"];

				ReceivedButton.Style =
					(Style)Application.Current.Resources[
						"AppSecondaryButtonStyle"];

				PaymentTypeDescription.Text =
					"Money paid to the company";
			}
		}

		private async void UpdateButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			string companyName =
				CompanyBox.Text.Trim();

			string receipt =
				ReceiptBox.Text.Trim();

			string paymentMethod =
				(PaymentMethodBox.SelectedItem as ComboBoxItem)
					?.Content?.ToString()
				?? "Cash";

			decimal amount =
				Convert.ToDecimal(AmountBox.Value);

			if (string.IsNullOrWhiteSpace(companyName))
			{
				await ShowValidation(
					"Company is required.");
				return;
			}

			if (amount <= 0)
			{
				await ShowValidation(
					"Amount must be greater than zero.");
				return;
			}

			int companyId =
				DatabaseHelper.GetCompanyIdByName(
					companyName);

			if (companyId <= 0)
			{
				await ShowValidation(
					"Company not found.");
				return;
			}

			try
			{
				/*
                 * We intentionally do not silently invent the
                 * historical ledger-recalculation behavior here.
                 *
                 * The Add operation is fully supported from the
                 * supplied WinForms reference.
                 *
                 * Before enabling Update, the complete
                 * PaymentUpdateForm.cs should be used as the
                 * authoritative reference for how the old
                 * application recalculates Ledger rows.
                 */

				await ShowValidation(
					"Payment loading and editing UI is ready, but the final database update should be connected to the complete WinForms PaymentUpdateForm.cs before changing accounting records.");

			}
			catch (Exception ex)
			{
				await ShowValidation(
					$"Unable to update payment.\n\n{ex.Message}");
			}
		}

		private void CancelButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (Frame.CanGoBack)
				Frame.GoBack();
		}

		private async System.Threading.Tasks.Task ShowValidation(
			string message)
		{
			ContentDialog dialog = new ContentDialog
			{
				Title = "Payment",
				Content = message,
				CloseButtonText = "OK",
				XamlRoot = XamlRoot
			};

			await dialog.ShowAsync();
		}

		private async void ShowError(
			string message)
		{
			ContentDialog dialog = new ContentDialog
			{
				Title = "Payment",
				Content = message,
				CloseButtonText = "OK",
				XamlRoot = XamlRoot
			};

			await dialog.ShowAsync();
		}
	}
}