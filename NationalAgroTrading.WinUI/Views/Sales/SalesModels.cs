using System;
using NationalAgroTrading.WinUI.Services;

namespace NationalAgroTrading.WinUI.Views.Sales
{
	// ============================================================
	// SALES COMPANY
	// ============================================================

	public sealed class SalesCompanyItem
	{
		public long CompanyID { get; set; }

		public string CompanyName { get; set; } = string.Empty;

		public int BillCount { get; set; }

		public decimal TotalAmount { get; set; }

		public string Summary =>
			$"{BillCount} bill(s) • Rs. {TotalAmount:N2}";
	}


	// ============================================================
	// SALES BILL
	// ============================================================

	public sealed class SalesBillItem
	{
		public long CompanyID { get; set; }

		public string CompanyName { get; set; } = string.Empty;

		public string BillNumber { get; set; } = string.Empty;

		public string NepaliDate { get; set; } = string.Empty;

		public string SaleDate { get; set; } = string.Empty;

		public int ItemCount { get; set; }

		public decimal Subtotal { get; set; }

		public decimal Discount { get; set; }

		public decimal VAT { get; set; }

		public decimal Amount { get; set; }

		public string AmountText =>
			$"Rs. {Amount:N2}";

		public string ItemCountText =>
			$"{ItemCount} item(s)";

		public string DiscountText =>
			$"Rs. {Discount:N2}";

		public string VATText =>
			$"Rs. {VAT:N2}";
	}


	// ============================================================
	// SALES DETAIL
	// ============================================================

	public sealed class SalesDetailItem
	{
		public long SaleID { get; set; }

		public long ProductID { get; set; }

		public string ProductName { get; set; } = string.Empty;

		public long Quantity { get; set; }

		public decimal Price { get; set; }

		public decimal Subtotal { get; set; }

		public decimal Discount { get; set; }

		public decimal VAT { get; set; }

		public decimal Total { get; set; }

		public string PriceText =>
			$"Rs. {Price:N2}";

		public string SubtotalText =>
			$"Rs. {Subtotal:N2}";

		public string DiscountText =>
			$"Rs. {Discount:N2}";

		public string VATText =>
			$"Rs. {VAT:N2}";

		public string TotalText =>
			$"Rs. {Total:N2}";

		public string QuantityText =>
			Quantity.ToString("N0");
	}


	// ============================================================
	// NEW SALE ITEM
	// ============================================================

	public sealed class SaleEntryItem
	{
		public const decimal VatRate = TaxCalculator.VatRate;

		public long ProductID { get; set; }

		public string ProductName { get; set; } = string.Empty;

		public long Quantity { get; set; }

		public decimal Price { get; set; }

		public bool IsVattable { get; set; }

		public string HSCode { get; set; } = string.Empty;

		public string PriceType { get; set; } = "Retail";


		// ------------------------------------------------------------
		// CALCULATIONS
		// ------------------------------------------------------------

		public decimal Subtotal =>
			Quantity * Price;

		// Undiscounted per-item preview only; the bill-level
		// discount is applied by TaxCalculator at save time.
		public decimal VAT =>
			TaxCalculator.VatOnAmount(
				Subtotal,
				IsVattable);

		public decimal TotalWithVAT =>
			Subtotal + VAT;


		// ------------------------------------------------------------
		// DISPLAY
		// ------------------------------------------------------------

		public string QuantityText =>
			Quantity.ToString("N0");

		public string PriceText =>
			$"Rs. {Price:N2}";

		public string SubtotalText =>
			$"Rs. {Subtotal:N2}";

		public string VATText =>
			IsVattable
				? $"Rs. {VAT:N2}"
				: "—";

		public string TotalText =>
			$"Rs. {TotalWithVAT:N2}";

		public string VATStatusText =>
			IsVattable
				? "VAT 13%"
				: "Non-VAT";
	}


	// ============================================================
	// EDITABLE SALE ITEM
	// ============================================================

	public sealed class EditableSaleItem
	{
		public const decimal VatRate = TaxCalculator.VatRate;

		public long SaleID { get; set; }

		public long ProductID { get; set; }

		public string ProductName { get; set; } = string.Empty;


		// ------------------------------------------------------------
		// ORIGINAL VALUES
		// ------------------------------------------------------------

		public long OriginalQuantity { get; set; }

		public decimal OriginalPrice { get; set; }

		public decimal OriginalDiscount { get; set; }

		public decimal OriginalDiscountPercent { get; set; }

		public decimal OriginalVAT { get; set; }


		// ------------------------------------------------------------
		// CURRENT VALUES
		// ------------------------------------------------------------

		public long Quantity { get; set; }

		public decimal Price { get; set; }

		public bool IsVattable { get; set; }

		public string HSCode { get; set; } = string.Empty;

		public string PriceType { get; set; } = "Retail";


		// ------------------------------------------------------------
		// CALCULATIONS
		// ------------------------------------------------------------

		public decimal Subtotal =>
			Quantity * Price;

		// Undiscounted per-item preview only; the bill-level
		// discount is applied by TaxCalculator at save time.
		public decimal VAT =>
			TaxCalculator.VatOnAmount(
				Subtotal,
				IsVattable);


		// ------------------------------------------------------------
		// DISPLAY
		// ------------------------------------------------------------

		public string QuantityText =>
			Quantity.ToString("N0");

		public string PriceText =>
			$"Rs. {Price:N2}";

		public string SubtotalText =>
			$"Rs. {Subtotal:N2}";

		public string VATText =>
			IsVattable
				? $"Rs. {VAT:N2}"
				: "—";

		public string TotalText =>
			$"Rs. {Subtotal + VAT:N2}";

		public string VATStatusText =>
			IsVattable
				? "VAT 13%"
				: "Non-VAT";
	}
}