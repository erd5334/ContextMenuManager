using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ContextMenuManager
{
    public class LockedItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public string TargetType { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
    }

    public class LockedGroup
    {
        public string PasswordHash { get; set; } = string.Empty;
        public int UnlockDurationSeconds { get; set; } = 90;
        public string GroupIconPath { get; set; } = string.Empty;
        public string GroupPosition { get; set; } = string.Empty;
        public List<LockedItem> Items { get; set; } = new List<LockedItem>();
    }

    public static class LockSettings
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "SagTikYoneticisi"
        );
        private static readonly string SettingsFile = Path.Combine(SettingsFolder, "lock_settings.json");

        public static Dictionary<string, LockedGroup> Load()
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }

                if (!File.Exists(SettingsFile))
                {
                    return new Dictionary<string, LockedGroup>(StringComparer.OrdinalIgnoreCase);
                }

                string json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<Dictionary<string, LockedGroup>>(json) 
                       ?? new Dictionary<string, LockedGroup>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ayarlar yüklenirken hata oluştu: {ex.Message}");
                return new Dictionary<string, LockedGroup>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public static void Save(Dictionary<string, LockedGroup> settings)
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ayarlar kaydedilirken hata oluştu: {ex.Message}");
            }
        }

        public static string ComputeSha256(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
