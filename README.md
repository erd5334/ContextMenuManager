# Gelişmiş Sağ Tık Menüsü Yöneticisi (ContextMenuManager)

Windows işletim sistemlerinde sağ tık (bağlam) menüsünü kolayca özelleştirmek ve yönetmek için C# WPF ile geliştirilmiş, sade renklerde ve modern arayüze sahip hafif bir masaüstü uygulamasıdır.

## 🚀 Özellikler
*   **Kısayol Ekleme:** Sık kullandığınız klasörleri veya uygulamaları (`.exe`) sağ tık menüsüne ekleyin.
*   **Menü Gruplama (Submenu):** Kısayollarınızı doğrudan göstermek yerine kendi oluşturacağınız özel alt menü gruplarında toplayın.
*   **Windows 11 Klasik Menü Geçişi:** Windows 11'in yeni sağ tık menüsünü klasik (Windows 10 stili) menüye tek tıkla dönüştürün (Windows Explorer'ı otomatik olarak yeniden başlatır).
*   **PowerShell Sabitleme:** "PowerShell penceresini burada aç" seçeneğini Shift tuşuna basmaya gerek kalmadan sağ tık menüsünde her zaman görünür yapın.
*   **Yönetim Arayüzü:** Eklediğiniz kısayolları listeleyin ve dilediklerinizi kolayca silin.
*   **Admin Yetkisi Gerektirmez:** HKCU (HKEY_CURRENT_USER) kayıt defteri dikeyini kullandığı için yönetici olarak çalıştırma zorunluluğu yoktur.

## 🎨 Tasarım Detayları
*   **Sade ve Göz Yormayan Palet:** Slate/Zinc tonlarında antrasit ve gri renkler kullanılarak modern bir arayüz sunulmuştur. Neon renkler içermez.
*   **Kullanıcı Deneyimi:** Yuvarlatılmış köşeler, yumuşak geçişli buton hover efektleri ve grid çizgileri arındırılmış temiz bir liste görünümü.

## 🛠️ Nasıl Derlenir?
Projeyi derlemek için sisteminizde .NET 8.0 SDK veya daha yeni bir sürümün yüklü olması gerekir.

```powershell
# Proje dizininde bağımlı modda (Framework-Dependent) yayınlama (Önerilen - ~150 KB):
dotnet publish -c Release --no-self-contained

# Bağımsız tek exe (Self-contained) olarak yayınlama (~160 MB):
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 📄 Lisans
Bu proje MIT lisansı altında lisanslanmıştır.
