using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using System;
using System.Data;

using ItextPdfDocument = iText.Kernel.Pdf.PdfDocument;


namespace NationalAgroTrading.WinUI.Views.Ledger
{
	public class LedgerPdfExporter
	{
		private readonly string companyName;
		private readonly string fiscalYear;
		private readonly DataTable ledgerData;


		public LedgerPdfExporter(
			string companyName,
			string fiscalYear,
			DataTable ledgerData)
		{
			this.companyName =
				companyName ?? string.Empty;

			this.fiscalYear =
				fiscalYear ?? string.Empty;

			this.ledgerData =
				ledgerData
				?? throw new ArgumentNullException(
					nameof(ledgerData));
		}


		// ============================================================
		// EXPORT
		// ============================================================

		public void ExportToPdf(
			string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath))
			{
				throw new ArgumentException(
					"File path is required.",
					nameof(filePath));
			}


			using (var writer =
				new PdfWriter(filePath))
			using (var pdf =
				new PdfDocument(writer))
			{
				var document =
					new Document(pdf);


				// ====================================================
				// FONTS
				// ====================================================

				PdfFont boldFont =
					PdfFontFactory.CreateFont(
						StandardFonts.HELVETICA_BOLD);

				PdfFont normalFont =
					PdfFontFactory.CreateFont(
						StandardFonts.HELVETICA);


				// ====================================================
				// COMPANY
				// ====================================================

				document.Add(
					new Paragraph(companyName)
						.SetFont(boldFont)
						.SetFontSize(16)
						.SetTextAlignment(
							TextAlignment.CENTER));


				// ====================================================
				// FISCAL YEAR
				// ====================================================

				document.Add(
					new Paragraph(
						$"Ledger Report - Fiscal Year: {fiscalYear}")
						.SetFont(normalFont)
						.SetFontSize(10)
						.SetTextAlignment(
							TextAlignment.CENTER));


				document.Add(
					new Paragraph(" "));


				// ====================================================
				// TABLE
				// ====================================================

				Table table =
					new Table(
						ledgerData.Columns.Count)
					.UseAllAvailableWidth();


				// ====================================================
				// HEADER
				// ====================================================

				foreach (
					DataColumn column
					in ledgerData.Columns)
				{
					Cell headerCell =
						new Cell()
							.Add(
								new Paragraph(
									column.ColumnName))
							.SetBackgroundColor(
								ColorConstants.LIGHT_GRAY)
							.SetFont(normalFont)
							.SetFontSize(9);

					table.AddHeaderCell(
						headerCell);
				}


				// ====================================================
				// DATA
				// ====================================================

				foreach (
					DataRow row
					in ledgerData.Rows)
				{
					foreach (
						object item
						in row.ItemArray)
					{
						string text =
							item == null ||
							item == DBNull.Value
								? ""
								: item.ToString()
								  ?? "";

						Cell cell =
							new Cell()
								.Add(
									new Paragraph(text))
								.SetFont(normalFont)
								.SetFontSize(9);

						table.AddCell(cell);
					}
				}


				// ====================================================
				// ADD TABLE
				// ====================================================

				document.Add(table);


				// ====================================================
				// CLOSE
				// ====================================================

				document.Close();
			}
		}
	}
}