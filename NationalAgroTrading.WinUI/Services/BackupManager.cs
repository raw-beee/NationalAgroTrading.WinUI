using System;
using System.IO;
using System.Linq;

namespace NationalAgroTrading.WinUI.Services
{
	// Copies the database to the configured backup folder once per day,
	// on startup and on window close, keeping the most recent N copies.
	// Safe to call any number of times per day; only the first call copies.
	public static class BackupManager
	{
		private const int KeepBackups = 30;

		public static void EnsureDailyBackup()
		{
			try
			{
				string db = AppConfig.DatabasePath;

				if (!File.Exists(db))
					return;

				Directory.CreateDirectory(AppConfig.BackupFolder);

				string target = Path.Combine(
					AppConfig.BackupFolder,
					DateTime.Now.ToString("yyyy-MM-dd") + ".db");

				if (File.Exists(target))
					return;

				File.Copy(db, target);

				PruneOldBackups();
			}
			catch (Exception ex)
			{
				TryLog("Backup failed: " + ex.Message);
			}
		}

		private static void PruneOldBackups()
		{
			// yyyy-MM-dd file names sort lexically the same as chronologically.
			var oldest = new DirectoryInfo(AppConfig.BackupFolder)
				.GetFiles("*.db")
				.OrderByDescending(f => f.Name)
				.Skip(KeepBackups);

			foreach (var file in oldest)
			{
				try
				{
					file.Delete();
				}
				catch
				{
					// In use or locked — retried on the next backup run.
				}
			}
		}

		private static void TryLog(string message)
		{
			try
			{
				Directory.CreateDirectory(AppConfig.BackupFolder);

				File.AppendAllText(
					Path.Combine(AppConfig.BackupFolder, "backup-log.txt"),
					DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
						"  " + message + Environment.NewLine);
			}
			catch
			{
				// Nothing further we can report to.
			}
		}
	}
}
