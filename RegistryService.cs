using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ContextMenuManager
{
    public class RegistryService
    {
        private const string REG_PATH = @"Software\Classes\Directory\Background\shell";
        private const string DIR_REG_PATH = @"Software\Classes\Directory\shell";
        private const string CLASSIC_MENU_PATH = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";

        public static List<ShortcutItem> LoadShortcuts()
        {
            var shortcuts = new List<ShortcutItem>();

            // 1. Root Level CustomFolder_ items
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(REG_PATH))
                {
                    if (key != null)
                    {
                        foreach (var subkeyName in key.GetSubKeyNames())
                        {
                            if (subkeyName.StartsWith("CustomFolder_"))
                            {
                                string displayName = subkeyName;
                                string path = string.Empty;

                                using (var subkey = key.OpenSubKey(subkeyName))
                                {
                                    if (subkey != null)
                                    {
                                        displayName = subkey.GetValue("")?.ToString() ?? subkeyName;
                                        using (var cmdkey = subkey.OpenSubKey("command"))
                                        {
                                            if (cmdkey != null)
                                            {
                                                var cmd = cmdkey.GetValue("")?.ToString() ?? string.Empty;
                                                path = ExtractPath(cmd);
                                            }
                                        }
                                    }
                                }

                                shortcuts.Add(new ShortcutItem
                                {
                                    Id = subkeyName,
                                    Group = "Ana Menü",
                                    Name = displayName,
                                    Path = path,
                                    IsFolder = IsFolderPath(path)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Root kısayollar yüklenirken hata oluştu: {ex.Message}");
            }

            // 2. Nested CustomGroup_ items
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(REG_PATH))
                {
                    if (key != null)
                    {
                        foreach (var groupKeyName in key.GetSubKeyNames())
                        {
                            if (groupKeyName.StartsWith("CustomGroup_"))
                            {
                                string groupDisplayName = groupKeyName.Replace("CustomGroup_", "");

                                using (var gkey = key.OpenSubKey(groupKeyName))
                                {
                                    if (gkey != null)
                                    {
                                        groupDisplayName = gkey.GetValue("MUIVerb")?.ToString() ?? groupDisplayName;

                                        using (var shellkey = gkey.OpenSubKey("shell"))
                                        {
                                            if (shellkey != null)
                                            {
                                                foreach (var itemKeyName in shellkey.GetSubKeyNames())
                                                {
                                                    if (itemKeyName.StartsWith("CustomItem_"))
                                                    {
                                                        string displayName = itemKeyName;
                                                        string path = string.Empty;

                                                        using (var subkey = shellkey.OpenSubKey(itemKeyName))
                                                        {
                                                            if (subkey != null)
                                                            {
                                                                displayName = subkey.GetValue("")?.ToString() ?? itemKeyName;
                                                                using (var cmdkey = subkey.OpenSubKey("command"))
                                                                {
                                                                    if (cmdkey != null)
                                                                    {
                                                                        var cmd = cmdkey.GetValue("")?.ToString() ?? string.Empty;
                                                                        path = ExtractPath(cmd);
                                                                    }
                                                                }
                                                            }
                                                        }

                                                        string fullDelPath = $"{groupKeyName}\\shell\\{itemKeyName}";
                                                        shortcuts.Add(new ShortcutItem
                                                        {
                                                            Id = fullDelPath,
                                                            Group = groupDisplayName,
                                                            Name = displayName,
                                                            Path = path,
                                                            IsFolder = IsFolderPath(path)
                                                        });
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Grup kısayolları yüklenirken hata oluştu: {ex.Message}");
            }

            return shortcuts;
        }

        public static List<string> GetExistingGroups()
        {
            var groups = new List<string> { "Ana Menü" };
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(REG_PATH))
                {
                    if (key != null)
                    {
                        foreach (var name in key.GetSubKeyNames())
                        {
                            if (name.StartsWith("CustomGroup_"))
                            {
                                using (var gkey = key.OpenSubKey(name))
                                {
                                    if (gkey != null)
                                    {
                                        var display = gkey.GetValue("MUIVerb")?.ToString();
                                        if (!string.IsNullOrEmpty(display) && !groups.Contains(display))
                                        {
                                            groups.Add(display);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return groups;
        }

        public static void AddShortcut(string name, string path, string group, bool isFolder)
        {
            string absolutePath = Path.GetFullPath(path);
            string cmdVal = isFolder ? $@"explorer.exe ""{absolutePath}""" : $@"""{absolutePath}""";
            string iconPath = isFolder ? "explorer.exe" : absolutePath;

            string cleanItemName = Regex.Replace(name, @"\W+", "");
            long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string itemKeyName = $"CustomItem_{cleanItemName}_{unixTimestamp}";

            if (string.IsNullOrEmpty(group) || group == "Ana Menü")
            {
                // Root shortcut
                string rootKeyName = $"CustomFolder_{cleanItemName}_{unixTimestamp}";
                string fullPath = $@"{REG_PATH}\{rootKeyName}";

                using (var key = Registry.CurrentUser.CreateSubKey(fullPath))
                {
                    key.SetValue("", name);
                    key.SetValue("Icon", iconPath);
                    using (var cmdkey = key.CreateSubKey("command"))
                    {
                        cmdkey.SetValue("", cmdVal);
                    }
                }
            }
            else
            {
                // Group shortcut
                string cleanGroupName = Regex.Replace(group, @"\W+", "");
                string groupKeyName = $"CustomGroup_{cleanGroupName}";
                string groupPath = $@"{REG_PATH}\{groupKeyName}";

                using (var gkey = Registry.CurrentUser.CreateSubKey(groupPath))
                {
                    gkey.SetValue("MUIVerb", group);
                    gkey.SetValue("SubCommands", "");
                    gkey.SetValue("Icon", "shell32.dll,3"); // standard folder icon
                }

                string itemPath = $@"{groupPath}\shell\{itemKeyName}";
                using (var key = Registry.CurrentUser.CreateSubKey(itemPath))
                {
                    key.SetValue("", name);
                    key.SetValue("Icon", iconPath);
                    using (var cmdkey = key.CreateSubKey("command"))
                    {
                        cmdkey.SetValue("", cmdVal);
                    }
                }
            }
        }

        public static void DeleteShortcut(string keyId)
        {
            if (keyId.Contains(@"\"))
            {
                // Nested item: groupKey\shell\itemKey
                var parts = keyId.Split('\\');
                if (parts.Length == 3)
                {
                    string groupKey = parts[0];
                    string itemKey = parts[2];

                    string itemPath = $@"{REG_PATH}\{groupKey}\shell\{itemKey}";
                    Registry.CurrentUser.DeleteSubKeyTree(itemPath, false);

                    // Clean up group if empty
                    string groupShellPath = $@"{REG_PATH}\{groupKey}\shell";
                    bool hasItems = false;
                    using (var gshell = Registry.CurrentUser.OpenSubKey(groupShellPath))
                    {
                        if (gshell != null && gshell.SubKeyCount > 0)
                        {
                            hasItems = true;
                        }
                    }

                    if (!hasItems)
                    {
                        Registry.CurrentUser.DeleteSubKeyTree(groupShellPath, false);
                        Registry.CurrentUser.DeleteSubKeyTree($@"{REG_PATH}\{groupKey}", false);
                    }
                }
            }
            else
            {
                // Root item
                string fullPath = $@"{REG_PATH}\{keyId}";
                Registry.CurrentUser.DeleteSubKeyTree(fullPath, false);
            }
        }

        public static bool CheckPowerShellStatus()
        {
            string psAlwaysPath = $@"{REG_PATH}\PowershellAlways";
            using (var key = Registry.CurrentUser.OpenSubKey(psAlwaysPath))
            {
                return key != null;
            }
        }

        public static void TogglePowerShell(bool enable)
        {
            string[] paths = { REG_PATH, DIR_REG_PATH };

            foreach (var basePath in paths)
            {
                string psAlwaysPath = $@"{basePath}\PowershellAlways";
                string psDefaultPath = $@"{basePath}\Powershell";

                if (enable)
                {
                    // Create custom powershell entry
                    using (var key = Registry.CurrentUser.CreateSubKey(psAlwaysPath))
                    {
                        key.SetValue("", "PowerShell Penceresini Burada Aç");
                        key.SetValue("Icon", "powershell.exe");

                        using (var cmdkey = key.CreateSubKey("command"))
                        {
                            string param = basePath.Contains("Background") ? "%V" : "%1";
                            string cmdVal = $@"powershell.exe -NoExit -Command Set-Location -LiteralPath '{param}'";
                            cmdkey.SetValue("", cmdVal);
                        }
                    }

                    // Hide default shift-only powershell entry
                    using (var key = Registry.CurrentUser.CreateSubKey(psDefaultPath))
                    {
                        key.SetValue("LegacyDisable", "");
                    }
                }
                else
                {
                    // Remove custom entry
                    Registry.CurrentUser.DeleteSubKeyTree(psAlwaysPath, false);

                    // Restore default shift-only entry
                    using (var key = Registry.CurrentUser.OpenSubKey(psDefaultPath, true))
                    {
                        if (key != null)
                        {
                            try { key.DeleteValue("LegacyDisable"); } catch { }
                        }
                    }
                    // Clean up key if empty
                    using (var key = Registry.CurrentUser.OpenSubKey(psDefaultPath))
                    {
                        if (key != null && key.SubKeyCount == 0 && key.ValueCount == 0)
                        {
                            Registry.CurrentUser.DeleteSubKey(psDefaultPath, false);
                        }
                    }
                }
            }
        }

        public static bool CheckClassicMenuStatus()
        {
            string inprocPath = $@"{CLASSIC_MENU_PATH}\InprocServer32";
            using (var key = Registry.CurrentUser.OpenSubKey(inprocPath))
            {
                return key != null;
            }
        }

        public static void ToggleClassicMenu(bool enable)
        {
            if (enable)
            {
                using (var key = Registry.CurrentUser.CreateSubKey($@"{CLASSIC_MENU_PATH}\InprocServer32"))
                {
                    key.SetValue("", "");
                }
            }
            else
            {
                Registry.CurrentUser.DeleteSubKeyTree(CLASSIC_MENU_PATH, false);
            }
        }

        private static string ExtractPath(string cmd)
        {
            if (string.IsNullOrEmpty(cmd)) return string.Empty;

            // explorer.exe "path"
            var match = Regex.Match(cmd, @"explorer\.exe\s+""([^""]+)""");
            if (match.Success) return match.Groups[1].Value;

            match = Regex.Match(cmd, @"explorer\.exe\s+(.+)");
            if (match.Success) return match.Groups[1].Value;

            // Just quoted file path
            match = Regex.Match(cmd, @"^""([^""]+)""");
            if (match.Success) return match.Groups[1].Value;

            return cmd;
        }

        private static bool IsFolderPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return false;
                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }
    }
}
