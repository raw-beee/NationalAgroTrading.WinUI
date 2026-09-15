using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Draw;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using NationalAgroTrading.WinUI.Data;
using NationalAgroTrading.WinUI.Services;

using PdfDocument = iText.Kernel.Pdf.PdfDocument;


namespace NationalAgroTrading.WinUI.Views.Sales
{
	// ============================================================
	// SALES BILL PDF GENERATOR
	//
	// Generates a printable tax invoice for a saved sale bill.
	// The totals are computed with the same rules that were used
	// when the sale was saved:
	//
	//     - Bill-level discount percentage is allocated to each
	//       item in proportion to its subtotal.
	//     - VAT (13%) is charged only on vattable items and only
	//       on the amount remaining after the discount.
	// ============================================================

	public static class SalesBillPdfGenerator
	{
		// ============================================================
		// SELLER INFORMATION
		//
		// Fill these in once and every bill will carry them.
		// Empty lines are skipped automatically.
		// ============================================================

		private const string SellerName =
			"NATIONAL AGRO TRADING";

		private const string SellerAddress =
			"Lagankhel-12, Lalitpur, Nepal";

		private const string SellerPhone =
			"(+977)01-5448383";

		private const string SellerVatNumber =
			"";


		// ============================================================
		// CONSTANTS
		// ============================================================

		private const decimal VatRate = Services.TaxCalculator.VatRate;

		private static readonly string PrimaryBillsFolder =
			AppConfig.SalesBillsFolder;


		// ============================================================
		// PUBLIC API
		// ============================================================

		public static Task<string> GenerateBillPdfAsync(
			long companyId,
			string companyName,
			string billNumber,
			string nepaliDate,
			DateTime saleDate,
			IReadOnlyList<SaleEntryItem> items,
			decimal discountPercent)
		{
			return Task.Run(
				() => GenerateBillPdf(
					companyId,
					companyName,
					billNumber,
					nepaliDate,
					saleDate,
					items,
					discountPercent));
		}


