using ClosedXML.Excel;
using System;
using System.Data;

namespace NationalAgroTrading.WinUI.Views.Ledger
{
	public static class ExcelExporter
	{
		public static void ExportDataTableToExcel(
			DataTable dataTable,
			string filePath)
		{
			if (dataTable == null)
			{
				throw new ArgumentNullException(
					nameof(dataTable));
			}

			if (string.IsNullOrWhiteSpace(filePath))
			{
				throw new ArgumentException(
					"File path is required.",
					nameof(filePath));
			}

			using (var workbook = new XLWorkbook())
			{
				var worksheet =
					workbook.Worksheets.Add(
						"Ledger Report");

				worksheet.Cell(
					1,
					1)
					.InsertTable(dataTable);

				worksheet
					.Columns()
					.AdjustToContents();

				workbook.SaveAs(filePath);
			}
		}
	}
}