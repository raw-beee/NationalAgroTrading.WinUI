using System;
using NationalAgroTrading.WinUI.Services;

namespace NationalAgroTrading.WinUI.Views.Purchase
{
	public sealed class PurchaseCompanyItem
	{
		public long CompanyID { get; set; }

		public string CompanyName { get; set; } = string.Empty;

		public int PurchaseCount { get; set; }

		public decimal TotalAmount { get; set; }

		public string PurchaseCountText =>
			$"{PurchaseCount} bill(s)";

		public string TotalAmountText =>
			$"Rs. {TotalAmount:N2}";
	}

	public sealed class PurchaseBillItem
	{
		public long CompanyID { get; set; }

		public string CompanyName { get; set; } = string.Empty;

		public string BillNumber { get; set; } = string.Empty;

		public string NepaliDate { get; set; } = string.Empty;

		public string PurchaseDate { get; set; } = string.Empty;

		public int ItemCount { get; set; }

		public decimal Subtotal { get; set; }

		public decimal Discount { get; set; }

		public decimal VAT { get; set; }

		public decimal Amount { get; set; }

		public string ItemCountText =>
			$"{ItemCount} item(s)";

		public string SubtotalText =>
			$"Rs. {Subtotal:N2}";

		public string DiscountText =>
			$"Rs. {Discount:N2}";

		public string VATText =>
			$"Rs. {VAT:N2}";

		public string AmountText =>
			$"Rs. {Amount:N2}";
	}

	public sealed class PurchaseEntryItem
	{
		public long ProductID { get; set; }

		public string ProductName { get; set; } = string.Empty;

		public long Quantity { get; set; }

		public decimal CostPrice { get; set; }

		public bool IsVattable { get; set; }

		public decimal Subtotal =>
			Quantity * CostPrice;

		// Undiscounted per-item preview only; the bill-level
		// discount is applied by TaxCalculator at save time.
		public decimal VAT =>
			TaxCalculator.VatOnAmount(
				Subtotal,
				IsVattable);

		public decimal TotalWithVAT =>
			Subtotal + VAT;

		public string QuantityText =>
			Quantity.ToString("N0");

		public string CostPriceText =>
			CostPrice.ToString("N2");

		public string SubtotalText =>
			Subtotal.ToString("N2");

		public string VATText =>
			VAT.ToString("N2");

		public string TotalWithVATText =>
			TotalWithVAT.ToString("N2");
	}

	public sealed class PurchaseDetailItem
	{
		public long PurchaseID { get; set; }

		public long ProductID { get; set; }

		public string ProductName { get; set; } = string.Empty;

		public long Quantity { get; set; }

		public decimal CostPrice { get; set; }

		public decimal Subtotal { get; set; }

		public decimal Discount { get; set; }

		public decimal VAT { get; set; }

		public decimal Total { get; set; }

		public string QuantityText =>
			Quantity.ToString("N0");

		public string CostPriceText =>
			CostPrice.ToString("N2");

		public string SubtotalText =>
			Subtotal.ToString("N2");

		public string DiscountText =>
			Discount.ToString("N2");

		public string VATText =>
			VAT.ToString("N2");

		public string TotalText =>
			Total.ToString("N2");
	}
}