using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace JPEG
{
    /// <summary>
    /// Logika interakcji dla klasy MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadImgBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Select a picture",
                Filter = "Bitmap graphics|*.bmp"
            };

            if (ofd.ShowDialog() == true)
            {
                LoadedImg.Source = new BitmapImage(new Uri(ofd.FileName));
                LoadedImgPath.Content = ofd.FileName;
                LoadedImgPath.ToolTip = ofd.FileName;
                ModPixelCounter.Content = "0";
                UpdateStatus("Image loaded.");
            }
            else UpdateStatus("Image not loaded.", true);
        }

        private void ImgPath_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", Directory.GetParent(((System.Windows.Controls.Label)sender).Content.ToString()).FullName);
                UpdateStatus("Dir opened.");
            }
            catch
            {
                UpdateStatus("Cannot open the dir.", true);
            }
        }

        private void UpdateStatus(string status, bool isError = false)
        {
            DateTime localDate = DateTime.Now;
            StatusLabel.Foreground = isError ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Green;
            StatusLabel.Content = localDate + " - " + status;
            StatusLabel.ToolTip = status;
        }

        private System.Drawing.Bitmap BitmapSource2Bitmap(BitmapSource bitmapsource)
        {
            System.Drawing.Bitmap bitmap;
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                enc.Save(outStream);
                bitmap = new System.Drawing.Bitmap(outStream);
            }
            return bitmap;
        }

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);
        public ImageSource ImageSourceFromBitmap(System.Drawing.Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }

        private void MarkChangesBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadedImg.Source = MarkChanges(LoadedImgPath.Content.ToString());
                UpdateStatus("Changes marked.");
            }
            catch (Exception err)
            {
                UpdateStatus(err.Message, true);
            }
        }

        private ImageSource MarkChanges(string path)
        {
            JPEGAnalyzer detector = new JPEGAnalyzer(path);
            List<System.Drawing.Point> pixels = detector.GetModifiedPixelsList();
            if (pixels == null) throw new Exception("No pixels were changed.");
            ModPixelCounter.Content = detector.GetNumOfModifiedPixels();
            return ImageSourceFromBitmap(detector.MarkPixels());
        }
    }
}