		public static string GenerateBillPdf(
			long companyId,
			string companyName,
			string billNumber,
			string nepaliDate,
			DateTime saleDate,
			IReadOnlyList<SaleEntryItem> items,
			decimal discountPercent)
		{
			if (items == null ||
				items.Count == 0)
			{
				throw new InvalidOperationException(
					"No sale items were supplied for the bill.");
			}

			List<SaleEntryItem> billItems =
				items.ToList();

			// --------------------------------------------------------
			// Totals - same rules as the save logic.
			// --------------------------------------------------------

			decimal subtotal =
				billItems.Sum(
					x => x.Subtotal);

			decimal totalDiscount =
				subtotal *
				discountPercent /
				100m;

			decimal vatAmount =
				CalculateVat(
					billItems,
					subtotal,
					totalDiscount);

			decimal taxableAmount =
				subtotal -
				totalDiscount;

			decimal grandTotal =
				taxableAmount +
				vatAmount;

			// --------------------------------------------------------
			// Buyer information.
			// --------------------------------------------------------

			Models.Company buyer =
				LoadBuyer(
					companyId,
					companyName);

			// --------------------------------------------------------
			// Output file.
			// --------------------------------------------------------

			string filePath =
				CreateOutputPath(
					billNumber,
					companyName);

			// --------------------------------------------------------
			// Document.
			// --------------------------------------------------------

			PdfFont boldFont =
				PdfFontFactory.CreateFont(
					StandardFonts.HELVETICA_BOLD);

			PdfFont normalFont =
				PdfFontFactory.CreateFont(
					StandardFonts.HELVETICA);

			PdfFont italicFont =
				PdfFontFactory.CreateFont(
					StandardFonts.HELVETICA_OBLIQUE);

			DeviceRgb headerBackground =
				new DeviceRgb(235, 235, 235);

			DeviceRgb totalBackground =
				new DeviceRgb(220, 228, 220);

			DeviceRgb accentColor =
				new DeviceRgb(46, 104, 46);

			using (var writer =
				new PdfWriter(filePath))
			using (var pdf =
				new PdfDocument(writer))
			{
				var document =
					new Document(pdf);

				document.SetMargins(
					36f,
					36f,
					36f,
					36f);


				// ====================================================
				// SELLER HEADER
				// ====================================================

				document.Add(
					new Paragraph(SellerName)
						.SetFont(boldFont)
						.SetFontSize(18)
						.SetFontColor(accentColor)
						.SetTextAlignment(
							TextAlignment.CENTER));

				AddSellerLine(
					document,
					SellerAddress);

				AddSellerLine(
					document,
					CombinePhoneAndVat());

				document.Add(
					new Paragraph("TAX INVOICE")
						.SetFont(boldFont)
						.SetFontSize(11)
						.SetTextAlignment(
							TextAlignment.CENTER));

				document.Add(
					new LineSeparator(
						new SolidLine(1f)));

				document.Add(
					new Paragraph(" "));


				// ====================================================
				// BILL METADATA + BUYER
				// ====================================================

				Table metaTable =
					new Table(
						UnitValue.CreatePercentArray(
							new float[] { 58f, 42f }))
						.UseAllAvailableWidth()
						.SetBorder(
							Border.NO_BORDER);

				// ----------------------------------------------------
				// Left cell - buyer information.
				// ----------------------------------------------------

				var buyerCell =
					new Cell()
						.SetBorder(
							Border.NO_BORDER);

				buyerCell.Add(
					new Paragraph("Billed To")
						.SetFont(boldFont)
						.SetFontSize(10));

				buyerCell.Add(
					new Paragraph(buyer.CompanyName)
						.SetFont(boldFont)
						.SetFontSize(11));

				AddBuyerLine(
					buyerCell,
					"Address",
					buyer.Address,
					normalFont);

				AddBuyerLine(
					buyerCell,
					"Phone",
					buyer.Phone,
					normalFont);

				AddBuyerLine(
					buyerCell,
					"VAT/PAN",
					buyer.VATNumber,
					normalFont);

				metaTable.AddCell(
					buyerCell);

				// ----------------------------------------------------
				// Right cell - bill details.
				// ----------------------------------------------------

				var metaCell =
					new Cell()
						.SetBorder(
							Border.NO_BORDER)
						.SetTextAlignment(
							TextAlignment.RIGHT);

				metaCell.Add(
					new Paragraph(
						$"Bill No: {billNumber}")
						.SetFont(boldFont)
						.SetFontSize(11));

				metaCell.Add(
					new Paragraph(
						$"Date (BS): {nepaliDate}")
						.SetFont(normalFont)
						.SetFontSize(10));

				metaCell.Add(
					new Paragraph(
						$"Date (AD): {saleDate:yyyy-MM-dd}")
						.SetFont(normalFont)
						.SetFontSize(10));

				metaTable.AddCell(
					metaCell);

				document.Add(
					metaTable);

				document.Add(
					new Paragraph(" "));


				// ====================================================
				// ITEMS TABLE
				// ====================================================

				Table itemsTable =
					new Table(
						UnitValue.CreatePercentArray(
							new float[] { 7f, 35f, 13f, 10f, 17f, 18f }))
						.UseAllAvailableWidth();

				string[] headers =
				{
					"S.N.",
					"Product",
					"HS Code",
					"Qty",
					"Unit Price",
					"Amount"
				};

				foreach (string header in headers)
				{
					itemsTable.AddHeaderCell(
						new Cell()
							.SetBackgroundColor(
								headerBackground)
							.SetFont(boldFont)
							.SetFontSize(9)
							.SetTextAlignment(
								IsNumericColumn(header)
									? TextAlignment.RIGHT
									: TextAlignment.LEFT)
							.SetPadding(5f)
							.Add(
								new Paragraph(header)));
				}

				int serialNumber =
					1;

				foreach (SaleEntryItem item in billItems)
				{
					itemsTable.AddCell(
						StyledCell(
							serialNumber.ToString(),
							normalFont,
							TextAlignment.RIGHT));

					itemsTable.AddCell(
						StyledCell(
							item.ProductName,
							normalFont,
							TextAlignment.LEFT));

					itemsTable.AddCell(
						StyledCell(
							string.IsNullOrWhiteSpace(item.HSCode)
								? "-"
								: item.HSCode,
							normalFont,
							TextAlignment.LEFT));

					itemsTable.AddCell(
						StyledCell(
							item.Quantity.ToString("N0"),
							normalFont,
							TextAlignment.RIGHT));

					itemsTable.AddCell(
						StyledCell(
							item.Price.ToString("N2"),
							normalFont,
							TextAlignment.RIGHT));

					itemsTable.AddCell(
						StyledCell(
							item.Subtotal.ToString("N2"),
							normalFont,
							TextAlignment.RIGHT));

					serialNumber++;
				}

				document.Add(
					itemsTable);

				document.Add(
					new Paragraph(" "));


				// ====================================================
				// TOTALS
				// ====================================================

				Table totalsTable =
					new Table(
						UnitValue.CreatePercentArray(2))
						.SetWidth(
							UnitValue.CreatePercentValue(50f))
						.SetHorizontalAlignment(
							HorizontalAlignment.RIGHT);

				AddTotalRow(
					totalsTable,
					"Subtotal",
					subtotal,
					normalFont,
					null);

				AddTotalRow(
					totalsTable,
					$"Discount ({discountPercent:0.##}%)",
					totalDiscount,
					normalFont,
					null);

				AddTotalRow(
					totalsTable,
					"Taxable Amount",
					taxableAmount,
					normalFont,
					null);

				AddTotalRow(
					totalsTable,
					"VAT (13%)",
					vatAmount,
					normalFont,
					null);

				AddTotalRow(
					totalsTable,
					"Grand Total",
					grandTotal,
					boldFont,
					totalBackground);

				document.Add(
					totalsTable);

				document.Add(
					new Paragraph(" "));


				// ====================================================
				// AMOUNT IN WORDS
				// ====================================================

				document.Add(
					new Paragraph(
						$"In Words: {AmountInWords(grandTotal)}")
						.SetFont(boldFont)
						.SetFontSize(9)
						.SetTextAlignment(
							TextAlignment.LEFT));


				// ====================================================
				// FOOTER
				// ====================================================

				document.Add(
					new Paragraph(" "));

				document.Add(
					new Paragraph(
						"Goods once sold are not returnable unless agreed otherwise. " +
						"Please retain this bill for warranty and accounting purposes.")
						.SetFont(italicFont)
						.SetFontSize(8)
						.SetTextAlignment(
							TextAlignment.CENTER));

				document.Add(
					new Paragraph(" "));

				document.Add(
					new Paragraph("Authorised Signature: ______________________")
						.SetFont(normalFont)
						.SetFontSize(9)
						.SetTextAlignment(
							TextAlignment.RIGHT));
			}

			return filePath;
		}


