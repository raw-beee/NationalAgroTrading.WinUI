using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NationalAgroTrading.WinUI.Services
{
	// Central application settings. Read once at startup from the first
	// config.json found (ProgramData first, then next to the exe).
	// If no config exists, defaults are written so the file is easy to
	// discover and edit. Every default preserves the pre-config behaviour.
	public static class AppConfig
	{
		public sealed class ConfigFile
		{
			public string? DatabasePath { get; set; }

			public string? BackupFolder { get; set; }

			public string? SalesBillsFolder { get; set; }
		}

		public static string DatabasePath { get; private set; } =
			@"D:\National Software\natdatabase.db";

		public static string BackupFolder { get; private set; } =
			Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
				"National Agro Trading",
				"Backups");

		public static string SalesBillsFolder { get; private set; } =
			@"D:\National Software\Sales Bills";

		public static string ConfigFilePath { get; private set; } = "";

		static AppConfig()
		{
			Load();
		}

		private static IEnumerable<string> CandidatePaths()
		{
			yield return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
				"NationalAgroTrading",
				"config.json");

			yield return Path.Combine(
				AppContext.BaseDirectory,
				"config.json");
		}

		private static void Load()
		{
			bool first = true;

			foreach (string path in CandidatePaths())
			{
				if (first)
				{
					ConfigFilePath = path;
					first = false;
				}

				if (!File.Exists(path))
					continue;

				try
				{
					var cfg = JsonSerializer.Deserialize<ConfigFile>(
						File.ReadAllText(path),
						new JsonSerializerOptions
						{
							PropertyNameCaseInsensitive = true
						});

					if (cfg == null)
						continue;

					if (!string.IsNullOrWhiteSpace(cfg.DatabasePath))
						DatabasePath = cfg.DatabasePath;

					if (!string.IsNullOrWhiteSpace(cfg.BackupFolder))
						BackupFolder = cfg.BackupFolder;

					if (!string.IsNullOrWhiteSpace(cfg.SalesBillsFolder))
						SalesBillsFolder = cfg.SalesBillsFolder;

					ConfigFilePath = path;
					return;
				}
				catch
				{
					// A corrupt config falls through to the defaults.
				}
			}

			TryWriteDefault();
		}

		private static void TryWriteDefault()
		{
			try
			{
				Directory.CreateDirectory(
					Path.GetDirectoryName(ConfigFilePath)!);

				File.WriteAllText(
					ConfigFilePath,
					JsonSerializer.Serialize(
						new ConfigFile
						{
							DatabasePath = DatabasePath,
							BackupFolder = BackupFolder,
							SalesBillsFolder = SalesBillsFolder
						},
						new JsonSerializerOptions
						{
							WriteIndented = true
						}));
			}
			catch
			{
				// Read-only location (e.g. packaged install): config stays optional.
			}
		}
	}
}
