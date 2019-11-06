using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace wizut_steganografia
{
    /// <summary>
    /// Logika interakcji dla klasy MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _imageLoaded, _imageEncrypted;
        private byte[] _encKey, _encIV;

        public MainWindow()
        {
            InitializeComponent();
            _imageLoaded = false;
            _imageEncrypted = false;
            _encKey = Encoding.UTF8.GetBytes("mYk3Y");
            _encIV = Encoding.UTF8.GetBytes("aE23Nc");
        }

        private void PutMsgBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_imageLoaded)
            {
                Bitmap ImgToMod = new Bitmap(LoadedImgPath.Content.ToString());
                string MsgToPut = "test";
                byte[] EncodedMsg = EncodeMsg(MsgToPut);
                ImgToMod = PutMsg(ImgToMod, EncodedMsg);
                SaveImg.Source = Bitmap2BitmapSource(ImgToMod);
                _imageEncrypted = true;
                ImgToMod.Dispose();
            }
        }

        private byte[] EncodeMsg(string MsgToPut)
        {
            if (EncKey.Text.Length > 0) _encKey = Encoding.UTF8.GetBytes(EncKey.Text);
            if (EncIV.Text.Length > 0) _encIV = Encoding.UTF8.GetBytes(EncIV.Text);

            byte[] encrypted;
            using (Aes aesAlg = Aes.Create())
            {
                using (SHA512 shaM = new SHA512Managed())
                {
                    _encKey = shaM.ComputeHash(_encKey);
                }
                aesAlg.Key = _encKey.Take(32).ToArray();
                aesAlg.IV = _encKey.Skip(32).ToArray().Take(16).ToArray();
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(MsgToPut);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }

            return encrypted;
        }

        //private byte[] RefillWithZeros(int value, int bitsCount)
        //{
        //    byte[] ret = BitConverter.GetBytes(value);
        //    if (ret.Length * 8 < bitsCount)
        //    {
        //        ret = new byte[ret.Length+()]
        //    }
        //}

        private Bitmap PutMsg(Bitmap ImgToMod, byte[] MsgToPut)
        {
            byte[] tempLen = BitConverter.GetBytes(MsgToPut.Length);
            byte[] msgLen = new byte[2];
            msgLen[0] = tempLen[0];
            msgLen[1] = tempLen[1];
            byte[] NewMsg = new byte[msgLen.Length + MsgToPut.Length];
            Buffer.BlockCopy(msgLen, 0, NewMsg, 0, msgLen.Length);
            Buffer.BlockCopy(MsgToPut, 0, NewMsg, msgLen.Length, MsgToPut.Length);

            for (int i=0; i<ImgToMod.Width; ++i)
            {
                for (int j=0; j<ImgToMod.Height; ++j)
                {
                    Color CurPixel = ImgToMod.GetPixel(i, j);
                    int R = CurPixel.R - CurPixel.R % 23,
                        G = CurPixel.G - CurPixel.G % 23,
                        B = CurPixel.B - CurPixel.B % 23;
                    // ukryć wiadomość
                    ImgToMod.SetPixel(i, j, Color.FromArgb(R, G, B));
                }
            }
            return ImgToMod;
        }

        private void LoadImgBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog();
            op.Title = "Select a picture";
            op.Filter = "Bitmap graphics|*.bmp";
            if (op.ShowDialog() == true)
            {
                LoadedImg.Source = new BitmapImage(new Uri(op.FileName));
                LoadedImgPath.Content = op.FileName;
                LoadedImgPath.ToolTip = op.FileName;
                _imageLoaded = true;
            }
        }

        public BitmapSource Bitmap2BitmapSource(Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                System.Windows.Media.PixelFormats.Bgr24, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }
    }
}
