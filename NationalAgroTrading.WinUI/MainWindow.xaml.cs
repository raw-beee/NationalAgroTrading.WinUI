using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using NationalAgroTrading.WinUI.Views.Dashboard;
using NationalAgroTrading.WinUI.Views.Company;
using NationalAgroTrading.WinUI.Views.Inventory;
using NationalAgroTrading.WinUI.Views.Purchase;
using NationalAgroTrading.WinUI.Views.Sales;
using NationalAgroTrading.WinUI.Views.Payment;
using NationalAgroTrading.WinUI.Views.Expenses;
using NationalAgroTrading.WinUI.Views.Ledger;
using NationalAgroTrading.WinUI.Views.CostPriceHistory;
using NationalAgroTrading.WinUI.Views.Product;
using NationalAgroTrading.WinUI.Views.SalesPriceHistory;

namespace NationalAgroTrading.WinUI
{
	public sealed partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			Title = "National Agro Trading";

			NavigateToHome();
		}

		private void NavigateToHome()
		{
			PageTitleTextBlock.Text = "Dashboard";

			MainFrame.Navigate(typeof(DashboardPage));

			SetSelectedButton(HomeButton);
		}

		private void HomeButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			NavigateToHome();
		}

		private void SalesButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			PageTitleTextBlock.Text = "Sales";

			MainFrame.Navigate(typeof(SalesPage));

			SetSelectedButton(SalesButton);
		}

		private void CounterSalesButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			PageTitleTextBlock.Text = "Counter Sales";

			SetSelectedButton(CounterSalesButton);
		}

		private void PurchasesButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			PageTitleTextBlock.Text = "Purchases";

			MainFrame.Navigate(typeof(PurchasePage));

			SetSelectedButton(PurchasesButton);
		}

		private void InventoryButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			PageTitleTextBlock.Text = "Inventory";

			MainFrame.Navigate(typeof(InventoryPage));

			SetSelectedButton(InventoryButton);
		}

		private void AddProductButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			PageTitleTextBlock.Text = "Add Product";

			MainFrame.Navigate(typeof(ProductPage));

			SetSelectedButton(AddProductButton);
		}

		private void LedgerButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			PageTitleTextBlock.Text = "Ledger";

			MainFrame.Navigate(typeof(LedgerPage));

			SetSelectedButton(LedgerButton);
		}

		private void CompaniesButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			PageTitleTextBlock.Text = "Companies";

			MainFrame.Navigate(typeof(CompanyPage));

			SetSelectedButton(CompaniesButton);
		}

		private void PaymentButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			PageTitleTextBlock.Text = "Payment";

			MainFrame.Navigate(typeof(PaymentPage));

			SetSelectedButton(PaymentButton);
		}

		private void ExpensesButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			PageTitleTextBlock.Text = "Expenses";

			MainFrame.Navigate(typeof(ExpensesPage));

			SetSelectedButton(ExpensesButton);
		}

		private void CostPriceHistoryButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			PageTitleTextBlock.Text = "CP History";

			MainFrame.Navigate(typeof(CostPriceHistoryPage));

			SetSelectedButton(CostPriceHistoryButton);
		}

		private void SalesPriceHistoryButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			PageTitleTextBlock.Text = "SP History";

			MainFrame.Navigate(typeof(SalesPriceHistoryPage));

			SetSelectedButton(SalesPriceHistoryButton);
		}

		private void SetSelectedButton(Button selectedButton)
		{
			Button[] buttons =
			{
				HomeButton,
				SalesButton,
				CounterSalesButton,
				PurchasesButton,
				InventoryButton,
				AddProductButton,
				LedgerButton,
				CompaniesButton,
				PaymentButton,
				ExpensesButton,
				CostPriceHistoryButton,
				SalesPriceHistoryButton
			};

			foreach (Button button in buttons)
			{
				button.Opacity = 0.70;
			}

			selectedButton.Opacity = 1.0;
		}
	}
}