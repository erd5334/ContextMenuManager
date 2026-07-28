using System;
using System.Windows;
using System.Windows.Input;

namespace ContextMenuManager
{
    public partial class PasswordWindow : Window
    {
        private readonly string _groupName;

        public PasswordWindow(string groupName)
        {
            InitializeComponent();
            _groupName = groupName;
            
            TitleLabel.Text = $"'{_groupName}' Kilidini Aç";
            DescLabel.Text = $"Lütfen bu menünün kilidini açmak için şifrenizi girin. Doğrulandıktan sonra menü aktif hale gelecektir.";
            
            PasswordTxt.Focus();
        }

        private void UnlockBtn_Click(object sender, RoutedEventArgs e)
        {
            string password = PasswordTxt.Password;
            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Lütfen şifrenizi girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var settings = LockSettings.Load();
                if (!settings.ContainsKey(_groupName))
                {
                    MessageBox.Show($"'{_groupName}' isimli şifreli bir grup bulunamadı. Lütfen Sağ Tık Yöneticisi uygulamasından bu grubu yapılandırın.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                var groupConfig = settings[_groupName];
                string inputHash = LockSettings.ComputeSha256(password);

                if (string.Equals(groupConfig.PasswordHash, inputHash, StringComparison.OrdinalIgnoreCase))
                {
                    // Şifre doğru, kilidi aç
                    RegistryService.UnlockGroup(_groupName);
                    
                    // Zamanlayıcıyı başlat (Arka planda süre sonunda otomatik kilitleyecek)
                    RegistryService.StartLockTimer(_groupName, groupConfig.UnlockDurationSeconds);

                    MessageBox.Show(
                        $"'{_groupName}' menüsünün kilidi başarıyla açıldı.\n\n" +
                        $"Menü {groupConfig.UnlockDurationSeconds} saniye boyunca aktif kalacak ve ardından otomatik olarak yeniden kilitlenecektir.", 
                        "Kilit Açıldı", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information
                    );

                    Close();
                }
                else
                {
                    MessageBox.Show("Girdiğiniz şifre hatalı. Lütfen tekrar deneyin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    PasswordTxt.Clear();
                    PasswordTxt.Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kilidi açarken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void PasswordTxt_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                UnlockBtn_Click(this, new RoutedEventArgs());
            }
        }
    }
}
