using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NationalAgroTrading.WinUI.Models
{
	public class PurchaseItem
	{
		public int ProductID { get; set; }
		public int Quantity { get; set; }
		public decimal Rate { get; set; }
		public decimal Amount { get; set; }
	}
}
