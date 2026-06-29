using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;

namespace ContextMenuManager
{
    public static class IconHelper
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int PrivateExtractIcons(
            string lpszFile,
            int nIconIndex,
            int cxIcon,
            int cyIcon,
            IntPtr[] phicon,
            int[] piconid,
            int nIcons,
            int flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static ImageSource? ExtractIcon(string filePath, int index, int size = 32)
        {
            IntPtr[] phicon = new IntPtr[1];
            int[] piconid = new int[1];

            try
            {
                int count = PrivateExtractIcons(filePath, index, size, size, phicon, piconid, 1, 0);
                if (count > 0 && phicon[0] != IntPtr.Zero)
                {
                    try
                    {
                        ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                            phicon[0],
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        
                        imageSource.Freeze();
                        return imageSource;
                    }
                    finally
                    {
                        DestroyIcon(phicon[0]);
                    }
                }
            }
            catch
            {
                // Ignore extraction failures
            }
            return null;
        }
    }
}
