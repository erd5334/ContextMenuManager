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
            string cmdVal;
            string absolutePath;

            bool isRaw = IsRawCommand(path, out absolutePath);

            if (isRaw)
            {
                cmdVal = path;
            }
            else
            {
                if (targetType == "Background")
                {
                    cmdVal = isFolder ? $@"explorer.exe ""{absolutePath}""" : $@"""{absolutePath}""";
                }
                else // Directory or AllFiles
                {
                    cmdVal = isFolder ? $@"explorer.exe ""{absolutePath}""" : $@"""{absolutePath}"" ""%1""";
                }
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

                    // Root item gets custom icon if specified, otherwise its default icon
                    string itemIcon;
                    if (!string.IsNullOrEmpty(customIconPath))
                    {
                        itemIcon = customIconPath;
                    }
                    else
                    {
                        if (isRaw)
                        {
                            string exePath = ExtractExecutableFromCommand(path);
                            itemIcon = File.Exists(exePath) ? exePath : (isFolder ? "shell32.dll,3" : "cmd.exe");
                        }
                        else
                        {
                            itemIcon = isFolder ? "explorer.exe" : absolutePath;
                        }
                    }
                    key.SetValue("Icon", itemIcon);
                    
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
                    // Grup simgesini sadece formda yeni bir simge seçildiyse güncelleriz, 
                    // aksi halde grubun mevcut simgesini bozmayıp koruruz (yeni grup ise varsayılan sarı klasör simgesi verilir).
                    if (!string.IsNullOrEmpty(customIconPath))
                    {
                        gkey.SetValue("Icon", customIconPath);
                    }
                    else if (gkey.GetValue("Icon") == null)
                    {
                        gkey.SetValue("Icon", "shell32.dll,3"); // varsayılan klasör simgesi
                    }

                    if (position == "Top" || position == "Bottom")
                    {
                        gkey.SetValue("Position", position);
                    }
                }

                string itemPath = $@"{groupPath}\shell\{itemKeyName}";
                using (var key = Registry.CurrentUser.CreateSubKey(itemPath))
                {
                    key.SetValue("", name);

                    // Inside a group, the item always gets its own default icon, NOT the group's custom icon path!
                    string itemIcon;
                    if (isRaw)
                    {
                        string exePath = ExtractExecutableFromCommand(path);
                        itemIcon = File.Exists(exePath) ? exePath : (isFolder ? "shell32.dll,3" : "cmd.exe");
                    }
                    else
                    {
                        itemIcon = isFolder ? "explorer.exe" : absolutePath;
                    }
                    key.SetValue("Icon", itemIcon);

                    using (var cmdkey = key.CreateSubKey("command"))
                    {
                        cmdkey.SetValue("", cmdVal);
                    }
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const int SHCNF_FLUSH = 0x1000;

        public static void RefreshExplorer()
        {
            try
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }

        public static void LockGroup(string groupName)
        {
            var settings = LockSettings.Load();
            if (settings.ContainsKey(groupName))
            {
                LockGroupInternal(groupName, settings[groupName]);
            }
        }

        public static void LockGroup(string groupName, string password, int durationSeconds)
        {
            var allShortcuts = LoadShortcuts();
            var groupItems = allShortcuts.FindAll(s => string.Equals(s.Group, groupName, StringComparison.OrdinalIgnoreCase));
            if (groupItems.Count == 0)
            {
                throw new Exception($"'{groupName}' grubuna ait kısayol bulunamadı. Boş bir grup kilitlenemez.");
            }

            var lockedItems = new List<LockedItem>();
            var targetsUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            string groupIcon = string.Empty;
            string groupPosition = string.Empty;
            string cleanGroupName = Regex.Replace(groupName, @"\W+", "");
            string groupKeyName = $"CustomGroup_{cleanGroupName}";

            foreach (var item in groupItems)
            {
                string rawCommand = string.Empty;
                string rootRegPath = GetRegistryPath(item.TargetType);
                
                string[] parts = item.Id.Split('|');
                if (parts.Length == 2)
                {
                    string subkeyPath = $@"{rootRegPath}\{parts[1]}\command";
                    using (var cmdKey = Registry.CurrentUser.OpenSubKey(subkeyPath))
                    {
                        if (cmdKey != null)
                        {
                            rawCommand = cmdKey.GetValue("")?.ToString() ?? string.Empty;
                        }
                    }
                    
                    if (string.IsNullOrEmpty(groupIcon) || string.IsNullOrEmpty(groupPosition))
                    {
                        string groupPath = $@"{rootRegPath}\{groupKeyName}";
                        using (var gkey = Registry.CurrentUser.OpenSubKey(groupPath))
                        {
                            if (gkey != null)
                            {
                                groupIcon = gkey.GetValue("Icon")?.ToString() ?? groupIcon;
                                groupPosition = gkey.GetValue("Position")?.ToString() ?? groupPosition;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(rawCommand))
                {
                    rawCommand = item.Path;
                }

                lockedItems.Add(new LockedItem
                {
                    Name = item.Name,
                    Path = rawCommand,
                    IsFolder = item.IsFolder,
                    TargetType = item.TargetType,
                    Position = item.Position,
                    IconPath = item.IconPath
                });

                targetsUsed.Add(item.TargetType);
            }

            var config = new LockedGroup
            {
                PasswordHash = LockSettings.ComputeSha256(password),
                UnlockDurationSeconds = durationSeconds,
                GroupIconPath = groupIcon,
                GroupPosition = groupPosition,
                Items = lockedItems
            };

            LockGroupInternal(groupName, config);
        }

        private static void LockGroupInternal(string groupName, LockedGroup config)
        {
            string cleanGroupName = Regex.Replace(groupName, @"\W+", "");
            string groupKeyName = $"CustomGroup_{cleanGroupName}";
            string lockedKeyName = $"CustomFolder_LockedGroup_{cleanGroupName}";

            string exePath = Environment.ProcessPath ?? "ContextMenuManager.exe";

            // Save settings
            var settings = LockSettings.Load();
            settings[groupName] = config;
            LockSettings.Save(settings);

            var targetsUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in config.Items)
            {
                targetsUsed.Add(item.TargetType);
            }

            foreach (var targetType in targetsUsed)
            {
                string rootRegPath = GetRegistryPath(targetType);

                // Delete custom group
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree($@"{rootRegPath}\{groupKeyName}", false);
                }
                catch { }

                // Create kilitli tekli buton (CustomFolder)
                string lockedPath = $@"{rootRegPath}\{lockedKeyName}";
                using (var key = Registry.CurrentUser.CreateSubKey(lockedPath))
                {
                    key.SetValue("", $"{groupName} (Kilitli)");
                    key.SetValue("Icon", "shell32.dll,47"); // Lock icon

                    if (!string.IsNullOrEmpty(config.GroupPosition))
                    {
                        key.SetValue("Position", config.GroupPosition);
                    }

                    using (var cmdKey = key.CreateSubKey("command"))
                    {
                        cmdKey.SetValue("", $@"""{exePath}"" --unlock ""{groupName}""");
                    }
                }
            }

            RefreshExplorer();
        }

        public static void UnlockGroup(string groupName)
        {
            var settings = LockSettings.Load();
            if (!settings.ContainsKey(groupName))
            {
                throw new Exception($"'{groupName}' grubuna ait kilit ayarları bulunamadı.");
            }

            var groupConfig = settings[groupName];
            string cleanGroupName = Regex.Replace(groupName, @"\W+", "");
            string groupKeyName = $"CustomGroup_{cleanGroupName}";
            string lockedKeyName = $"CustomFolder_LockedGroup_{cleanGroupName}";

            var targetsUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in groupConfig.Items)
            {
                string rootRegPath = GetRegistryPath(item.TargetType);
                targetsUsed.Add(item.TargetType);

                // Create custom group key
                string groupPath = $@"{rootRegPath}\{groupKeyName}";
                using (var gkey = Registry.CurrentUser.CreateSubKey(groupPath))
                {
                    gkey.SetValue("MUIVerb", groupName);
                    gkey.SetValue("SubCommands", "");
                    
                    if (!string.IsNullOrEmpty(groupConfig.GroupIconPath))
                    {
                        gkey.SetValue("Icon", groupConfig.GroupIconPath);
                    }
                    else
                    {
                        gkey.SetValue("Icon", "shell32.dll,3"); // default folder
                    }

                    if (!string.IsNullOrEmpty(groupConfig.GroupPosition))
                    {
                        gkey.SetValue("Position", groupConfig.GroupPosition);
                    }
                }

                // Create item subkey
                string cleanItemName = Regex.Replace(item.Name, @"\W+", "");
                long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string itemKeyName = $"CustomItem_{cleanItemName}_{unixTimestamp}";

                string itemPath = $@"{groupPath}\shell\{itemKeyName}";
                using (var key = Registry.CurrentUser.CreateSubKey(itemPath))
                {
                    key.SetValue("", item.Name);
                    
                    if (!string.IsNullOrEmpty(item.IconPath))
                    {
                        key.SetValue("Icon", item.IconPath);
                    }
                    else
                    {
                        string exePath = ExtractExecutableFromCommand(item.Path);
                        key.SetValue("Icon", File.Exists(exePath) ? exePath : (item.IsFolder ? "explorer.exe" : "cmd.exe"));
                    }

                    using (var cmdKey = key.CreateSubKey("command"))
                    {
                        cmdKey.SetValue("", item.Path);
                    }
                }
            }

            // Remove kilitli tekli buton (CustomFolder_LockedGroup_*)
            foreach (var targetType in targetsUsed)
            {
                string rootRegPath = GetRegistryPath(targetType);
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree($@"{rootRegPath}\{lockedKeyName}", false);
                }
                catch { }
            }

            RefreshExplorer();
        }

        public static void StartLockTimer(string groupName, int durationSeconds)
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(durationSeconds * 1000);
                try
                {
                    LockGroup(groupName);
                }
                catch { }
            });
        }

        public static void CheckAndLockAllGroups()
        {
            try
            {
                var settings = LockSettings.Load();
                var allShortcuts = LoadShortcuts();

                foreach (var groupName in settings.Keys)
                {
                    bool isUnlocked = allShortcuts.Exists(s => string.Equals(s.Group, groupName, StringComparison.OrdinalIgnoreCase));
                    if (isUnlocked)
                    {
                        LockGroupInternal(groupName, settings[groupName]);
                    }
                }

                // Clean up orphan locked keys (keys that exist in registry but not in JSON)
                string[] paths = { REG_PATH_BG, REG_PATH_DIR, REG_PATH_FILE };
                foreach (var regPath in paths)
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(regPath, true))
                    {
                        if (key != null)
                        {
                            foreach (var subkeyName in key.GetSubKeyNames())
                            {
                                if (subkeyName.StartsWith("CustomFolder_LockedGroup_"))
                                {
                                    string cleanGroupName = subkeyName.Replace("CustomFolder_LockedGroup_", "");
                                    
                                    bool existsInJson = false;
                                    foreach (var groupName in settings.Keys)
                                    {
                                        string clean = Regex.Replace(groupName, @"\W+", "");
                                        if (string.Equals(clean, cleanGroupName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            existsInJson = true;
                                            break;
                                        }
                                    }

                                    if (!existsInJson)
                                    {
                                        try
                                        {
                                            key.DeleteSubKeyTree(subkeyName, false);
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
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

        private static bool IsRawCommand(string path, out string absolutePath)
        {
            absolutePath = path;
            if (string.IsNullOrEmpty(path)) return false;

            if (path.StartsWith("\"") || path.Contains(" /") || path.Contains(" -"))
            {
                return true;
            }

            try
            {
                absolutePath = Path.GetFullPath(path);
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static string ExtractExecutableFromCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return string.Empty;
            command = command.Trim();
            if (command.StartsWith("\""))
            {
                int endQuoteIndex = command.IndexOf("\"", 1);
                if (endQuoteIndex > 0)
                {
                    return command.Substring(1, endQuoteIndex - 1);
                }
            }
            else
            {
                int spaceIndex = command.IndexOf(" ");
                if (spaceIndex > 0)
                {
                    return command.Substring(0, spaceIndex);
                }
            }
            return command;
        }

        public static List<ShellExtensionItem> LoadShellExtensions()
        {
            var list = new List<ShellExtensionItem>();
            var uniqueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Load ContextMenuHandlers (Shell Extensions)
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

                                    string uniqueKey = $"Ext|{clsid}|{path}";
                                    if (!uniqueKeys.Contains(uniqueKey))
                                    {
                                        uniqueKeys.Add(uniqueKey);
                                        list.Add(new ShellExtensionItem
                                        {
                                            KeyName = subkeyName,
                                            Clsid = clsid,
                                            RegistryPath = $@"HKCR\{path}\{subkeyName}",
                                            TargetDisplay = targetDisplay,
                                            IsBlocked = blockedClsids.Contains(clsid),
                                            IsStatic = false
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // 2. Load Static Commands (shell keys)
            string[] shellPaths = {
                @"*\shell",
                @"Directory\shell",
                @"Directory\Background\shell"
            };

            foreach (var path in shellPaths)
            {
                string targetDisplay = path switch
                {
                    @"*\shell" => "Tüm Dosyalar",
                    @"Directory\shell" => "Klasör",
                    @"Directory\Background\shell" => "Boş Alan",
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
                                // Skip our own custom shortcuts and system commands
                                if (subkeyName.StartsWith("CustomFolder_") || 
                                    subkeyName.StartsWith("CustomGroup_") || 
                                    subkeyName.Equals("PowershellAlways", StringComparison.OrdinalIgnoreCase) ||
                                    subkeyName.Equals("cmd", StringComparison.OrdinalIgnoreCase) ||
                                    subkeyName.Equals("Powershell", StringComparison.OrdinalIgnoreCase) ||
                                    subkeyName.Equals("find", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                string displayName = subkeyName;
                                using (var subkey = key.OpenSubKey(subkeyName))
                                {
                                    if (subkey != null)
                                    {
                                        string rawName = subkey.GetValue("MUIVerb")?.ToString() ?? subkey.GetValue("")?.ToString() ?? subkeyName;
                                        if (!string.IsNullOrEmpty(rawName) && !rawName.StartsWith("@"))
                                        {
                                            displayName = rawName.Replace("&", "");
                                        }
                                    }
                                }

                                // Check if blocked under HKCU
                                bool isBlocked = false;
                                string hkcuPath = $@"Software\Classes\{path}\{subkeyName}";
                                try
                                {
                                    using (var hkcuKey = Registry.CurrentUser.OpenSubKey(hkcuPath))
                                    {
                                        if (hkcuKey != null)
                                        {
                                            isBlocked = hkcuKey.GetValue("LegacyDisable") != null;
                                        }
                                    }
                                }
                                catch { }

                                string uniqueKey = $"Static|{subkeyName}|{path}";
                                if (!uniqueKeys.Contains(uniqueKey))
                                {
                                    uniqueKeys.Add(uniqueKey);
                                    list.Add(new ShellExtensionItem
                                    {
                                        KeyName = displayName,
                                        Clsid = subkeyName,
                                        RegistryPath = $@"HKCR\{path}\{subkeyName}",
                                        TargetDisplay = targetDisplay,
                                        IsBlocked = isBlocked,
                                        IsStatic = true
                                    });
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

        public static void ToggleShellExtension(string idOrClsid, bool block, bool isStatic, string registryPath)
        {
            if (isStatic)
            {
                string prefix = @"HKCR\";
                if (registryPath.StartsWith(prefix))
                {
                    string relativePath = registryPath.Substring(prefix.Length);
                    string hkcuPath = $@"Software\Classes\{relativePath}";

                    if (block)
                    {
                        using (var key = Registry.CurrentUser.CreateSubKey(hkcuPath))
                        {
                            key.SetValue("LegacyDisable", "");
                        }
                    }
                    else
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey(hkcuPath, true))
                        {
                            if (key != null)
                            {
                                try { key.DeleteValue("LegacyDisable"); } catch { }

                                // Clean up if key is completely empty
                                if (key.SubKeyCount == 0 && key.ValueCount == 0)
                                {
                                    try
                                    {
                                        Registry.CurrentUser.DeleteSubKey(hkcuPath, false);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                string blockedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";
                if (block)
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(blockedKeyPath))
                    {
                        key.SetValue(idOrClsid, "");
                    }
                }
                else
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(blockedKeyPath, true))
                    {
                        if (key != null)
                        {
                            try { key.DeleteValue(idOrClsid); } catch { }
                        }
                    }
                }
            }
        }

        public static List<CustomGroupItem> LoadCustomGroups()
        {
            var groups = new List<CustomGroupItem>();

            groups.AddRange(LoadCustomGroupsFromKey(REG_PATH_BG, "Background", "Boş Alan"));
            groups.AddRange(LoadCustomGroupsFromKey(REG_PATH_DIR, "Directory", "Klasör"));
            groups.AddRange(LoadCustomGroupsFromKey(REG_PATH_FILE, "AllFiles", "Tüm Dosyalar"));

            try
            {
                using (var baseKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SystemFileAssociations"))
                {
                    if (baseKey != null)
                    {
                        foreach (var ext in baseKey.GetSubKeyNames())
                        {
                            string shellPath = $@"Software\Classes\SystemFileAssociations\{ext}\shell";
                            groups.AddRange(LoadCustomGroupsFromKey(shellPath, $"FileExtension:{ext}", $"Uzantı ({ext})"));
                        }
                    }
                }
            }
            catch { }

            return groups;
        }

        private static List<CustomGroupItem> LoadCustomGroupsFromKey(string regPath, string targetType, string targetDisplay)
        {
            var list = new List<CustomGroupItem>();
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(regPath))
                {
                    if (key != null)
                    {
                        foreach (var subkeyName in key.GetSubKeyNames())
                        {
                            if (subkeyName.StartsWith("CustomGroup_"))
                            {
                                using (var subkey = key.OpenSubKey(subkeyName))
                                {
                                    if (subkey != null)
                                    {
                                        string name = subkey.GetValue("MUIVerb")?.ToString() ?? subkeyName.Replace("CustomGroup_", "");
                                        string iconPath = subkey.GetValue("Icon")?.ToString() ?? string.Empty;
                                        string position = subkey.GetValue("Position")?.ToString() ?? "Default";

                                        list.Add(new CustomGroupItem
                                        {
                                            Id = $"{targetType}|{subkeyName}",
                                            Name = name,
                                            TargetType = targetType,
                                            TargetDisplay = targetDisplay,
                                            IconPath = iconPath,
                                            Position = position
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        public static void AddCustomGroup(string name, string targetType, string iconPath, string position)
        {
            string rootPath = GetRegistryPath(targetType);
            string cleanGroupName = Regex.Replace(name, @"\W+", "");
            string groupKeyName = $"CustomGroup_{cleanGroupName}";
            string groupPath = $@"{rootPath}\{groupKeyName}";

            using (var gkey = Registry.CurrentUser.CreateSubKey(groupPath))
            {
                gkey.SetValue("MUIVerb", name);
                gkey.SetValue("SubCommands", "");

                if (!string.IsNullOrEmpty(iconPath))
                {
                    gkey.SetValue("Icon", iconPath);
                }
                else
                {
                    gkey.SetValue("Icon", "shell32.dll,3"); // default yellow folder icon
                }

                if (position == "Top" || position == "Bottom")
                {
                    gkey.SetValue("Position", position);
                }
                else
                {
                    try { gkey.DeleteValue("Position"); } catch { }
                }
            }
        }

        public static void DeleteCustomGroup(string compoundId)
        {
            var index = compoundId.IndexOf('|');
            if (index == -1) return;

            string targetType = compoundId.Substring(0, index);
            string keyId = compoundId.Substring(index + 1);
            string rootPath = GetRegistryPath(targetType);

            string fullPath = $@"{rootPath}\{keyId}";
            Registry.CurrentUser.DeleteSubKeyTree(fullPath, false);
        }

        public static bool LoadThemeSetting()
        {
            string settingsPath = @"Software\ContextMenuManager\Settings";
            using (var key = Registry.CurrentUser.OpenSubKey(settingsPath))
            {
                if (key != null)
                {
                    var val = key.GetValue("DarkMode");
                    if (val != null && int.TryParse(val.ToString(), out int result))
                    {
                        return result == 1;
                    }
                }
            }
            return false; // light mode is default
        }

        public static void SaveThemeSetting(bool isDark)
        {
            string settingsPath = @"Software\ContextMenuManager\Settings";
            using (var key = Registry.CurrentUser.CreateSubKey(settingsPath))
            {
                key.SetValue("DarkMode", isDark ? 1 : 0, RegistryValueKind.DWord);
            }
        }
    }
}
