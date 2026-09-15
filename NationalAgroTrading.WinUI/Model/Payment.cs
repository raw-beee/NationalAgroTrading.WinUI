using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NationalAgroTrading.WinUI.Models
{
	public class Payment
	{
		public int CompanyID { get; set; }

		public DateTime PaymentDate { get; set; }

		public decimal Amount { get; set; }

		public string PaymentType { get; set; }

		public string Receipt { get; set; }
	}
}