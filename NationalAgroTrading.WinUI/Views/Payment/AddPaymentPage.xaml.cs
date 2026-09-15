using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NationalAgroTrading.WinUI.Controls;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace NationalAgroTrading.WinUI.Views.Payment
{
	public sealed partial class AddPaymentPage : Page
	{
		private readonly List<string> _companyNames = new();

		private string _selectedPaymentType = "Received";

		public AddPaymentPage()
		{
			InitializeComponent();

			LoadCompanyNames();

			Loaded += AddPaymentPage_Loaded;
		}

		private void AddPaymentPage_Loaded(
			object sender,
			RoutedEventArgs e)
		{
			SetPaymentType("Received");
		}

		private void LoadCompanyNames()
		{
			try
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
					string name = row["CompanyName"]?.ToString() ?? "";

					if (!string.IsNullOrWhiteSpace(name))
					{
						_companyNames.Add(name);
					}
				}
			}
			catch (Exception ex)
			{
				ShowError("Unable to load companies.", ex);
			}
		}

		private void CompanyBox_TextChanged(
			AutoSuggestBox sender,
			AutoSuggestBoxTextChangedEventArgs args)
		{
			if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
				return;

			string text = sender.Text.Trim();

			if (string.IsNullOrWhiteSpace(text))
			{
				sender.ItemsSource = null;
				return;
			}

			sender.ItemsSource = _companyNames
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
			{
				sender.Text = args.SelectedItem.ToString();
			}
		}

		private void CompanyBox_QuerySubmitted(
			AutoSuggestBox sender,
			AutoSuggestBoxQuerySubmittedEventArgs args)
		{
			if (args.ChosenSuggestion != null)
			{
				sender.Text = args.ChosenSuggestion.ToString();
			}
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

		private async void SaveButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			ErrorText.Visibility = Visibility.Collapsed;

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
				await ShowValidation("Company is required.");
				return;
			}

			if (amount <= 0)
			{
				await ShowValidation("Amount must be greater than zero.");
				return;
			}

			int companyId =
				DatabaseHelper.GetCompanyIdByName(companyName);

			if (companyId <= 0)
			{
				await ShowValidation("Company not found.");
				return;
			}

			try
			{
				/*
                 * Keep the original NepaliDatePicker implementation.
                 *
                 * SelectedADDate supplies the AD date.
                 * SelectedBSDate supplies the BS date.
                 */
				string dateOnly =
					NepaliDatePicker.SelectedADDate
						.ToString("yyyy-MM-dd");

				string nepaliDate =
					NepaliDatePicker.SelectedBSDate;

				SavePayment(
					companyId,
					dateOnly,
					nepaliDate,
					amount,
					receipt,
					paymentMethod);

				Frame.GoBack();
			}
			catch (Exception ex)
			{
				await ShowValidation(
					$"Unable to save payment.\n\n{ex.Message}");
			}
		}

		private void SavePayment(
			int companyId,
			string dateOnly,
			string nepaliDate,
			decimal amount,
			string receipt,
			string paymentMethod)
		{
			/*
			 * Payment and its Ledger entry are written in one
			 * transaction: either both are saved or neither is.
			 */
			using var conn = DatabaseHelper.GetConnection();

			conn.Open();

			using var transaction = conn.BeginTransaction();

			try
			{
				/*
				 * 1. Insert Payment and capture its ID on this
				 *    connection (last_insert_rowid is per-connection).
				 */
				const string insertPaymentQuery = @"
                    INSERT INTO Payment
                    (
                        CompanyID,
                        PaymentDate,
                        Amount,
                        PaymentType,
                        Receipt,
                        PaymentMethod,
                        NepaliDate
                    )
                    VALUES
                    (
                        @Company,
                        @Date,
                        @Amount,
                        @Type,
                        @Receipt,
                        @PaymentMethod,
                        @NepaliDate
                    );
                    SELECT last_insert_rowid();";

				long paymentId;

				using (var cmd =
					new SQLiteCommand(insertPaymentQuery, conn, transaction))
				{
					cmd.Parameters.AddWithValue("@Company", companyId);
					cmd.Parameters.AddWithValue("@Date", dateOnly);
					cmd.Parameters.AddWithValue("@Amount", amount);
					cmd.Parameters.AddWithValue("@Type", _selectedPaymentType);
					cmd.Parameters.AddWithValue("@Receipt", receipt);
					cmd.Parameters.AddWithValue("@PaymentMethod", paymentMethod);
					cmd.Parameters.AddWithValue("@NepaliDate", nepaliDate);

					paymentId = Convert.ToInt64(cmd.ExecuteScalar());
				}

				/*
				 * 2. Get latest company ledger balance.
				 */
				const string lastBalanceQuery = @"
                    SELECT Balance
                    FROM Ledger
                    WHERE CompanyID = @Company
                    ORDER BY Date DESC, LedgerID DESC
                    LIMIT 1";

				object? balanceObject;

				using (var cmd =
					new SQLiteCommand(lastBalanceQuery, conn, transaction))
				{
					cmd.Parameters.AddWithValue("@Company", companyId);

					balanceObject = cmd.ExecuteScalar();
				}

				decimal lastBalance =
					balanceObject != null &&
					balanceObject != DBNull.Value
						? Convert.ToDecimal(balanceObject)
						: 0m;

				/*
				 * 3. Preserve WinForms accounting logic.
				 *
				 * Received = balance + amount
				 * Made     = balance - amount
				 */
				decimal newBalance =
					_selectedPaymentType == "Received"
						? lastBalance + amount
						: lastBalance - amount;

				decimal debit =
					_selectedPaymentType == "Made"
						? amount
						: 0m;

				decimal credit =
					_selectedPaymentType == "Received"
						? amount
						: 0m;

				string particular =
					$"Payment {_selectedPaymentType} via {paymentMethod}, Receipt: {receipt}";

				/*
				 * 4. Insert corresponding Ledger record, linked to
				 *    the payment so deletion can match it exactly.
				 */
				const string insertLedgerQuery = @"
                    INSERT INTO Ledger
                    (
                        Date,
                        Particular,
                        Debit,
                        Credit,
                        Balance,
                        CompanyID,
                        PaymentID
                    )
                    VALUES
                    (
                        @Date,
                        @Particular,
                        @Debit,
                        @Credit,
                        @Balance,
                        @CompanyID,
                        @PaymentID
                    )";

				using (var cmd =
					new SQLiteCommand(insertLedgerQuery, conn, transaction))
				{
					cmd.Parameters.AddWithValue("@Date", dateOnly);
					cmd.Parameters.AddWithValue("@Particular", particular);
					cmd.Parameters.AddWithValue("@Debit", debit);
					cmd.Parameters.AddWithValue("@Credit", credit);
					cmd.Parameters.AddWithValue("@Balance", newBalance);
					cmd.Parameters.AddWithValue("@CompanyID", companyId);
					cmd.Parameters.AddWithValue("@PaymentID", paymentId);

					cmd.ExecuteNonQuery();
				}

				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}
		}

		private void CancelButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (Frame.CanGoBack)
			{
				Frame.GoBack();
			}
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
			string message,
			Exception ex)
		{
			ContentDialog dialog = new ContentDialog
			{
				Title = "Payment",
				Content = $"{message}\n\n{ex.Message}",
				CloseButtonText = "OK",
				XamlRoot = XamlRoot
			};

			await dialog.ShowAsync();
		}
	}
}