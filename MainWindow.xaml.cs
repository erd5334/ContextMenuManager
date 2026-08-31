using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;

namespace ContextMenuManager
{
    public partial class MainWindow : Window
    {
        private bool _isInitializing = true;
        private ShortcutItem? _editingItem = null;
        private CustomGroupItem? _editingGroup = null;

        public MainWindow()
        {
            InitializeComponent();
            _isInitializing = false;

            // Command-line arguments processing
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 2 && args[1] == "--navigate-dialog")
            {
                string targetFolder = args[2];
                bool navigated = RegistryService.NavigateActiveDialog(targetFolder);
                if (!navigated)
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"\"{targetFolder}\"");
                    }
                    catch { }
                }
                Application.Current.Shutdown();
                return;
            }

            if (args.Length > 2 && args[1] == "--unlock")
            {
                string groupToUnlock = args[2];
                
                this.Visibility = Visibility.Hidden;
                this.ShowInTaskbar = false;
                this.Width = 0;
                this.Height = 0;
                this.WindowStyle = WindowStyle.None;
                this.Opacity = 0;

                var pw = new PasswordWindow(groupToUnlock);
                pw.ShowDialog();

                var settings = LockSettings.Load();
                if (settings.ContainsKey(groupToUnlock))
                {
                    var config = settings[groupToUnlock];
                    var shortcuts = RegistryService.LoadShortcuts();
                    bool isUnlocked = shortcuts.Exists(s => string.Equals(s.Group, groupToUnlock, StringComparison.OrdinalIgnoreCase));
                    if (isUnlocked)
                    {
                        System.Threading.Tasks.Task.Run(async () =>
                        {
                            await System.Threading.Tasks.Task.Delay(config.UnlockDurationSeconds * 1000);
                            try
                            {
                                RegistryService.LockGroup(groupToUnlock);
                            }
                            catch { }
                            Dispatcher.Invoke(() => Application.Current.Shutdown());
                        });
                        return;
                    }
                }

                Application.Current.Shutdown();
                return;
            }

            // Normal Startup: Check and lock all groups (self-healing)
            RegistryService.CheckAndLockAllGroups();
            ToggleTheme(RegistryService.LoadThemeSetting());
            RegistryService.MigrateOldFolderShortcuts();
            RefreshAll();
        }

        private void RefreshAll()
        {
            try
            {
                _isInitializing = true;

                // Load shortcuts and bind to Grid
                var shortcuts = RegistryService.LoadShortcuts();
                ShortcutsGrid.ItemsSource = shortcuts;

                // Load groups
                var groups = RegistryService.GetExistingGroups();
                var lockableGroups = new List<string>(groups);
                lockableGroups.Remove("Ana Menü");
                LockGroupCombo.ItemsSource = lockableGroups;
                if (lockableGroups.Count > 0)
                {
                    LockGroupCombo.SelectedIndex = 0;
                }

                // Load custom groups and bind to CategoriesGrid
                var customGroups = RegistryService.LoadCustomGroups();
                CategoriesGrid.ItemsSource = customGroups;

                // Load locked groups
                var lockedGroups = LockSettings.Load();
                LockedGroupsList.ItemsSource = lockedGroups;

                // Load shell extensions and bind to Grid
                var shellExtensions = RegistryService.LoadShellExtensions();
                ShellExtensionsGrid.ItemsSource = shellExtensions;

                // Set initial status of checkboxes
                ClassicMenuChk.IsChecked = RegistryService.CheckClassicMenuStatus();
                PowerShellChk.IsChecked = RegistryService.CheckPowerShellStatus();
                CopyAsPathChk.IsChecked = RegistryService.CheckCopyAsPathStatus();

                // Dynamic GroupCombo update
                UpdateGroupComboForTarget();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veriler yüklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || PathLabel == null) return;

            if (TypeCombo.SelectedIndex == 0) // Klasör
            {
                PathLabel.Text = "Hedef Klasör Yolu";
            }
            else // Dosya / Program
            {
                PathLabel.Text = "Hedef Dosya/Program Yolu";
            }
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TypeCombo.SelectedIndex == 0) // Klasör
                {
                    var dialog = new Microsoft.Win32.OpenFolderDialog
                    {
                        Title = "Hedef Klasörü Seçin"
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        PathTxt.Text = dialog.FolderName;
                    }
                }
                else // Dosya
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Hedef Uygulamayı Seçin",
                        Filter = "Uygulamalar (*.exe)|*.exe|Tüm Dosyalar (*.*)|*.*"
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        PathTxt.Text = dialog.FileName;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dosya seçici açılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IconBrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new IconPickerDialog
                {
                    Owner = this
                };
                if (dialog.ShowDialog() == true)
                {
                    IconTxt.Text = dialog.SelectedIconResult;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İkon seçici açılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTxt.Text.Trim();
            string path = PathTxt.Text.Trim();
            string group = (GroupCombo.SelectedItem as string ?? "Ana Menü").Trim();
            bool isFolder = TypeCombo.SelectedIndex == 0;
            string customIconPath = IconTxt.Text.Trim();

            string targetType = TargetCombo.SelectedIndex switch
            {
                0 => "Background",
                1 => "Directory",
                2 => "AllFiles",
                3 => $"FileExtension:{NormalizeExtension(ExtensionTxt.Text.Trim())}",
                _ => "Background"
            };

            string position = PositionCombo.SelectedIndex switch
            {
                0 => "Default",
                1 => "Top",
                2 => "Bottom",
                _ => "Default"
            };

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Lütfen kısayol adını ve yolunu doldurun.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (isFolder)
            {
                if (!Directory.Exists(path))
                {
                    var result = MessageBox.Show(
                        "Belirtilen klasör yolu sistemde bulunamadı. Bunu yine de özel bir komut klasör kısayolu olarak kaydetmek istiyor musunuz?",
                        "Klasör Yolu Bulunamadı",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (result == MessageBoxResult.No) return;
                }
            }
            else
            {
                if (!File.Exists(path))
                {
                    var result = MessageBox.Show(
                        "Belirtilen dosya/program yolu sistemde bulunamadı. Bunu özel bir komut/argümanlı komut olarak kaydetmek istiyor musunuz?",
                        "Dosya Yolu Bulunamadı",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (result == MessageBoxResult.No) return;
                }
            }

            try
            {
                RegistryService.AddShortcut(name, path, group, isFolder, targetType, position, customIconPath);
                
                MessageBox.Show($"'{name}' kısayolu başarıyla sağ tık menüsüne eklendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                
                NameTxt.Text = string.Empty;
                PathTxt.Text = string.Empty;
                IconTxt.Text = string.Empty;
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kısayol eklenirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditSelected()
        {
            var selectedItem = ShortcutsGrid.SelectedItem as ShortcutItem;
            if (selectedItem == null) return;

            _editingItem = selectedItem;

            // Populate fields
            NameTxt.Text = selectedItem.Name;
            PathTxt.Text = selectedItem.Path;
            GroupCombo.SelectedItem = string.IsNullOrEmpty(selectedItem.Group) ? "Ana Menü" : selectedItem.Group;
            IconTxt.Text = selectedItem.IconPath;

            // Type
            TypeCombo.SelectedIndex = selectedItem.IsFolder ? 0 : 1;

            // Target type selection
            if (selectedItem.TargetType.StartsWith("FileExtension:"))
            {
                TargetCombo.SelectedIndex = 3;
                string ext = selectedItem.TargetType.Substring("FileExtension:".Length);
                ExtensionTxt.Text = ext;
                if (ExtensionPanel != null) ExtensionPanel.Visibility = Visibility.Visible;
            }
            else
            {
                TargetCombo.SelectedIndex = selectedItem.TargetType switch
                {
                    "Background" => 0,
                    "Directory" => 1,
                    "AllFiles" => 2,
                    _ => 0
                };
                if (ExtensionPanel != null) ExtensionPanel.Visibility = Visibility.Collapsed;
            }

            // Position selection
            PositionCombo.SelectedIndex = selectedItem.Position switch
            {
                "Default" => 0,
                "Top" => 1,
                "Bottom" => 2,
                _ => 0
            };

            // Change UI state to editing
            FormTitleLabel.Text = "Kısayolu Düzenle";
            AddBtn.Visibility = Visibility.Collapsed;
            EditBtnGrid.Visibility = Visibility.Visible;
        }

        private void CancelEdit()
        {
            _editingItem = null;
            NameTxt.Text = string.Empty;
            PathTxt.Text = string.Empty;
            IconTxt.Text = string.Empty;
            GroupCombo.SelectedItem = "Ana Menü";
            TypeCombo.SelectedIndex = 0;
            TargetCombo.SelectedIndex = 0;
            PositionCombo.SelectedIndex = 0;
            ExtensionTxt.Text = ".txt";
            if (ExtensionPanel != null) ExtensionPanel.Visibility = Visibility.Collapsed;

            FormTitleLabel.Text = "Yeni Kısayol Ekle";
            AddBtn.Visibility = Visibility.Visible;
            EditBtnGrid.Visibility = Visibility.Collapsed;
        }

        private void EditBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ShortcutsGrid.SelectedItem as ShortcutItem;
            if (selectedItem == null)
            {
                MessageBox.Show("Lütfen düzenlemek istediğiniz kısayolu listeden seçin.", "Seçim Yok", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            EditSelected();
        }

        private void ShortcutsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EditSelected();
        }

        private void CancelEditBtn_Click(object sender, RoutedEventArgs e)
        {
            CancelEdit();
        }

        private void SaveEditBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_editingItem == null) return;

            string name = NameTxt.Text.Trim();
            string path = PathTxt.Text.Trim();
            string group = (GroupCombo.SelectedItem as string ?? "Ana Menü").Trim();
            bool isFolder = TypeCombo.SelectedIndex == 0;
            string customIconPath = IconTxt.Text.Trim();

            string targetType = TargetCombo.SelectedIndex switch
            {
                0 => "Background",
                1 => "Directory",
                2 => "AllFiles",
                3 => $"FileExtension:{NormalizeExtension(ExtensionTxt.Text.Trim())}",
                _ => "Background"
            };

            string position = PositionCombo.SelectedIndex switch
            {
                0 => "Default",
                1 => "Top",
                2 => "Bottom",
                _ => "Default"
            };

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
            {
                MessageBox.Show("Lütfen kısayol adını ve yolunu doldurun.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (isFolder)
            {
                if (!Directory.Exists(path))
                {
                    var result = MessageBox.Show(
                        "Belirtilen klasör yolu sistemde bulunamadı. Bunu yine de özel bir komut klasör kısayolu olarak kaydetmek istiyor musunuz?",
                        "Klasör Yolu Bulunamadı",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (result == MessageBoxResult.No) return;
                }
            }
            else
            {
                if (!File.Exists(path))
                {
                    var result = MessageBox.Show(
                        "Belirtilen dosya/program yolu sistemde bulunamadı. Bunu özel bir komut/argümanlı komut olarak kaydetmek istiyor musunuz?",
                        "Dosya Yolu Bulunamadı",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (result == MessageBoxResult.No) return;
                }
            }

            try
            {
                // 1. Delete: Remove old key from registry
                RegistryService.DeleteShortcut(_editingItem.Id);

                // 2. Re-create: Insert updated data
                RegistryService.AddShortcut(name, path, group, isFolder, targetType, position, customIconPath);

                MessageBox.Show($"'{name}' kısayolu başarıyla güncellendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                CancelEdit();
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kısayol güncellenirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ShortcutsGrid.SelectedItem as ShortcutItem;
            if (selectedItem == null)
            {
                MessageBox.Show("Lütfen silmek istediğiniz kısayolu listeden seçin.", "Seçim Yok", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show($"'{selectedItem.Name}' kısayolunu sağ tık menüsünden kaldırmak istediğinize emin misiniz?", "Kısayolu Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                RegistryService.DeleteShortcut(selectedItem.Id);
                MessageBox.Show("Kısayol başarıyla kaldırıldı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kısayol silinirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            RefreshAll();
        }

        private void ClassicMenuChk_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            try
            {
                bool enable = ClassicMenuChk.IsChecked == true;
                RegistryService.ToggleClassicMenu(enable);

                var confirm = MessageBox.Show(
                    "Windows 11 Klasik Sağ Tık Menüsü ayarı güncellendi.\n\n" +
                    "Değişikliklerin etkili olması için Windows Gezgini'nin (explorer.exe) yeniden başlatılması gerekmektedir.\n\n" +
                    "Gezgin şimdi yeniden başlatılsın mı?",
                    "Gezgini Yeniden Başlat",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    RestartExplorer();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Klasik menü ayarlanırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                ClassicMenuChk.IsChecked = !ClassicMenuChk.IsChecked;
            }
        }

        private void PowerShellChk_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            try
            {
                bool enable = PowerShellChk.IsChecked == true;
                RegistryService.TogglePowerShell(enable);

                string msg = enable 
                    ? "PowerShell kısayolu sağ tık menüsüne sabitlendi." 
                    : "PowerShell kısayolu varsayılana döndürüldü (sadece Shift tuşu ile görünecek).";

                MessageBox.Show(msg, "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PowerShell ayarı değiştirilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                PowerShellChk.IsChecked = !PowerShellChk.IsChecked;
            }
        }

        private void CopyAsPathChk_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            try
            {
                bool enable = CopyAsPathChk.IsChecked == true;
                RegistryService.ToggleCopyAsPath(enable);

                string msg = enable 
                    ? "Yol Olarak Kopyala seçeneği her zaman görünecek şekilde sabitlendi." 
                    : "Yol Olarak Kopyala seçeneği varsayılana döndürüldü (sadece Shift tuşu ile görünecek).";

                MessageBox.Show(msg, "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yol Olarak Kopyala ayarı değiştirilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                CopyAsPathChk.IsChecked = !CopyAsPathChk.IsChecked;
            }
        }

        private void PresetExplorer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RegistryService.AddRawShortcut(
                    "Gezgini Yeniden Başlat", 
                    @"cmd.exe /c taskkill /f /im explorer.exe & start explorer.exe", 
                    "shell32.dll,238", 
                    "Background", 
                    "Default"
                );
                MessageBox.Show("'Gezgini Yeniden Başlat' eylemi sağ tık menüsü boş alanına başarıyla eklendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şablon eklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PresetAdminCmd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Add to background
                RegistryService.AddRawShortcut(
                    "Yönetici Komut İstemi", 
                    @"powershell.exe -Command ""Start-Process cmd -ArgumentList '/k cd /d %V' -Verb RunAs""", 
                    "cmd.exe", 
                    "Background", 
                    "Default"
                );

                // Add to directory click
                RegistryService.AddRawShortcut(
                    "Yönetici Komut İstemi", 
                    @"powershell.exe -Command ""Start-Process cmd -ArgumentList '/k cd /d %1' -Verb RunAs""", 
                    "cmd.exe", 
                    "Directory", 
                    "Default"
                );

                MessageBox.Show("'Yönetici Komut İstemi' eylemi sağ tık menüsüne (boş alan ve klasör) başarıyla eklendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şablon eklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PresetTempCleaner_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string cmdVal = @"powershell.exe -WindowStyle Hidden -Command ""Remove-Item -Path $env:TEMP\* -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item -Path C:\Windows\Temp\* -Recurse -Force -ErrorAction SilentlyContinue""";
                RegistryService.AddRawShortcut(
                    "Geçici Dosyaları Temizle", 
                    cmdVal, 
                    "shell32.dll,31", 
                    "Background", 
                    "Default"
                );
                MessageBox.Show("'Geçici Dosyaları Temizle' eylemi sağ tık menüsü boş alanına başarıyla eklendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şablon eklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestartExplorer()
        {
            try
            {
                foreach (var process in Process.GetProcessesByName("explorer"))
                {
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Windows Gezgini yeniden başlatılamadı, lütfen bilgisayarınızı yeniden başlatın veya oturumu kapatıp açın.\nDetay: {ex.Message}", "Bilgilendirme", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TargetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || ExtensionPanel == null) return;

            if (TargetCombo.SelectedIndex == 3) // Belirli Dosya Uzantısı
            {
                ExtensionPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ExtensionPanel.Visibility = Visibility.Collapsed;
            }

            UpdateGroupComboForTarget();
        }

        private void ToggleBlockedBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ShellExtensionsGrid.SelectedItem as ShellExtensionItem;
            if (selectedItem == null)
            {
                MessageBox.Show("Lütfen engellemek veya etkinleştirmek istediğiniz öğeyi listeden seçin.", "Seçim Yok", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                bool newBlockedState = !selectedItem.IsBlocked;
                RegistryService.ToggleShellExtension(selectedItem.Clsid, newBlockedState, selectedItem.IsStatic, selectedItem.RegistryPath);

                string actionText = newBlockedState ? "devre dışı bırakıldı" : "etkinleştirildi";
                
                var confirm = MessageBox.Show(
                    $"'{selectedItem.KeyName}' öğesi başarıyla {actionText}.\n\n" +
                    "Değişikliklerin etkili olması için Windows Gezgini'nin (explorer.exe) yeniden başlatılması gerekmektedir.\n\n" +
                    "Gezgin şimdi yeniden başlatılsın mı?",
                    "Gezgini Yeniden Başlat",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    RestartExplorer();
                }

                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İşlem sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshExtensionsBtn_Click(object sender, RoutedEventArgs e)
        {
            RefreshAll();
        }

        private string NormalizeExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return ".txt";
            if (!ext.StartsWith(".")) ext = "." + ext;
            return ext.ToLower();
        }

        private void LockGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            string? groupName = LockGroupCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(groupName))
            {
                MessageBox.Show("Lütfen kilitlemek istediğiniz grubu seçin.", "Seçim Yok", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string password = LockPasswordTxt.Password;
            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Lütfen grubu korumak için bir şifre girin.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string durationStr = LockDurationTxt.Text.Trim();
            if (!int.TryParse(durationStr, out int duration) || duration <= 0)
            {
                MessageBox.Show("Lütfen açık kalma süresi için geçerli bir saniye değeri girin (örn: 90).", "Geçersiz Süre", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                RegistryService.LockGroup(groupName, password, duration);
                MessageBox.Show($"'{groupName}' grubu başarıyla şifrelenerek kilitlendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                
                LockPasswordTxt.Clear();
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Grup kilitlenirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UnlockPermanentlyBtn_Click(object sender, RoutedEventArgs e)
        {
            var selected = LockedGroupsList.SelectedItem;
            if (selected == null)
            {
                MessageBox.Show("Lütfen kalıcı olarak kilidini kaldırmak istediğiniz grubu listeden seçin.", "Seçim Yok", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selected is System.Collections.Generic.KeyValuePair<string, LockedGroup> pair)
            {
                string groupName = pair.Key;

                var confirm = MessageBox.Show($"'{groupName}' grubunun şifre korumasını kalıcı olarak kaldırmak ve tüm kısayollarını görünür yapmak istediğinize emin misiniz?", "Kilidi Kaldır", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;

                try
                {
                    RegistryService.UnlockGroup(groupName);

                    var settings = LockSettings.Load();
                    if (settings.ContainsKey(groupName))
                    {
                        settings.Remove(groupName);
                        LockSettings.Save(settings);
                    }

                    MessageBox.Show($"'{groupName}' grubunun kilidi kalıcı olarak kaldırıldı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshAll();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Kilidi kaldırırken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExtensionTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateGroupComboForTarget();
        }

        private void UpdateGroupComboForTarget()
        {
            if (GroupCombo == null || TargetCombo == null) return;

            string targetType;
            if (TargetCombo.SelectedIndex == 3 && ExtensionTxt != null) // Belirli Dosya Uzantısı
            {
                targetType = $"FileExtension:{NormalizeExtension(ExtensionTxt.Text.Trim())}";
            }
            else
            {
                targetType = TargetCombo.SelectedIndex switch
                {
                    0 => "Background",
                    1 => "Directory",
                    2 => "AllFiles",
                    _ => "Background"
                };
            }

            var customGroups = RegistryService.LoadCustomGroups();
            var filteredGroups = new List<string> { "Ana Menü" };
            foreach (var g in customGroups)
            {
                if (string.Equals(g.TargetType, targetType, StringComparison.OrdinalIgnoreCase))
                {
                    filteredGroups.Add(g.Name);
                }
            }

            GroupCombo.ItemsSource = filteredGroups;
            if (filteredGroups.Count > 0)
            {
                GroupCombo.SelectedIndex = 0;
            }
        }

        private void GroupTargetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || GroupExtensionPanel == null) return;

            if (GroupTargetCombo.SelectedIndex == 3) // Belirli Dosya Uzantısı
            {
                GroupExtensionPanel.Visibility = Visibility.Visible;
            }
            else
            {
                GroupExtensionPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void GroupIconBrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new IconPickerDialog
                {
                    Owner = this
                };
                if (dialog.ShowDialog() == true)
                {
                    GroupIconTxt.Text = dialog.SelectedIconResult;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İkon seçici açılırken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            string name = GroupNameTxt.Text.Trim();
            string iconPath = GroupIconTxt.Text.Trim();
            
            string targetType = GroupTargetCombo.SelectedIndex switch
            {
                0 => "Background",
                1 => "Directory",
                2 => "AllFiles",
                3 => $"FileExtension:{NormalizeExtension(GroupExtensionTxt.Text.Trim())}",
                _ => "Background"
            };

            string position = GroupPositionCombo.SelectedIndex switch
            {
                0 => "Default",
                1 => "Top",
                2 => "Bottom",
                _ => "Default"
            };

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Lütfen kategori (alt menü) adını doldurun.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                RegistryService.AddCustomGroup(name, targetType, iconPath, position);
                MessageBox.Show($"'{name}' kategorisi başarıyla oluşturuldu.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                
                GroupNameTxt.Text = string.Empty;
                GroupIconTxt.Text = string.Empty;
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kategori oluşturulurken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditGroupSelected()
        {
            var selectedItem = CategoriesGrid.SelectedItem as CustomGroupItem;
            if (selectedItem == null) return;

            _editingGroup = selectedItem;

            GroupNameTxt.Text = selectedItem.Name;
            GroupIconTxt.Text = selectedItem.IconPath;

            // Target type selection
            if (selectedItem.TargetType.StartsWith("FileExtension:"))
            {
                GroupTargetCombo.SelectedIndex = 3;
                string ext = selectedItem.TargetType.Substring("FileExtension:".Length);
                GroupExtensionTxt.Text = ext;
                if (GroupExtensionPanel != null) GroupExtensionPanel.Visibility = Visibility.Visible;
            }
            else
            {
                GroupTargetCombo.SelectedIndex = selectedItem.TargetType switch
                {
                    "Background" => 0,
                    "Directory" => 1,
                    "AllFiles" => 2,
                    _ => 0
                };
                if (GroupExtensionPanel != null) GroupExtensionPanel.Visibility = Visibility.Collapsed;
            }

            // Position selection
            GroupPositionCombo.SelectedIndex = selectedItem.Position switch
            {
                "Default" => 0,
                "Top" => 1,
                "Bottom" => 2,
                _ => 0
            };

            GroupFormTitleLabel.Text = "Kategoriyi Düzenle";
            AddGroupBtn.Visibility = Visibility.Collapsed;
            EditGroupBtnGrid.Visibility = Visibility.Visible;
        }

        private void CancelEditGroup()
        {
            _editingGroup = null;
            GroupNameTxt.Text = string.Empty;
            GroupIconTxt.Text = string.Empty;
            GroupTargetCombo.SelectedIndex = 0;
            GroupPositionCombo.SelectedIndex = 0;
            GroupExtensionTxt.Text = ".txt";
            if (GroupExtensionPanel != null) GroupExtensionPanel.Visibility = Visibility.Collapsed;

            GroupFormTitleLabel.Text = "Yeni Kategori Oluştur";
            AddGroupBtn.Visibility = Visibility.Visible;
            EditGroupBtnGrid.Visibility = Visibility.Collapsed;
        }

        private void CategoriesGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EditGroupSelected();
        }

        private void EditGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesGrid.SelectedItem == null)
            {
                MessageBox.Show("Lütfen düzenlemek istediğiniz kategoriyi listeden seçin.", "Seçim Yok", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            EditGroupSelected();
        }

        private void CancelEditGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            CancelEditGroup();
        }

        private void SaveEditGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_editingGroup == null) return;

            string name = GroupNameTxt.Text.Trim();
            string iconPath = GroupIconTxt.Text.Trim();
            
            string targetType = GroupTargetCombo.SelectedIndex switch
            {
                0 => "Background",
                1 => "Directory",
                2 => "AllFiles",
                3 => $"FileExtension:{NormalizeExtension(GroupExtensionTxt.Text.Trim())}",
                _ => "Background"
            };

            string position = GroupPositionCombo.SelectedIndex switch
            {
                0 => "Default",
                1 => "Top",
                2 => "Bottom",
                _ => "Default"
            };

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Lütfen kategori (alt menü) adını doldurun.", "Eksik Bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                RegistryService.EditCustomGroup(_editingGroup.Id, name, targetType, iconPath, position);
                MessageBox.Show($"'{name}' kategorisi başarıyla güncellendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                CancelEditGroup();
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kategori güncellenirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteGroupBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = CategoriesGrid.SelectedItem as CustomGroupItem;
            if (selectedItem == null)
            {
                MessageBox.Show("Lütfen silmek istediğiniz kategoriyi listeden seçin.", "Seçim Yok", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"'{selectedItem.Name}' kategorisini silmek istediğinize emin misiniz?\n\n" +
                "UYARI: Bu kategoriyi sildiğinizde, içerisindeki tüm kısayollar da kalıcı olarak silinecektir!",
                "Kategoriyi Sil", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);
            
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                RegistryService.DeleteCustomGroup(selectedItem.Id);
                MessageBox.Show("Kategori başarıyla kaldırıldı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kategori silinirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshGroupsBtn_Click(object sender, RoutedEventArgs e)
        {
            RefreshAll();
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string path = files[0];
                    PathTxt.Text = path;

                    if (System.IO.Directory.Exists(path))
                    {
                        TypeCombo.SelectedIndex = 0; // Klasör
                        NameTxt.Text = System.IO.Path.GetFileName(path);
                        IconTxt.Text = "shell32.dll,3";
                    }
                    else if (System.IO.File.Exists(path))
                    {
                        TypeCombo.SelectedIndex = 1; // Dosya / Program
                        NameTxt.Text = System.IO.Path.GetFileNameWithoutExtension(path);
                        
                        string ext = System.IO.Path.GetExtension(path).ToLower();
                        if (ext == ".exe" || ext == ".ico")
                        {
                            IconTxt.Text = path;
                        }
                        else
                        {
                            IconTxt.Text = "shell32.dll,16";
                        }
                    }
                    
                    this.Activate();
                    NameTxt.Focus();
                }
            }
        }

        private void ThemeToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            bool nextDark = !_isDarkMode;
            ToggleTheme(nextDark);
            RegistryService.SaveThemeSetting(nextDark);
        }

        private bool _isDarkMode = false;

        private void ToggleTheme(bool dark)
        {
            _isDarkMode = dark;

            var windowBg = dark ? "#0F172A" : "#F8FAFC";
            var cardBg = dark ? "#1E293B" : "#FFFFFF";
            var borderBrush = dark ? "#334155" : "#E2E8F0";
            var primaryText = dark ? "#F8FAFC" : "#0F172A";
            var secondaryText = dark ? "#94A3B8" : "#64748B";
            var labelText = dark ? "#CBD5E1" : "#475569";
            var inputBg = dark ? "#0F172A" : "#FFFFFF";
            var inputText = dark ? "#F8FAFC" : "#0F172A";
            var btnPrimaryBg = dark ? "#475569" : "#334155";
            var btnSecondaryText = dark ? "#CBD5E1" : "#334155";
            var dgAlternating = dark ? "#1C2535" : "#F8FAFC";

            UpdateResourceBrush("WindowBgBrush", windowBg);
            UpdateResourceBrush("CardBgBrush", cardBg);
            UpdateResourceBrush("BorderBrush", borderBrush);
            UpdateResourceBrush("PrimaryTextBrush", primaryText);
            UpdateResourceBrush("SecondaryTextBrush", secondaryText);
            UpdateResourceBrush("LabelTextBrush", labelText);
            UpdateResourceBrush("InputBgBrush", inputBg);
            UpdateResourceBrush("InputTextBrush", inputText);
            UpdateResourceBrush("BtnPrimaryBgBrush", btnPrimaryBg);
            UpdateResourceBrush("BtnSecondaryTextBrush", btnSecondaryText);
            UpdateResourceBrush("DataGridAlternatingBgBrush", dgAlternating);

            if (ThemeToggleBtn != null)
            {
                ThemeToggleBtn.Content = dark ? "☀️ Açık Tema" : "🌙 Karanlık Tema";
            }
        }

        private void UpdateResourceBrush(string resourceKey, string hexColor)
        {
            var brush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor));
            brush.Freeze();
            this.Resources[resourceKey] = brush;
        }

        private void PresetToggleHidden_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string cmdVal = @"powershell.exe -WindowStyle Hidden -Command ""$p='HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'; $v=Get-ItemProperty -Path $p; $newVal=if($v.Hidden -eq 1){2}else{1}; Set-ItemProperty -Path $p -Name Hidden -Value $newVal; Set-ItemProperty -Path $p -Name ShowSuperHidden -Value $newVal; stop-process -name explorer -force""";
                RegistryService.AddRawShortcut(
                    "Gizli Dosyaları Göster / Gizle", 
                    cmdVal, 
                    "imageres.dll,85", 
                    "Background", 
                    "Default"
                );
                MessageBox.Show("'Gizli Dosyaları Göster / Gizle' eylemi sağ tık menüsü boş alanına başarıyla eklendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şablon eklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PresetToggleExtensions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string cmdVal = @"powershell.exe -WindowStyle Hidden -Command ""$p='HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'; $v=Get-ItemProperty -Path $p; $newVal=if($v.HideFileExt -eq 1){0}else{1}; Set-ItemProperty -Path $p -Name HideFileExt -Value $newVal; stop-process -name explorer -force""";
                RegistryService.AddRawShortcut(
                    "Dosya Uzantılarını Göster / Gizle", 
                    cmdVal, 
                    "shell32.dll,22", 
                    "Background", 
                    "Default"
                );
                MessageBox.Show("'Dosya Uzantılarını Göster / Gizle' eylemi sağ tık menüsü boş alanına başarıyla eklendi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şablon eklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PathTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PathTxt.Text)) return;
            PathTxt.Text = PathTxt.Text.Replace("\"", "");
        }
    }
}