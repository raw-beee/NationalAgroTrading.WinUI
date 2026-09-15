using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;

namespace NationalAgroTrading.WinUI.Views.Company
{
	public sealed partial class EditCompanyPage : Page
	{
		private int companyId;

		public EditCompanyPage()
		{
			InitializeComponent();
		}

		protected override void OnNavigatedTo(
			NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			if (e.Parameter is not CompanyEditData company)
				return;

			companyId = company.CompanyID;

			CompanyNameTextBox.Text =
				company.CompanyName;

			VatPanTextBox.Text =
				company.VAT_PAN_Number;

			EmailTextBox.Text =
				company.Email;

			Contact1TextBox.Text =
				company.Contact1;

			Contact2TextBox.Text =
				company.Contact2;

			AddressTextBox.Text =
				company.Address;
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

			string contact1 =
				Contact1TextBox.Text.Trim();

			string contact2 =
				Contact2TextBox.Text.Trim();

			string address =
				AddressTextBox.Text.Trim();

			const string query =
				"UPDATE Company SET " +
				"CompanyName = @name, " +
				"VAT_PAN_Number = @vat, " +
				"Email = @mail, " +
				"Contact1 = @phone1, " +
				"Contact2 = @contact2, " +
				"Address = @address " +
				"WHERE CompanyID = @id";

			var parameters =
				new Dictionary<string, object>
				{
					["@name"] = name,

					["@vat"] = vatPan,

					["@mail"] =
						string.IsNullOrWhiteSpace(email)
							? DBNull.Value
							: email,

					["@phone1"] =
						string.IsNullOrWhiteSpace(contact1)
							? DBNull.Value
							: contact1,

					["@contact2"] =
						string.IsNullOrWhiteSpace(contact2)
							? DBNull.Value
							: contact2,

					["@address"] =
						string.IsNullOrWhiteSpace(address)
							? DBNull.Value
							: address,

					["@id"] = companyId
				};

			try
			{
				DatabaseHelper.ExecuteNonQuery(
					query,
					parameters);

				await ShowMessageAsync(
					"Company updated successfully.",
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
			{
				Frame.GoBack();
			}
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