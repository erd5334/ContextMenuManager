using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ContextMenuManager
{
    public partial class IconPickerDialog : Window
    {
        public string SelectedIconResult { get; private set; } = string.Empty;
        private string _currentFilePath = string.Empty;

        public IconPickerDialog()
        {
            InitializeComponent();
            SourceCombo.SelectedIndex = 0;
        }

        private void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourceCombo == null || IconsList == null || LoadingLabel == null) return;
            if (SourceCombo.SelectedIndex == -1) return;

            string fileName = SourceCombo.SelectedIndex switch
            {
                0 => "imageres.dll",
                1 => "shell32.dll",
                2 => "ddores.dll",
                _ => "imageres.dll"
            };

            string fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), fileName);
            LoadIconsFromFile(fullPath);
        }

        private async void LoadIconsFromFile(string filePath)
        {
            if (IconsList == null || LoadingLabel == null) return;
            if (!File.Exists(filePath)) return;

            _currentFilePath = filePath;
            IconsList.Items.Clear();
            LoadingLabel.Visibility = Visibility.Visible;

            try
            {
                await Task.Run(() =>
                {
                    // Scan up to 250 icons from resources
                    for (int i = 0; i < 250; i++)
                    {
                        var imageSource = IconHelper.ExtractIcon(filePath, i, 32);
                        if (imageSource == null)
                        {
                            // If we hit 5 consecutive nulls, stop scanning (reached end of resource file)
                            int nullCount = 0;
                            for (int j = 1; j <= 5; j++)
                            {
                                if (IconHelper.ExtractIcon(filePath, i + j, 32) == null) nullCount++;
                            }
                            if (nullCount == 5) break;
                            continue;
                        }

                        int index = i;
                        Dispatcher.Invoke(() =>
                        {
                            var image = new Image
                            {
                                Source = imageSource,
                                Width = 32,
                                Height = 32,
                                ToolTip = $"Index: {index}"
                            };

                            var item = new ListBoxItem
                            {
                                Content = image,
                                Tag = index
                            };
                            IconsList.Items.Add(item);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İkonlar yüklenirken bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingLabel.Visibility = Visibility.Collapsed;
            }
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "İkon Dosyası Seçin",
                Filter = "İkon İçeren Dosyalar (*.ico;*.exe;*.dll)|*.ico;*.exe;*.dll|Tüm Dosyalar (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                SourceCombo.SelectedIndex = -1; // Deselect presets
                LoadIconsFromFile(dialog.FileName);
            }
        }

        private void IconsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IconsList.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag is int index)
            {
                SelectedLabel.Text = $"Seçilen İkon: Index {index} ({Path.GetFileName(_currentFilePath)})";
            }
            else
            {
                SelectedLabel.Text = "Seçilen İkon: -";
            }
        }

        private void SelectBtn_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void IconsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        private void ConfirmSelection()
        {
            if (IconsList.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag is int index)
            {
                SelectedIconResult = $"{_currentFilePath},{index}";
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Lütfen listeden bir ikon seçin.", "Seçim Yapılmadı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
