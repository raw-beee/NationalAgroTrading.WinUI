using System;
using System.Data;

namespace NationalAgroTrading.WinUI.Views.Ledger
{
	public class LedgerPrinter
	{
		private readonly DataTable ledgerTable;
		private readonly string title;


		public LedgerPrinter(
			DataTable ledgerTable,
			string title = "Ledger Report")
		{
			this.ledgerTable =
				ledgerTable
				?? throw new ArgumentNullException(
					nameof(ledgerTable));

			this.title =
				title;
		}


		public void Print()
		{
			if (ledgerTable.Rows.Count == 0)
			{
				throw new InvalidOperationException(
					"No data to print.");
			}

			throw new NotSupportedException(
				"WinForms printing is not used in WinUI 3. " +
				"The Ledger printing implementation will be migrated " +
				"to the WinUI printing mechanism separately.");
		}
	}
}