namespace NationalAgroTrading.WinUI.Models
{
	using NationalAgroTrading.WinUI.Services;

	public sealed class SaleItem
	{
		public long SaleID { get; set; }

		public long ProductID { get; set; }

		public string ProductName { get; set; } =
			string.Empty;

		public long Quantity { get; set; }

		public decimal Price { get; set; }

		public string PriceType { get; set; } =
			"Retail";

		public decimal Discount { get; set; }

		public decimal DiscountPercent { get; set; }

		public decimal VATAmount { get; set; }

		public bool IsVattable { get; set; }

		public string HSCode { get; set; } =
			string.Empty;

		public long OriginalQuantity { get; set; }

		public decimal OriginalPrice { get; set; }

		public bool IsNew { get; set; }

		// ------------------------------------------------------------
		// Gross item subtotal
		// ------------------------------------------------------------

		public decimal Subtotal =>
			Quantity * Price;

		// ------------------------------------------------------------
		// Item's allocated share of invoice discount
		// ------------------------------------------------------------

		public decimal DiscountAmount =>
			Subtotal *
			DiscountPercent /
			100m;

		// ------------------------------------------------------------
		// After discount
		// ------------------------------------------------------------

		public decimal AfterDiscount =>
			Subtotal - DiscountAmount;

		// ------------------------------------------------------------
		// VAT
		// ------------------------------------------------------------

		public decimal VAT =>
			TaxCalculator.VatOnAmount(
				AfterDiscount,
				IsVattable);

		// ------------------------------------------------------------
		// Final item total
		// ------------------------------------------------------------

		public decimal Total =>
			AfterDiscount + VAT;

		public string SubtotalText =>
			$"Rs. {Subtotal:N2}";

		public string VATText =>
			$"Rs. {VAT:N2}";

		public string TotalText =>
			$"Rs. {Total:N2}";
	}
}