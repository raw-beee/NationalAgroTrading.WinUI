using System;
using System.IO;

namespace NationalAgroTrading.WinUI.Helpers
{
	public static class LastExportPathHelper
	{
		private static readonly string pathFile =
			Path.Combine(
				AppDomain.CurrentDomain.BaseDirectory,
				"last_export_path.txt");

		public static string GetLastExportPath()
		{
			if (File.Exists(pathFile))
			{
				return File.ReadAllText(pathFile);
			}

			return null;
		}

		public static void SaveLastExportPath(string folderPath)
		{
			try
			{
				File.WriteAllText(pathFile, folderPath);
			}
			catch (Exception ex)
			{
				Console.WriteLine(
					"Error saving path: " + ex.Message);
			}
		}
	}
}