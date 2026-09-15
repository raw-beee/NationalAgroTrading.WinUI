using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace NationalAgroTrading.WinUI.Views.Payment
{
	public sealed partial class PaymentPage : Page
	{
		private readonly ObservableCollection<PaymentRow> _payments = new();
		private List<PaymentRow> _allPayments = new();

		public PaymentPage()
		{
			InitializeComponent();

			PaymentsListView.ItemsSource = _payments;

			Loaded += PaymentPage_Loaded;
		}

		private void PaymentPage_Loaded(object sender, RoutedEventArgs e)
		{
			LoadPayments();
		}

		private void LoadPayments()
		{
			try
			{
				const string query = @"
                    SELECT
                        p.PaymentID,
                        c.CompanyName,
                        p.Receipt,
                        p.Amount,
                        p.NepaliDate,
                        p.PaymentMethod,
                        p.PaymentType
                    FROM Payment p
                    JOIN Company c
                        ON p.CompanyID = c.CompanyID
                    ORDER BY p.PaymentDate DESC, p.PaymentID DESC";

				DataTable table = DatabaseHelper.GetData(
					query,
					new Dictionary<string, object>());

				_allPayments = new List<PaymentRow>();

				foreach (DataRow row in table.Rows)
				{
					_allPayments.Add(new PaymentRow
					{
						PaymentID = Convert.ToInt32(row["PaymentID"]),
						CompanyName = row["CompanyName"]?.ToString() ?? "",
						Receipt = row["Receipt"]?.ToString() ?? "",
						Amount = Convert.ToDecimal(row["Amount"]),
						NepaliDate = row["NepaliDate"]?.ToString() ?? "",
						PaymentMethod = row["PaymentMethod"]?.ToString() ?? "",
						PaymentType = row["PaymentType"]?.ToString() ?? ""
					});
				}

				ApplyFilter(SearchBox.Text);
			}
			catch (Exception ex)
			{
				ShowError("Unable to load payments.", ex);
			}
		}

		private void ApplyFilter(string? searchText)
		{
			string text = searchText?.Trim() ?? "";

			IEnumerable<PaymentRow> result = _allPayments;

			if (!string.IsNullOrWhiteSpace(text))
			{
				result = _allPayments.Where(p =>
					p.CompanyName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
					p.Receipt.Contains(text, StringComparison.OrdinalIgnoreCase) ||
					p.PaymentMethod.Contains(text, StringComparison.OrdinalIgnoreCase) ||
					p.PaymentType.Contains(text, StringComparison.OrdinalIgnoreCase));
			}

			_payments.Clear();

			foreach (PaymentRow payment in result)
			{
				_payments.Add(payment);
			}

			EmptyText.Visibility =
				_payments.Count == 0
					? Visibility.Visible
					: Visibility.Collapsed;
		}

		private void SearchBox_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			ApplyFilter(SearchBox.Text);
		}

		private void RefreshButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			LoadPayments();
		}

		private void AddPaymentButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			Frame.Navigate(typeof(AddPaymentPage));
		}

		private void PaymentsListView_ItemClick(
			object sender,
			ItemClickEventArgs e)
		{
			if (e.ClickedItem is PaymentRow payment)
			{
				Frame.Navigate(
					typeof(UpdatePaymentPage),
					payment.PaymentID);
			}
		}

		private void PaymentsListView_RightTapped(
			object sender,
			RightTappedRoutedEventArgs e)
		{
			if (e.OriginalSource is FrameworkElement element &&
				element.DataContext is PaymentRow payment)
			{
				ShowDeleteMenu(element, payment);
			}
		}

		private void ShowDeleteMenu(
			FrameworkElement target,
			PaymentRow payment)
		{
			MenuFlyout menu = new MenuFlyout();

			MenuFlyoutItem editItem = new MenuFlyoutItem
			{
				Text = "Edit Payment"
			};

			editItem.Click += (_, _) =>
			{
				Frame.Navigate(
					typeof(UpdatePaymentPage),
					payment.PaymentID);
			};

			MenuFlyoutItem deleteItem = new MenuFlyoutItem
			{
				Text = "Delete Payment"
			};

			deleteItem.Click += async (_, _) =>
			{
				ContentDialog dialog = new ContentDialog
				{
					Title = "Delete Payment",
					Content = $"Are you sure you want to delete payment #{payment.PaymentID}?",
					PrimaryButtonText = "Delete",
					CloseButtonText = "Cancel",
					DefaultButton = ContentDialogButton.Close,
					XamlRoot = XamlRoot
				};

				ContentDialogResult result = await dialog.ShowAsync();

				if (result == ContentDialogResult.Primary)
				{
					DeletePayment(payment.PaymentID);
				}
			};

			menu.Items.Add(editItem);
			menu.Items.Add(deleteItem);

			menu.ShowAt(target);
		}

		private void DeletePayment(int paymentId)
		{
			try
			{
				/*
				 * 1. Delete the Ledger entry linked to this payment
				 *    by PaymentID (exact match — never by text, which
				 *    could delete unrelated history).
				 * 2. Delete the Payment record.
				 *
				 * Both in one transaction: either both go or neither.
				 */
				using var conn = DatabaseHelper.GetConnection();

				conn.Open();

				using var transaction = conn.BeginTransaction();

				try
				{
					const string deleteLedgerQuery = @"
                        DELETE FROM Ledger
                        WHERE PaymentID = @PaymentID";

					using (var ledgerCmd =
						new SQLiteCommand(deleteLedgerQuery, conn, transaction))
					{
						ledgerCmd.Parameters.AddWithValue(
							"@PaymentID",
							paymentId);

						ledgerCmd.ExecuteNonQuery();
					}

					const string deletePaymentQuery = @"
                        DELETE FROM Payment
                        WHERE PaymentID = @PaymentID";

					using (var paymentCmd =
						new SQLiteCommand(deletePaymentQuery, conn, transaction))
					{
						paymentCmd.Parameters.AddWithValue(
							"@PaymentID",
							paymentId);

						paymentCmd.ExecuteNonQuery();
					}

					transaction.Commit();
				}
				catch
				{
					transaction.Rollback();
					throw;
				}

				LoadPayments();
			}
			catch (Exception ex)
			{
				ShowError("Unable to delete payment.", ex);
			}
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

		public sealed class PaymentRow
		{
			public int PaymentID { get; set; }

			public string CompanyName { get; set; } = "";

			public string Receipt { get; set; } = "";

			public decimal Amount { get; set; }

			public string AmountDisplay =>
				Amount.ToString("N2");

			public string NepaliDate { get; set; } = "";

			public string PaymentMethod { get; set; } = "";

			public string PaymentType { get; set; } = "";
		}
	}
}