using System;
using System.Collections.Generic;
using System.Linq;

namespace NationalAgroTrading.WinUI.Services
{
	// The single source of truth for bill discount allocation and VAT.
	//
	// Rule: bill discount is allocated to each line item in proportion
	// to its subtotal; VAT (13%) is charged on the after-discount
	// amount of vattable items only, matching Nepali VAT practice.
	//
	// Every amount is rounded to 2 decimal places (AwayFromZero) at
	// this layer, so the confirmation dialog, the stored rows and the
	// printed invoice all show the same numbers.
	public static class TaxCalculator
	{
		public const decimal VatRate = 0.13m;

		public static decimal Round2(decimal value) =>
			Math.Round(value, 2, MidpointRounding.AwayFromZero);

		// Pro-rata share of the bill discount for one line item.
		public static decimal ItemDiscount(
			decimal itemSubtotal,
			decimal billSubtotal,
			decimal totalDiscount) =>
			Round2(
				billSubtotal == 0m
					? 0m
					: itemSubtotal /
					  billSubtotal *
					  totalDiscount);

		// Line subtotal minus its share of the bill discount.
		public static decimal ItemAfterDiscount(
			decimal itemSubtotal,
			decimal billSubtotal,
			decimal totalDiscount) =>
			Round2(
				itemSubtotal -
				ItemDiscount(
					itemSubtotal,
					billSubtotal,
					totalDiscount));

		// VAT for one line item, charged on the after-discount amount.
		public static decimal VatOnAmount(
			decimal afterDiscount,
			bool isVattable) =>
			isVattable
				? Round2(afterDiscount * VatRate)
				: 0m;

		// VAT for one line item inside a bill with a bill-level discount.
		public static decimal ItemVat(
			decimal itemSubtotal,
			decimal billSubtotal,
			decimal totalDiscount,
			bool isVattable) =>
			VatOnAmount(
				ItemAfterDiscount(
					itemSubtotal,
					billSubtotal,
					totalDiscount),
				isVattable);

		// Total bill VAT: sums the same rounded per-item VAT amounts
		// that get stored on each row, so displayed = stored = printed.
		public static decimal BillVat(
			IEnumerable<(decimal Subtotal, bool IsVattable)> items,
			decimal billSubtotal,
			decimal totalDiscount) =>
			items.Sum(item =>
				ItemVat(
					item.Subtotal,
					billSubtotal,
					totalDiscount,
					item.IsVattable));
	}
}
