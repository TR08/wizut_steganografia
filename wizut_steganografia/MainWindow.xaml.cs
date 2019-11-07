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
                _imageEncrypted = false;
                SaveImg.Source = new BitmapImage(new Uri("/images/no-img.png", UriKind.Relative));

                Bitmap ImgToMod = new Bitmap(LoadedImgPath.Content.ToString());
                byte[] EncodedMsg = EncodeMsg(CustomMsg.Text);
                ImgToMod = PutMsg(ImgToMod, EncodedMsg);
                if (ImgToMod == null)
                {
                    UpdateStatus("Message is too long for this bitmap file.");
                    return;
                }
                SaveImg.Source = Bitmap2BitmapSource(ImgToMod);
                _imageEncrypted = true;
                UpdateStatus("Message embedded into bitmap file.");
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

        private Bitmap PutMsg(Bitmap ImgToMod, byte[] MsgToPut)
        {
            byte[] tempLen = BitConverter.GetBytes(MsgToPut.Length);
            byte[] msgLen = new byte[2];
            msgLen[0] = tempLen[1];
            msgLen[1] = tempLen[0];
            byte[] NewMsg = new byte[msgLen.Length + MsgToPut.Length];
            Buffer.BlockCopy(msgLen, 0, NewMsg, 0, msgLen.Length);
            Buffer.BlockCopy(MsgToPut, 0, NewMsg, msgLen.Length, MsgToPut.Length);

            int x = 0, y = 0,
                i;
            for (i=0; i < NewMsg.Length; ++i)
            {
                for (int j = 0; j < 4; ++j)
                {
                    Color CurPixel = ImgToMod.GetPixel(x, y);
                    byte R = CurPixel.R,
                        G = CurPixel.G,
                        B = CurPixel.B,
                        a1 = GetBit(CurPixel.R, 0),
                        a2 = GetBit(CurPixel.G, 0),
                        a3 = GetBit(CurPixel.B, 0);

                    byte M1 = GetBit(NewMsg[i], i*2),    //x1
                        M2 = GetBit(NewMsg[i], i*2+1);  //x2

                    if (M1 == (a1 ^ a3))
                    {
                        if (M2 != (a2 ^ a3)) G ^= 1;
                    }
                    else
                    {

                        if (M2 == (a2 ^ a3)) R ^= 1;
                        else B ^= 1;
                    }

                    ImgToMod.SetPixel(x, y, Color.FromArgb(R, G, B));

                    ++x;
                    if (x >= ImgToMod.Width)
                    {
                        x = 0;
                        ++y;
                        if (y >= ImgToMod.Height)
                        {
                            return null;
                        }
                    }
                }


            }

            return ImgToMod;
        }

        private byte GetBit(byte fromByte, int bitNum)
        {
            return Convert.ToByte((fromByte >> bitNum) & 1);
        }

        private void LoadImgBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog
            {
                Title = "Select a picture",
                Filter = "Bitmap graphics|*.bmp"
            };

            if (op.ShowDialog() == true)
            {
                LoadedImg.Source = new BitmapImage(new Uri(op.FileName));
                LoadedImgPath.Content = op.FileName;
                LoadedImgPath.ToolTip = op.FileName;
                _imageLoaded = true;
                UpdateStatus("Image loaded.");
            }
            else UpdateStatus("Image not loaded.");
        }

        private void ReadMsgBtn_Click(object sender, RoutedEventArgs e)
        {
            Bitmap ImgToRead = new Bitmap(LoadedImgPath.Content.ToString());
            //byte[] EncodedMsg = EncodeMsg(CustomMsg.Text);
            byte[] msg = ReadMsg(ImgToRead);
            msg = DecodeMsg(msg);
            CustomMsg.Text = msg.ToString();
            UpdateStatus("Message read and decoded.");
        }

        private byte[] ReadMsg(Bitmap imgToRead)
        {
            byte[] lenInBytes = ReadTwoBytes(imgToRead.GetPixel(0, 0));
            int len = lenInBytes[0] * 256 + lenInBytes[1];
            for (int i = 1; i <= len; ++i)
            {

            }
            return imgToRead;
        }

        private byte[] ReadTwoBytes(Color pixel)
        {
            byte M1 = GetBit(pixel.R, 0),
                M2 = GetBit(pixel.G, 0);
            if (GetBit(pixel.B, 0) == 0)
            {
                return new byte[] { M1, M2 };
            }
            else //a3 == 1
            {
                return new byte[] { (M1^=1), (M2^=1) };
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

        private void UpdateStatus(string status)
        {
            DateTime localDate = DateTime.Now;
            StatusLabel.Content = localDate + " - " + status;
        }
    }
}
