using System;

namespace NationalAgroTrading.WinUI.Models
{
	public sealed class Sale
	{
		public long SaleID { get; set; }

		public long ProductID { get; set; }

		public long? CompanyID { get; set; }

		public long? CustomerID { get; set; }

		public string ProductName { get; set; } =
			string.Empty;

		public string CompanyName { get; set; } =
			string.Empty;

		public string CustomerName { get; set; } =
			string.Empty;

		public long Quantity { get; set; }

		public decimal Price { get; set; }

		public decimal TotalSalesAmount { get; set; }

		public decimal Discount { get; set; }

		public decimal DiscountPercent { get; set; }

		public decimal TotalAmountAfterDiscount { get; set; }

		public decimal VATAmount { get; set; }

		public decimal TotalAmountWithVAT { get; set; }

		public string BillNumber { get; set; } =
			string.Empty;

		public string NepaliDate { get; set; } =
			string.Empty;

		public DateTime SaleDate { get; set; }
	}
}