		// ============================================================
		// VAT
		// ============================================================

		private static decimal CalculateVat(
			List<SaleEntryItem> items,
			decimal subtotal,
			decimal totalDiscount)
		{
			return
				Services.TaxCalculator.BillVat(
					items.Select(
						x => (x.Subtotal, x.IsVattable)),
					subtotal,
					totalDiscount);
		}


		// ============================================================
		// BUYER
		// ============================================================

		private static Models.Company LoadBuyer(
			long companyId,
			string companyName)
		{
			var buyer =
				new Models.Company
				{
					CompanyName =
						companyName,

					Address =
						string.Empty,

					Phone =
						string.Empty,

					VATNumber =
						string.Empty
				};

			try
			{
				const string query =
					"""
                    SELECT
                        CompanyName,
                        Address,
                        Phone,
                        VATNumber
                    FROM Company
                    WHERE CompanyID = @CompanyID
                    LIMIT 1
                    """;

				DataTable table =
					DatabaseHelper.GetData(
						query,
						new Dictionary<string, object>
						{
							["@CompanyID"] =
								companyId
						});

				if (table.Rows.Count == 0)
				{
					return buyer;
				}

				DataRow row =
					table.Rows[0];

				buyer.CompanyName =
					row["CompanyName"] == DBNull.Value
						? companyName
						: row["CompanyName"]?.ToString()
							?? companyName;

				buyer.Address =
					row["Address"] == DBNull.Value
						? string.Empty
						: row["Address"]?.ToString()
							?? string.Empty;

				buyer.Phone =
					row["Phone"] == DBNull.Value
						? string.Empty
						: row["Phone"]?.ToString()
							?? string.Empty;

				buyer.VATNumber =
					row["VATNumber"] == DBNull.Value
						? string.Empty
						: row["VATNumber"]?.ToString()
							?? string.Empty;
			}
			catch
			{
				// The bill is still generated with the company
				// name only if the extra details cannot be read.
			}

			return buyer;
		}


