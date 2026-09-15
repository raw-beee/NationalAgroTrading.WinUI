using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using NationalAgroTrading.WinUI.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;

namespace NationalAgroTrading.WinUI.Views.Company
{
	public sealed partial class CompanyPage : Page
	{
		private DataTable? companyData;

		private readonly ObservableCollection<CompanyListItem> companies =
			new();

		public CompanyPage()
		{
			InitializeComponent();

			CompanyListView.ItemsSource = companies;

			LoadCompanies();
		}

		private void LoadCompanies()
		{
			const string query =
				"SELECT CompanyID, CompanyName, VAT_PAN_Number, Email, " +
				"Contact1, Contact2, Address " +
				"FROM Company " +
				"ORDER BY CompanyName ASC";

			DataTable dt = DatabaseHelper.GetData(
				query,
				new Dictionary<string, object>());

			companyData = dt;

			companies.Clear();

			foreach (DataRow row in dt.Rows)
			{
				companies.Add(
					new CompanyListItem
					{
						CompanyID = Convert.ToInt32(row["CompanyID"]),

						CompanyName =
							row["CompanyName"] == DBNull.Value
								? string.Empty
								: row["CompanyName"].ToString()!,

						VAT_PAN_Number =
							row["VAT_PAN_Number"] == DBNull.Value
								? string.Empty
								: row["VAT_PAN_Number"].ToString()!,

						Email =
							row["Email"] == DBNull.Value
								? string.Empty
								: row["Email"].ToString()!,

						Contact1 =
							row["Contact1"] == DBNull.Value
								? string.Empty
								: row["Contact1"].ToString()!,

						Contact2 =
							row["Contact2"] == DBNull.Value
								? string.Empty
								: row["Contact2"].ToString()!,

						Address =
							row["Address"] == DBNull.Value
								? string.Empty
								: row["Address"].ToString()!
					});
			}
		}

		private void SearchTextBox_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			if (companyData == null)
				return;

			string filterText =
				SearchTextBox.Text
					.Trim()
					.Replace("'", "''");

			if (string.IsNullOrWhiteSpace(filterText))
			{
				LoadCompanies();
				return;
			}

			string rowFilter =
				$"[CompanyName] LIKE '%{filterText}%' OR " +
				$"[Address] LIKE '%{filterText}%' OR " +
				$"[Email] LIKE '%{filterText}%' OR " +
				$"[Contact1] LIKE '%{filterText}%' OR " +
				$"[Contact2] LIKE '%{filterText}%' OR " +
				$"[VAT_PAN_Number] LIKE '%{filterText}%'";

			DataRow[] rows =
				companyData.Select(rowFilter);

			companies.Clear();

			foreach (DataRow row in rows)
			{
				companies.Add(
					new CompanyListItem
					{
						CompanyID = Convert.ToInt32(row["CompanyID"]),

						CompanyName =
							row["CompanyName"] == DBNull.Value
								? string.Empty
								: row["CompanyName"].ToString()!,

						VAT_PAN_Number =
							row["VAT_PAN_Number"] == DBNull.Value
								? string.Empty
								: row["VAT_PAN_Number"].ToString()!,

						Email =
							row["Email"] == DBNull.Value
								? string.Empty
								: row["Email"].ToString()!,

						Contact1 =
							row["Contact1"] == DBNull.Value
								? string.Empty
								: row["Contact1"].ToString()!,

						Contact2 =
							row["Contact2"] == DBNull.Value
								? string.Empty
								: row["Contact2"].ToString()!,

						Address =
							row["Address"] == DBNull.Value
								? string.Empty
								: row["Address"].ToString()!
					});
			}
		}

		private void AddCompanyButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			Frame.Navigate(typeof(AddCompanyPage));
		}

		private void CompanyListView_DoubleTapped(
			object sender,
			DoubleTappedRoutedEventArgs e)
		{
			if (CompanyListView.SelectedItem
				is not CompanyListItem company)
			{
				return;
			}

			Frame.Navigate(
				typeof(EditCompanyPage),
				new CompanyEditData
				{
					CompanyID = company.CompanyID,
					CompanyName = company.CompanyName,
					VAT_PAN_Number = company.VAT_PAN_Number,
					Email = company.Email,
					Contact1 = company.Contact1,
					Contact2 = company.Contact2,
					Address = company.Address
				});
		}

		protected override void OnNavigatedTo(
			NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			LoadCompanies();
		}
	}

	public sealed class CompanyListItem
	{
		public int CompanyID { get; set; }

		public string CompanyName { get; set; } = string.Empty;

		public string VAT_PAN_Number { get; set; } = string.Empty;

		public string Email { get; set; } = string.Empty;

		public string Contact1 { get; set; } = string.Empty;

		public string Contact2 { get; set; } = string.Empty;

		public string Address { get; set; } = string.Empty;
	}

	public sealed class CompanyEditData
	{
		public int CompanyID { get; set; }

		public string CompanyName { get; set; } = string.Empty;

		public string VAT_PAN_Number { get; set; } = string.Empty;

		public string Email { get; set; } = string.Empty;

		public string Contact1 { get; set; } = string.Empty;

		public string Contact2 { get; set; } = string.Empty;

		public string Address { get; set; } = string.Empty;
	}
}