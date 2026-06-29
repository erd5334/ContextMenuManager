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
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "İkon Görseli veya Kaynağı Seçin",
                    Filter = "İkon Kaynakları (*.ico;*.exe;*.dll;*.png)|*.ico;*.exe;*.dll;*.png|Tüm Dosyalar (*.*)|*.*"
                };
                if (dialog.ShowDialog() == true)
                {
                    IconTxt.Text = dialog.FileName;
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

            if (isFolder && !Directory.Exists(path))
            {
                MessageBox.Show("Belirtilen klasör yolu geçerli değil veya bulunamadı.", "Geçersiz Yol", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!isFolder && !File.Exists(path))
            {
                MessageBox.Show("Belirtilen dosya yolu geçerli değil veya bulunamadı.", "Geçersiz Yol", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
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
    }
}