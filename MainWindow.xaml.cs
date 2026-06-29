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

        public MainWindow()
        {
            InitializeComponent();
            _isInitializing = false;
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

                // Load groups and bind to ComboBox
                var groups = RegistryService.GetExistingGroups();
                GroupCombo.ItemsSource = groups;
                GroupCombo.Text = "Ana Menü";

                // Load shell extensions and bind to Grid
                var shellExtensions = RegistryService.LoadShellExtensions();
                ShellExtensionsGrid.ItemsSource = shellExtensions;

                // Set initial status of checkboxes
                ClassicMenuChk.IsChecked = RegistryService.CheckClassicMenuStatus();
                PowerShellChk.IsChecked = RegistryService.CheckPowerShellStatus();
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
            string group = GroupCombo.Text.Trim();
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

            bool isCommand = false;
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
                    isCommand = true;
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
                    isCommand = true;
                }
            }

            try
            {
                if (isCommand)
                {
                    string iconPath = customIconPath;
                    if (string.IsNullOrEmpty(iconPath))
                    {
                        iconPath = isFolder ? "shell32.dll,3" : "cmd.exe";
                    }
                    RegistryService.AddRawShortcut(name, path, iconPath, targetType, position);
                }
                else
                {
                    RegistryService.AddShortcut(name, path, group, isFolder, targetType, position, customIconPath);
                }
                
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
            GroupCombo.Text = string.IsNullOrEmpty(selectedItem.Group) ? "Ana Menü" : selectedItem.Group;
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
            GroupCombo.Text = "Ana Menü";
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
            string group = GroupCombo.Text.Trim();
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

            bool isCommand = false;
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
                    isCommand = true;
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
                    isCommand = true;
                }
            }

            try
            {
                // 1. Delete: Remove old key from registry
                RegistryService.DeleteShortcut(_editingItem.Id);

                // 2. Re-create: Insert updated data
                if (isCommand)
                {
                    string iconPath = customIconPath;
                    if (string.IsNullOrEmpty(iconPath))
                    {
                        iconPath = isFolder ? "shell32.dll,3" : "cmd.exe";
                    }
                    RegistryService.AddRawShortcut(name, path, iconPath, targetType, position);
                }
                else
                {
                    RegistryService.AddShortcut(name, path, group, isFolder, targetType, position, customIconPath);
                }

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
                RegistryService.ToggleShellExtension(selectedItem.Clsid, newBlockedState);

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
    }
}