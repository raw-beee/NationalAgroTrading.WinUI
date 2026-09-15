using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;

namespace NationalAgroTrading.WinUI.Views.Company
{
	public sealed partial class AddCompanyPage : Page
	{
		public AddCompanyPage()
		{
			InitializeComponent();
		}

		private async void SaveButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			string name =
				CompanyNameTextBox.Text.Trim();

			string vatPan =
				VatPanTextBox.Text.Trim();

			string email =
				EmailTextBox.Text.Trim();

			string contact =
				Contact1TextBox.Text.Trim();

			string contact2 =
				Contact2TextBox.Text.Trim();

			string address =
				AddressTextBox.Text.Trim();

			if (string.IsNullOrWhiteSpace(name) ||
				string.IsNullOrWhiteSpace(vatPan))
			{
				await ShowMessageAsync(
					"Company Name and VAT/PAN Number are required.",
					"Validation");

				return;
			}

			const string query =
				"INSERT INTO Company " +
				"(CompanyName, VAT_PAN_Number, Email, Contact1, Contact2, Address) " +
				"VALUES " +
				"(@name, @vat, @mail, @phone, @phone1, @address)";

			var parameters =
				new Dictionary<string, object>
				{
					["@name"] = name,
					["@vat"] = vatPan,
					["@mail"] =
						string.IsNullOrWhiteSpace(email)
							? DBNull.Value
							: email,

					["@phone"] =
						string.IsNullOrWhiteSpace(contact)
							? DBNull.Value
							: contact,

					["@phone1"] =
						string.IsNullOrWhiteSpace(contact2)
							? DBNull.Value
							: contact2,

					["@address"] =
						string.IsNullOrWhiteSpace(address)
							? DBNull.Value
							: address
				};

			try
			{
				DatabaseHelper.ExecuteNonQuery(
					query,
					parameters);

				await ShowMessageAsync(
					"Company added successfully.",
					"Success");

				if (Frame.CanGoBack)
				{
					Frame.GoBack();
				}
			}
			catch (Exception ex)
			{
				await ShowMessageAsync(
					"Error: " + ex.Message,
					"Database Error");
			}
		}

		private void CancelButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			if (Frame.CanGoBack)
				Frame.GoBack();
		}

		private async System.Threading.Tasks.Task ShowMessageAsync(
			string message,
			string title)
		{
			var dialog = new ContentDialog
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