using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NepDate;
using System;
using System.Globalization;

namespace NationalAgroTrading.WinUI.Controls
{
	public sealed partial class NepaliDatePicker : UserControl
	{
		// ============================================================
		// FIELDS
		// ============================================================

		private DateTime selectedAdDate;

		private bool updatingText;

		private bool settingDate;


		// ============================================================
		// CONSTRUCTOR
		// ============================================================

		public NepaliDatePicker()
		{
			InitializeComponent();

			SetTodayAsDefault();
		}


		// ============================================================
		// PUBLIC PROPERTIES
		// ============================================================

		public string SelectedBSDate
		{
			get
			{
				return DateTextBox.Text.Trim();
			}
		}


		public DateTime SelectedADDate
		{
			get
			{
				return selectedAdDate;
			}
		}


		public bool IsDateValid
		{
			get
			{
				return TryGetSelectedDate(
					out _,
					out _);
			}
		}


		// ============================================================
		// GET SELECTED DATE
		// ============================================================

		public bool TryGetSelectedDate(
			out DateTime adDate,
			out string bsDate)
		{
			adDate = default;
			bsDate = string.Empty;

			string text =
				DateTextBox.Text.Trim();

			if (string.IsNullOrWhiteSpace(text))
				return false;

			string[] parts =
				text.Split(
					'-',
					StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length != 3)
				return false;

			if (!int.TryParse(
					parts[0],
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out int year))
			{
				return false;
			}

			if (!int.TryParse(
					parts[1],
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out int month))
			{
				return false;
			}

			if (!int.TryParse(
					parts[2],
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out int day))
			{
				return false;
			}

			try
			{
				var bs =
					new NepaliDate(
						year,
						month,
						day);

				adDate =
					bs.EnglishDate.Date;

				bsDate =
					$"{bs.Year}-{bs.Month:D2}-{bs.Day:D2}";

				return true;
			}
			catch
			{
				return false;
			}
		}


		// ============================================================
		// SET TODAY
		// ============================================================

		private void SetTodayAsDefault()
		{
			SetADDate(
				DateTime.Today);
		}


		// ============================================================
		// SET AD DATE
		// ============================================================

		public void SetADDate(
			DateTime adDate)
		{
			settingDate = true;

			try
			{
				selectedAdDate =
					adDate.Date;

				var bs =
					new NepaliDate(
						selectedAdDate);

				updatingText = true;

				DateTextBox.Text =
					$"{bs.Year}-{bs.Month:D2}-{bs.Day:D2}";

				updatingText = false;

				AdCalendar.Date =
					selectedAdDate;
			}
			finally
			{
				settingDate = false;
			}
		}


		// ============================================================
		// SET BS DATE
		// ============================================================

		public void SetBSDate(
			int year,
			int month,
			int day)
		{
			try
			{
				var bs =
					new NepaliDate(
						year,
						month,
						day);

				selectedAdDate =
					bs.EnglishDate.Date;

				settingDate = true;

				try
				{
					updatingText = true;

					DateTextBox.Text =
						$"{bs.Year}-{bs.Month:D2}-{bs.Day:D2}";

					updatingText = false;

					AdCalendar.Date =
						selectedAdDate;
				}
				finally
				{
					settingDate = false;
				}
			}
			catch
			{
				// Invalid Nepali date.
				// Leave the current valid date unchanged.
			}
		}


		// ============================================================
		// MANUAL TEXT CHANGE
		// ============================================================

		private void DateTextBox_TextChanged(
			object sender,
			TextChangedEventArgs e)
		{
			if (updatingText ||
				settingDate)
			{
				return;
			}

			string text =
				DateTextBox.Text.Trim();

			if (string.IsNullOrWhiteSpace(text))
				return;

			string[] parts =
				text.Split(
					'-',
					StringSplitOptions.RemoveEmptyEntries);

			// While user is typing, don't immediately reject
			// incomplete input such as "2083-" or "2083-05".
			if (parts.Length != 3)
				return;

			if (!int.TryParse(
					parts[0],
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out int year))
			{
				return;
			}

			if (!int.TryParse(
					parts[1],
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out int month))
			{
				return;
			}

			if (!int.TryParse(
					parts[2],
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out int day))
			{
				return;
			}

			try
			{
				var bs =
					new NepaliDate(
						year,
						month,
						day);

				selectedAdDate =
					bs.EnglishDate.Date;

				AdCalendar.Date =
					selectedAdDate;
			}
			catch
			{
				// Invalid date.
				// The user can continue editing the field.
			}
		}


		// ============================================================
		// CALENDAR BUTTON
		// ============================================================

		private void DateButton_Click(
			object sender,
			RoutedEventArgs e)
		{
			AdCalendar.Date =
				selectedAdDate;

			CalendarPopup.IsOpen =
				true;
		}


		// ============================================================
		// AD CALENDAR DATE CHANGED
		// ============================================================

		private void AdCalendar_DateChanged(
			CalendarDatePicker sender,
			CalendarDatePickerDateChangedEventArgs args)
		{
			if (args.NewDate == null)
				return;

			DateTime date =
				args.NewDate.Value.Date;

			SetADDate(date);

			CalendarPopup.IsOpen =
				false;
		}


		// ============================================================
		// POPUP CLOSED
		// ============================================================

		private void CalendarPopup_Closed(
			object sender,
			object e)
		{
			AdCalendar.Date =
				selectedAdDate;
		}
	}
}