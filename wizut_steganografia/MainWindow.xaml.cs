using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Linq;

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
                //_bmp = new Bitmap(ImgToMod);
                if (ImgToMod == null)
                {
                    UpdateStatus("Message is too long for this bitmap file.", true);
                    return;
                }
                //SaveImg.Source = Bitmap2BitmapSource(ImgToMod);
                SaveImg.Source = ImageSourceFromBitmap(ImgToMod);
                //_bmp = GetBitmap((BitmapSource)SaveImg.Source);
                _imageEncrypted = true;
                UpdateStatus("Message embedded into bitmap file.");
                ImgToMod.Dispose();
            }
            else
            {
                UpdateStatus("No image to embed the message. Load something first.", true);
            }
        }

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);
        public System.Windows.Media.ImageSource ImageSourceFromBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }

        private byte[] EncodeMsg(string MsgToPut)
        {
            if (EncKey.Text.Length > 0) _encKey = Encoding.UTF8.GetBytes(EncKey.Text);

            byte[] encrypted;
            using (Aes aesAlg = Aes.Create())
            {
                using (SHA512 shaM = new SHA512Managed())
                {
                    _encKey = shaM.ComputeHash(Encoding.UTF8.GetBytes(EncKey.Text));
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
                using (SHA512 shaM = new SHA512Managed())
                {
                    _encKey = shaM.ComputeHash(Encoding.UTF8.GetBytes(EncKey.Text));
                }
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

            int seed = GetSeed();
            Random rX = new Random(seed),
                rY = new Random(seed);
            int[,] CoordsBase = new int[NewMsg.Length * 4, 2];

            for (int i=0; i < NewMsg.Length; ++i)
            {
                for (int j = 3; j >= 0; --j)
                {
                    int x, y;
                    do
                    {
                        x = rX.Next(0, ImgToMod.Width);
                        y = rY.Next(0, ImgToMod.Height);
                    } while (CoordsNotDuplicated(CoordsBase, x, y, (i * 4) + (3 - j)));

                    CoordsBase[(i * 4) + (3 - j), 0] = x;
                    CoordsBase[(i * 4) + (3 - j), 1] = y;

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
                }
            }
            return ImgToMod;
        }

        private bool CoordsNotDuplicated(int[,] coordsBase, int x, int y, int idx)
        {
            for (int ci = idx - 1; ci >= 0; ci--)
            {
                if ((coordsBase[ci, 0] == x) && (coordsBase[ci, 1] == y)) return false;
            }
            return true;
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
            else UpdateStatus("Image not loaded.", true);
        }

        private void SaveImgBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_imageEncrypted)
            {
                SaveFileDialog sfd = new SaveFileDialog
                {
                    Title = "Choose location and name",
                    Filter = "Bitmap graphics|*.bmp"
                };
                if (sfd.ShowDialog() == true)
                {
                    Bitmap SaveBitmap = GetBitmap((BitmapSource)SaveImg.Source);
                    SaveBitmap.Save(sfd.FileName, ImageFormat.Bmp);
                    SaveBitmap.Dispose();

                    SaveImgPath.Content = sfd.FileName;
                    SaveImgPath.ToolTip = sfd.FileName;
                    UpdateStatus("Image saved.");
                }
                else UpdateStatus("Image not saved.", true);
            }
            else UpdateStatus("No image to save.", true);
        }

        private Bitmap GetBitmap(BitmapSource source)
        {
            Bitmap bmp = new Bitmap(
              source.PixelWidth,
              source.PixelHeight,
              PixelFormat.Format32bppPArgb);
            BitmapData data = bmp.LockBits(
              new Rectangle(System.Drawing.Point.Empty, bmp.Size),
              ImageLockMode.WriteOnly,
              PixelFormat.Format32bppPArgb);
            source.CopyPixels(
              Int32Rect.Empty,
              data.Scan0,
              data.Height * data.Stride,
              data.Stride);
            bmp.UnlockBits(data);
            return bmp;
        }

        private void ReadMsgBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_imageEncrypted || ((bool)ReadFromLoaded.IsChecked && _imageLoaded))
            {
                Bitmap ImgToRead;
                if ((bool)ReadFromLoaded.IsChecked) ImgToRead = BitmapSource2Bitmap(LoadedImg.Source);
                else ImgToRead = BitmapSource2Bitmap(SaveImg.Source);
                byte[] msg = ReadMsg(ImgToRead);
                try
                {
                    CustomMsg.Text = DecodeMsg(msg);
                    UpdateStatus("Message read and decoded.");
                }
                catch (Exception err)
                {
                    CustomMsg.Text = err.Message;
                    UpdateStatus("Error during decoding. Most probably incorrect password. Check error msg below.", true);
                }
                ImgToRead.Dispose();
            }
            else
            {
                UpdateStatus("No image to read message from.", true);
            }
        }

        private byte[] ReadMsg(Bitmap imgToRead)
        {
            int seed = GetSeed();
            Random rX = new Random(seed),
                rY = new Random(seed);
            int[,] LenCoordsBase = new int[8, 2];

            int x, y;

            for (int ir = 0; ir < 8; ++ir)
            {
                do
                {
                    x = rX.Next(0, imgToRead.Width);
                    y = rY.Next(0, imgToRead.Height);
                } while (CoordsNotDuplicated(LenCoordsBase, x, y, ir);

                LenCoordsBase[ir, 0] = x;
                LenCoordsBase[ir, 1] = y;
            }

            //MyPoint[] idx = CalculateIndexes(new MyPoint(0, 0), imgToRead.Width);
            byte lenInByte1 = ReadByte(imgToRead, GetIndexes());
            //idx = CalculateIndexes(GetNextPoint(idx[3], imgToRead.Width), imgToRead.Width);
            byte lenInByte2 = ReadByte(imgToRead, );
            int len = lenInByte1 * 256 + lenInByte2;

            byte[] res = new byte[len];

            for (int i = 0; i < len; i++)
            {
                idx = CalculateIndexes(GetNextPoint(idx[3], imgToRead.Width), imgToRead.Width);
                res[i] = ReadByte(imgToRead, idx);
            }
            return res;
        }

        private MyPoint[] GetRowsOf2DArray(int[,] arr, int start, int end)
        {
            List 
            for (int xi = start; xi <= end; ++xi)
            {
                for (int yj = 0; yj < arr.GetLength(1); yj++)
                {
                    return new MyPoint(arr[xi, 0], arr[xi, 1]);
                }
            }
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
            bitmap = BitmapSource2Bitmap((BitmapSource)imagesource);
            return bitmap;
        }

        private void ImgPath_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", Directory.GetParent(((System.Windows.Controls.Label)sender).Content.ToString()).FullName);
                UpdateStatus("Dir opened.");
            }
            catch (Exception err)
            {
                CustomMsg.Text = err.Message;
                UpdateStatus("Cannot open the dir. Error msg below.", true);
            }
        }

        private void UpdateStatus(string status, bool isError = false)
        {
            DateTime localDate = DateTime.Now;
            StatusLabel.Foreground = isError ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Green;
            StatusLabel.Content = localDate + " - " + status;
            StatusLabel.ToolTip = status;
        }

        private int GetSeed()
        {
            int seed = 0;
            char[] temp = StegKey.Text.ToCharArray();
            foreach (char t in temp)
            {
                seed += t;
            }
            return seed;
        }
    }
}
