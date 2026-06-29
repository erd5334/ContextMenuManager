using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ContextMenuManager
{
    public class RegistryService
    {
        private const string REG_PATH_BG = @"Software\Classes\Directory\Background\shell";
        private const string REG_PATH_DIR = @"Software\Classes\Directory\shell";
        private const string REG_PATH_FILE = @"Software\Classes\*\shell";
        private const string CLASSIC_MENU_PATH = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";

        public static string GetRegistryPath(string targetType)
        {
            if (targetType.StartsWith("FileExtension:"))
            {
                string ext = targetType.Substring("FileExtension:".Length);
                return $@"Software\Classes\SystemFileAssociations\{ext}\shell";
            }

            return targetType switch
            {
                "Background" => REG_PATH_BG,
                "Directory" => REG_PATH_DIR,
                "AllFiles" => REG_PATH_FILE,
                _ => REG_PATH_BG
            };
        }

        public static List<ShortcutItem> LoadShortcuts()
        {
            var shortcuts = new List<ShortcutItem>();

            // Load from all three target locations
            shortcuts.AddRange(LoadShortcutsFromKey(REG_PATH_BG, "Background", "Boş Alan"));
            shortcuts.AddRange(LoadShortcutsFromKey(REG_PATH_DIR, "Directory", "Klasör"));
            shortcuts.AddRange(LoadShortcutsFromKey(REG_PATH_FILE, "AllFiles", "Tüm Dosyalar"));

            // Load from file extension associations
            try
            {
                using (var baseKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SystemFileAssociations"))
                {
                    if (baseKey != null)
                    {
                        foreach (var ext in baseKey.GetSubKeyNames())
                        {
                            string shellPath = $@"Software\Classes\SystemFileAssociations\{ext}\shell";
                            shortcuts.AddRange(LoadShortcutsFromKey(shellPath, $"FileExtension:{ext}", $"Uzantı ({ext})"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Uzantı kısayolları yüklenirken hata: {ex.Message}");
            }

            return shortcuts;
        }

        private static List<ShortcutItem> LoadShortcutsFromKey(string regPath, string targetType, string targetDisplay)
        {
            var list = new List<ShortcutItem>();

            // 1. Root Level CustomFolder_ items
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(regPath))
                {
                    if (key != null)
                    {
                        foreach (var subkeyName in key.GetSubKeyNames())
                        {
                            if (subkeyName.StartsWith("CustomFolder_"))
                            {
                                string displayName = subkeyName;
                                string path = string.Empty;
                                string position = "Default";
                                string iconPath = string.Empty;

                                using (var subkey = key.OpenSubKey(subkeyName))
                                {
                                    if (subkey != null)
                                    {
                                        displayName = subkey.GetValue("")?.ToString() ?? subkeyName;
                                        position = subkey.GetValue("Position")?.ToString() ?? "Default";
                                        iconPath = subkey.GetValue("Icon")?.ToString() ?? string.Empty;
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

                                list.Add(new ShortcutItem
                                {
                                    Id = $"{targetType}|{subkeyName}",
                                    Group = "Ana Menü",
                                    Name = displayName,
                                    Path = path,
                                    IsFolder = IsFolderPath(path),
                                    TargetType = targetType,
                                    TargetDisplay = targetDisplay,
                                    Position = position,
                                    IconPath = iconPath
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Root kısayollar yüklenirken hata oluştu ({regPath}): {ex.Message}");
            }

            // 2. Nested CustomGroup_ items
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(regPath))
                {
                    if (key != null)
                    {
                        foreach (var groupKeyName in key.GetSubKeyNames())
                        {
                            if (groupKeyName.StartsWith("CustomGroup_"))
                            {
                                string groupDisplayName = groupKeyName.Replace("CustomGroup_", "");
                                string position = "Default";

                                using (var gkey = key.OpenSubKey(groupKeyName))
                                {
                                    if (gkey != null)
                                    {
                                        groupDisplayName = gkey.GetValue("MUIVerb")?.ToString() ?? groupDisplayName;
                                        position = gkey.GetValue("Position")?.ToString() ?? "Default";

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
                                                        string iconPath = string.Empty;

                                                        using (var subkey = shellkey.OpenSubKey(itemKeyName))
                                                        {
                                                            if (subkey != null)
                                                            {
                                                                displayName = subkey.GetValue("")?.ToString() ?? itemKeyName;
                                                                iconPath = subkey.GetValue("Icon")?.ToString() ?? string.Empty;
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
                                                        list.Add(new ShortcutItem
                                                        {
                                                            Id = $"{targetType}|{fullDelPath}",
                                                            Group = groupDisplayName,
                                                            Name = displayName,
                                                            Path = path,
                                                            IsFolder = IsFolderPath(path),
                                                            TargetType = targetType,
                                                            TargetDisplay = targetDisplay,
                                                            Position = position,
                                                            IconPath = iconPath
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
                Console.WriteLine($"Grup kısayolları yüklenirken hata oluştu ({regPath}): {ex.Message}");
            }

            return list;
        }

        public static List<string> GetExistingGroups()
        {
            var groups = new List<string> { "Ana Menü" };
            string[] paths = { REG_PATH_BG, REG_PATH_DIR, REG_PATH_FILE };

            foreach (var regPath in paths)
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(regPath))
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
            }
            return groups;
        }

        public static void AddShortcut(string name, string path, string group, bool isFolder, string targetType, string position, string customIconPath)
        {
            string rootPath = GetRegistryPath(targetType);
            string absolutePath = Path.GetFullPath(path);
            string cmdVal;
            if (targetType == "Background")
            {
                cmdVal = isFolder ? $@"explorer.exe ""{absolutePath}""" : $@"""{absolutePath}""";
            }
            else // Directory or AllFiles
            {
                cmdVal = isFolder ? $@"explorer.exe ""{absolutePath}""" : $@"""{absolutePath}"" ""%1""";
            }
            
            string iconPath = customIconPath;
            if (string.IsNullOrEmpty(iconPath))
            {
                iconPath = isFolder ? "explorer.exe" : absolutePath;
            }

            string cleanItemName = Regex.Replace(name, @"\W+", "");
            long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string itemKeyName = $"CustomItem_{cleanItemName}_{unixTimestamp}";

            if (string.IsNullOrEmpty(group) || group == "Ana Menü")
            {
                // Root shortcut
                string rootKeyName = $"CustomFolder_{cleanItemName}_{unixTimestamp}";
                string fullPath = $@"{rootPath}\{rootKeyName}";

                using (var key = Registry.CurrentUser.CreateSubKey(fullPath))
                {
                    key.SetValue("", name);
                    key.SetValue("Icon", iconPath);
                    
                    if (position == "Top" || position == "Bottom")
                    {
                        key.SetValue("Position", position);
                    }

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
                string groupPath = $@"{rootPath}\{groupKeyName}";

                using (var gkey = Registry.CurrentUser.CreateSubKey(groupPath))
                {
                    gkey.SetValue("MUIVerb", group);
                    gkey.SetValue("SubCommands", "");
                    gkey.SetValue("Icon", !string.IsNullOrEmpty(customIconPath) ? customIconPath : "shell32.dll,3"); // folder icon

                    if (position == "Top" || position == "Bottom")
                    {
                        gkey.SetValue("Position", position);
                    }
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

        public static void AddRawShortcut(string name, string command, string icon, string targetType, string position)
        {
            string rootPath = GetRegistryPath(targetType);
            string cleanName = Regex.Replace(name, @"\W+", "");
            long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string rootKeyName = $"CustomFolder_{cleanName}_{unixTimestamp}";
            string fullPath = $@"{rootPath}\{rootKeyName}";

            using (var key = Registry.CurrentUser.CreateSubKey(fullPath))
            {
                key.SetValue("", name);
                key.SetValue("Icon", icon);
                
                if (position == "Top" || position == "Bottom")
                {
                    key.SetValue("Position", position);
                }

                using (var cmdkey = key.CreateSubKey("command"))
                {
                    cmdkey.SetValue("", command);
                }
            }
        }

        public static void DeleteShortcut(string compoundId)
        {
            var index = compoundId.IndexOf('|');
            if (index == -1) return;

            string targetType = compoundId.Substring(0, index);
            string keyId = compoundId.Substring(index + 1);
            string rootPath = GetRegistryPath(targetType);

            if (keyId.Contains(@"\"))
            {
                // Nested item: groupKey\shell\itemKey
                var parts = keyId.Split('\\');
                if (parts.Length == 3)
                {
                    string groupKey = parts[0];
                    string itemKey = parts[2];

                    string itemPath = $@"{rootPath}\{groupKey}\shell\{itemKey}";
                    Registry.CurrentUser.DeleteSubKeyTree(itemPath, false);

                    // Clean up group if empty
                    string groupShellPath = $@"{rootPath}\{groupKey}\shell";
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
                        Registry.CurrentUser.DeleteSubKeyTree($@"{rootPath}\{groupKey}", false);
                    }
                }
            }
            else
            {
                // Root item
                string fullPath = $@"{rootPath}\{keyId}";
                Registry.CurrentUser.DeleteSubKeyTree(fullPath, false);
            }
        }

        public static bool CheckPowerShellStatus()
        {
            string psAlwaysPath = $@"{REG_PATH_BG}\PowershellAlways";
            using (var key = Registry.CurrentUser.OpenSubKey(psAlwaysPath))
            {
                return key != null;
            }
        }

        public static void TogglePowerShell(bool enable)
        {
            string[] paths = { REG_PATH_BG, REG_PATH_DIR };

            foreach (var basePath in paths)
            {
                string psAlwaysPath = $@"{basePath}\PowershellAlways";
                string psDefaultPath = $@"{basePath}\Powershell";

                if (enable)
                {
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

                    using (var key = Registry.CurrentUser.CreateSubKey(psDefaultPath))
                    {
                        key.SetValue("LegacyDisable", "");
                    }
                }
                else
                {
                    Registry.CurrentUser.DeleteSubKeyTree(psAlwaysPath, false);

                    using (var key = Registry.CurrentUser.OpenSubKey(psDefaultPath, true))
                    {
                        if (key != null)
                        {
                            try { key.DeleteValue("LegacyDisable"); } catch { }
                        }
                    }

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

            var match = Regex.Match(cmd, @"explorer\.exe\s+""([^""]+)""");
            if (match.Success) return match.Groups[1].Value;

            match = Regex.Match(cmd, @"explorer\.exe\s+(.+)");
            if (match.Success) return match.Groups[1].Value;

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

        public static List<ShellExtensionItem> LoadShellExtensions()
        {
            var list = new List<ShellExtensionItem>();
            var uniqueClsids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Read blocked extensions list
            var blockedClsids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var blockedKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked"))
                {
                    if (blockedKey != null)
                    {
                        foreach (var name in blockedKey.GetValueNames())
                        {
                            blockedClsids.Add(name);
                        }
                    }
                }
            }
            catch { }

            string[] paths = {
                @"*\shellex\ContextMenuHandlers",
                @"Directory\shellex\ContextMenuHandlers",
                @"Folder\shellex\ContextMenuHandlers",
                @"Directory\Background\shellex\ContextMenuHandlers"
            };

            foreach (var path in paths)
            {
                string targetDisplay = path switch
                {
                    @"*\shellex\ContextMenuHandlers" => "Tüm Dosyalar",
                    @"Directory\shellex\ContextMenuHandlers" => "Klasör",
                    @"Folder\shellex\ContextMenuHandlers" => "Klasörler (Folder)",
                    @"Directory\Background\shellex\ContextMenuHandlers" => "Boş Alan",
                    _ => "Diğer"
                };

                try
                {
                    using (var key = Registry.ClassesRoot.OpenSubKey(path))
                    {
                        if (key != null)
                        {
                            foreach (var subkeyName in key.GetSubKeyNames())
                            {
                                string clsid = string.Empty;
                                using (var subkey = key.OpenSubKey(subkeyName))
                                {
                                    if (subkey != null)
                                    {
                                        clsid = subkey.GetValue("")?.ToString() ?? string.Empty;
                                    }
                                }

                                // If the value itself is empty/not a guid, check if the key name itself is a guid
                                if (string.IsNullOrEmpty(clsid) || !clsid.StartsWith("{") || !clsid.EndsWith("}"))
                                {
                                    if (subkeyName.StartsWith("{") && subkeyName.EndsWith("}"))
                                    {
                                        clsid = subkeyName;
                                    }
                                }

                                if (!string.IsNullOrEmpty(clsid) && clsid.StartsWith("{") && clsid.EndsWith("}"))
                                {
                                    // Skip system/essential Windows handlers to prevent users from breaking basic OS features
                                    if (IsSystemClsid(clsid) || IsSystemKeyName(subkeyName))
                                    {
                                        continue;
                                    }

                                    string uniqueKey = $"{clsid}|{path}";
                                    if (!uniqueClsids.Contains(uniqueKey))
                                    {
                                        uniqueClsids.Add(uniqueKey);
                                        list.Add(new ShellExtensionItem
                                        {
                                            KeyName = subkeyName,
                                            Clsid = clsid,
                                            RegistryPath = $@"HKCR\{path}\{subkeyName}",
                                            TargetDisplay = targetDisplay,
                                            IsBlocked = blockedClsids.Contains(clsid)
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            return list;
        }

        private static bool IsSystemClsid(string clsid)
        {
            string[] systemClsids = {
                "{09799AFB-AD67-11d1-ABCD-00C04FC30936}", // Open With
                "{f81e9010-6ea4-11ce-a7ff-00aa003ca9f6}", // Sharing
                "{E61BF828-5E63-4287-BEF1-60B1A4FDE0E3}", // WorkFolders
                "{90AA3A4E-1CBA-4233-B8BB-535773D48449}", // Taskband Pin
                "{a2a9545d-a0c2-42b4-9708-a0b2badd77c8}", // Start Menu Pin
                "{851A0071-23C4-42b9-9908-5682957D0850}", // Windows Defender
                "{09A47860-11B0-4DA5-AFA5-26D86198A780}"  // Windows Defender (EPP)
            };

            foreach (var sys in systemClsids)
            {
                if (sys.Equals(clsid, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool IsSystemKeyName(string name)
        {
            string[] systemNames = { "Open With", "Sharing", "WorkFolders", "EPP" };
            foreach (var sys in systemNames)
            {
                if (sys.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static void ToggleShellExtension(string clsid, bool block)
        {
            string blockedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";
            if (block)
            {
                using (var key = Registry.CurrentUser.CreateSubKey(blockedKeyPath))
                {
                    key.SetValue(clsid, "");
                }
            }
            else
            {
                using (var key = Registry.CurrentUser.OpenSubKey(blockedKeyPath, true))
                {
                    if (key != null)
                    {
                        try { key.DeleteValue(clsid); } catch { }
                    }
                }
            }
        }
    }
}