		// ============================================================
		// OUTPUT PATH
		// ============================================================

		private static string CreateOutputPath(
			string billNumber,
			string companyName)
		{
			string folder =
				CreateBillsFolder();

			string baseName =
				$"Bill {SanitizeFileName(billNumber)} - {SanitizeFileName(companyName)}";

			string path =
				Path.Combine(
					folder,
					$"{baseName}.pdf");

			int attempt =
				1;

			while (File.Exists(path))
			{
				path =
					Path.Combine(
						folder,
						$"{baseName} ({attempt}).pdf");

				attempt++;
			}

			return path;
		}


		private static string CreateBillsFolder()
		{
			// --------------------------------------------------------
			// Preferred location matches the application's existing
			// data folder convention. Fallbacks are used when the
			// drive is unavailable (e.g. another machine).
			// --------------------------------------------------------

			string[] candidates =
			{
				PrimaryBillsFolder,

				Path.Combine(
					Environment.GetFolderPath(
						Environment.SpecialFolder.MyDocuments),
					"National Agro Trading",
					"Sales Bills"),

				Path.Combine(
					Environment.GetFolderPath(
						Environment.SpecialFolder.LocalApplicationData),
					"National Agro Trading",
					"Sales Bills")
			};

			foreach (string candidate in candidates)
			{
				try
				{
					Directory.CreateDirectory(
						candidate);

					return candidate;
				}
				catch
				{
					// Try the next candidate folder.
				}
			}

			throw new InvalidOperationException(
				"Unable to create a folder to save the bill PDF.");
		}


		private static string SanitizeFileName(
			string value)
		{
			string sanitized =
				value ?? string.Empty;

			foreach (char invalid in Path.GetInvalidFileNameChars())
			{
				sanitized =
					sanitized.Replace(
						invalid,
						'_');
			}

			sanitized =
				sanitized.Trim();

			if (sanitized.Length == 0)
			{
				sanitized =
					"Unnamed";
			}

			return sanitized;
		}


		// ============================================================
		// DOCUMENT HELPERS
		// ============================================================

		private static void AddSellerLine(
			Document document,
			string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return;
			}

			document.Add(
				new Paragraph(value)
					.SetFontSize(9)
					.SetTextAlignment(
						TextAlignment.CENTER));
		}


		private static string CombinePhoneAndVat()
		{
			var parts =
				new List<string>();

			if (!string.IsNullOrWhiteSpace(SellerPhone))
			{
				parts.Add(
					$"Phone: {SellerPhone}");
			}

			if (!string.IsNullOrWhiteSpace(SellerVatNumber))
			{
				parts.Add(
					$"VAT/PAN: {SellerVatNumber}");
			}

			return
				string.Join(
					"  |  ",
					parts);
		}


