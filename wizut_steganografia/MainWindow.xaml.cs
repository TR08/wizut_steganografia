using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
        private byte[] _encKey;

        public MainWindow()
        {
            InitializeComponent();
            _imageLoaded = false;
            _imageEncrypted = false;
            _encKey = Encoding.UTF8.GetBytes("mYk3Y");
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

                Bitmap img1 = new Bitmap(LoadedImgPath.Content.ToString());
                Bitmap img2 = new Bitmap(LoadedImgPath.Content.ToString());
            }
        }

        private byte[] EncodeMsg(string MsgToPut)
        {
            if (EncKey.Text.Length > 0) _encKey = Encoding.UTF8.GetBytes(EncKey.Text);

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

        private string DecodeMsg(byte[] encodedMsg)
        {
            string plaintext = null;
            
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = _encKey.Take(32).ToArray();
                aesAlg.IV = _encKey.Skip(32).ToArray().Take(16).ToArray();
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                using (MemoryStream msDecrypt = new MemoryStream(encodedMsg))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }

            }
            return plaintext;
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
                for (int j = 3; j >= 0; --j)
                {
                    Color CurPixel = ImgToMod.GetPixel(x, y);
                    byte R = CurPixel.R,
                        G = CurPixel.G,
                        B = CurPixel.B;
                    bool a1 = GetBit(CurPixel.R, 0),
                        a2 = GetBit(CurPixel.G, 0),
                        a3 = GetBit(CurPixel.B, 0);

                    bool M1 = GetBit(NewMsg[i], j*2+1),    //x1
                        M2 = GetBit(NewMsg[i], j*2);  //x2

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

        private bool GetBit(byte fromByte, int bitNum)
        {
            return Convert.ToBoolean((fromByte >> bitNum) & 1);
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
                _imageLoaded = true;
                UpdateStatus("Image loaded.");
            }
            else UpdateStatus("Image not loaded.");
        }

        private void SaveImgBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog
            {
                Title = "Choose location and name",
                Filter = "Bitmap graphics|*.bmp"
            };
            if (sfd.ShowDialog() == true)
            {
                Bitmap SaveBitmap = BitmapSource2Bitmap(SaveImg.Source);
                //SaveBitmap.Save(sfd.FileName, ImageFormat.Bmp);
                using (MemoryStream memory = new MemoryStream())
                {
                    using (FileStream fs = new FileStream(sfd.FileName, FileMode.Create, FileAccess.ReadWrite))
                    {
                        SaveBitmap.Save(memory, ImageFormat.Bmp);
                        byte[] bytes = memory.ToArray();
                        fs.Write(bytes, 0, bytes.Length);
                    }
                }


                SaveImgPath.Content = sfd.FileName;
                SaveImgPath.ToolTip = sfd.FileName;
                UpdateStatus("Image saved.");
            }
            else UpdateStatus("Image not saved.");
        }

        private void ReadMsgBtn_Click(object sender, RoutedEventArgs e)
        {
            Bitmap ImgToRead = BitmapSource2Bitmap(SaveImg.Source);
            byte[] msg = ReadMsg(ImgToRead);
            CustomMsg.Text = DecodeMsg(msg);
            UpdateStatus("Message read and decoded.");
        }

        private byte[] ReadMsg(Bitmap imgToRead)
        {
            MyPoint[] idx = CalculateIndexes(new MyPoint(0, 0), imgToRead.Width);
            byte lenInByte1 = ReadByte(imgToRead, idx);
            idx = CalculateIndexes(GetNextPoint(idx[3], imgToRead.Width), imgToRead.Width);
            byte lenInByte2 = ReadByte(imgToRead, idx);
            int len = lenInByte1 * 256 + lenInByte2;

            byte[] res = new byte[len];

            for (int i = 0; i < len; i++)
            {
                idx = CalculateIndexes(GetNextPoint(idx[3], imgToRead.Width), imgToRead.Width);
                res[i] = ReadByte(imgToRead, idx);
            }
            return res;
        }

        private byte ReadByte(Bitmap imgToRead, MyPoint[] idx)
        {
            byte res = 0;
            Color[] px = new Color[4];
            for (int i = 0; i < 4; ++i)
            {
                px[i] = imgToRead.GetPixel(idx[i].X, idx[i].Y);
                bool[] bits = ReadTwoBits(px[i]);
                res <<= 1;
                res |= Convert.ToByte(bits[0]);
                res <<= 1;
                res |= Convert.ToByte(bits[1]);
            }
            return res;
        }

        private bool[] ReadTwoBits(Color pixel)
        {
            bool M1 = GetBit(pixel.R, 0),
                M2 = GetBit(pixel.G, 0);
            if (!GetBit(pixel.B, 0)) // a3==0
            {
                return new bool[] { M1, M2 };
            }
            else
            {
                return new bool[] { (!M1), (!M2) };
            }
        }

        private MyPoint[] CalculateIndexes(MyPoint start, int width)
        {
            MyPoint[] indexes = new MyPoint[] { start, new MyPoint(0, 0), new MyPoint(0, 0), new MyPoint(0, 0) };
            
            for (int i = 1; i < 4; ++i)
            {
                indexes[i].Y = indexes[i - 1].Y;
                indexes[i].X = indexes[i - 1].X + 1;
                if (indexes[i].X >= width)
                {
                    indexes[i].X = 0;
                    indexes[i].Y++;
                }
            }
            return indexes;
        }

        private MyPoint GetNextPoint(MyPoint CurPoint, int width)
        {
            if (CurPoint.X + 1 < width) return new MyPoint(CurPoint.X + 1, CurPoint.Y);
            else return new MyPoint(0, CurPoint.Y + 1);
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
        
        private Bitmap BitmapSource2Bitmap(BitmapSource bitmapsource)
        {
            Bitmap bitmap;
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                enc.Save(outStream);
                bitmap = new Bitmap(outStream);
            }
            return bitmap;
        }

        private Bitmap BitmapSource2Bitmap(System.Windows.Media.ImageSource imagesource)
        {
            Bitmap bitmap;
            bitmap = BitmapSource2Bitmap(imagesource as BitmapSource);
            return bitmap;
        }

        private void UpdateStatus(string status)
        {
            DateTime localDate = DateTime.Now;
            StatusLabel.Content = localDate + " - " + status;
        }
    }
}