		private static void AddBuyerLine(
			Cell cell,
			string label,
			string value,
			PdfFont font)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return;
			}

			cell.Add(
				new Paragraph(
					$"{label}: {value}")
					.SetFont(font)
					.SetFontSize(10));
		}


		private static bool IsNumericColumn(
			string header)
		{
			return
				header == "S.N." ||
				header == "Qty" ||
				header == "Unit Price" ||
				header == "Amount";
		}


		private static Cell StyledCell(
			string text,
			PdfFont font,
			TextAlignment alignment)
		{
			return
				new Cell()
					.SetFont(font)
					.SetFontSize(9)
					.SetTextAlignment(alignment)
					.SetPadding(5f)
					.Add(
						new Paragraph(text));
		}


		private static void AddTotalRow(
			Table table,
			string label,
			decimal amount,
			PdfFont font,
			DeviceRgb? background)
		{
			var labelCell =
				new Cell()
					.SetFont(font)
					.SetFontSize(10)
					.SetTextAlignment(
						TextAlignment.RIGHT)
					.SetPadding(5f);

			var amountCell =
				new Cell()
					.SetFont(font)
					.SetFontSize(10)
					.SetTextAlignment(
						TextAlignment.RIGHT)
					.SetPadding(5f)
					.Add(
						new Paragraph(
							amount.ToString("N2")));

			if (background != null)
			{
				labelCell
					.SetBackgroundColor(background);

				amountCell
					.SetBackgroundColor(background);
			}

			labelCell.Add(
				new Paragraph(
					$"{label}   "));

			table.AddCell(
				labelCell);

			table.AddCell(
				amountCell);
		}


		// ============================================================
		// AMOUNT IN WORDS
		// ============================================================

		private static string AmountInWords(
			decimal amount)
		{
			amount =
				Math.Round(
					amount,
					2,
					MidpointRounding.AwayFromZero);

			long rupees =
				(long)amount;

			int paisa =
				(int)Math.Round(
					(amount - rupees) * 100m);

			string words =
				$"Rupees {RupeesToWords(rupees)}";

			if (paisa > 0)
			{
				words +=
					$" and {TwoOrThreeDigitsToWords(paisa)} Paisa";
			}

			return
				$"{words} Only";
		}


		private static string RupeesToWords(
			long value)
		{
			if (value == 0)
			{
				return "Zero";
			}

			var parts =
				new List<string>();

			if (value >= 10_000_000)
			{
				parts.Add(
					$"{RupeesToWords(value / 10_000_000)} Crore");

				value %=
					10_000_000;
			}

			if (value >= 100_000)
			{
				parts.Add(
					$"{TwoOrThreeDigitsToWords((int)(value / 100_000))} Lakh");

				value %=
					100_000;
			}

			if (value >= 1_000)
			{
				parts.Add(
					$"{TwoOrThreeDigitsToWords((int)(value / 1_000))} Thousand");

				value %=
					1_000;
			}

			if (value > 0)
			{
				parts.Add(
					TwoOrThreeDigitsToWords(
						(int)value));
			}

			return
				string.Join(
					" ",
					parts);
		}


		private static string TwoOrThreeDigitsToWords(
			int number)
		{
			if (number == 0)
			{
				return "Zero";
			}

			var parts =
				new List<string>();

			if (number >= 100)
			{
				parts.Add(
					$"{OnesWords[number / 100]} Hundred");

				number %=
					100;
			}

			if (number >= 20)
			{
				parts.Add(
					TensWords[number / 10]);

				number %=
					10;
			}

			if (number > 0)
			{
				parts.Add(
					OnesWords[number]);
			}

			return
				string.Join(
					" ",
					parts);
		}


		private static readonly string[] OnesWords =
		{
			string.Empty,
			"One",
			"Two",
			"Three",
			"Four",
			"Five",
			"Six",
			"Seven",
			"Eight",
			"Nine",
			"Ten",
			"Eleven",
			"Twelve",
			"Thirteen",
			"Fourteen",
			"Fifteen",
			"Sixteen",
			"Seventeen",
			"Eighteen",
			"Nineteen"
		};


		private static readonly string[] TensWords =
		{
			string.Empty,
			string.Empty,
			"Twenty",
			"Thirty",
			"Forty",
			"Fifty",
			"Sixty",
			"Seventy",
			"Eighty",
			"Ninety"
		};
	}
}
