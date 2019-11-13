using System;
using System.Collections.Generic;
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
                    UpdateStatus("Message is too long for this bitmap file.", true);
                    return;
                }
                SaveImg.Source = ImageSourceFromBitmap(ImgToMod);
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

        private byte[] JoinTwoByteArrays(byte[] arr1, byte[] arr2)
        {
            byte[] joined = new byte[arr1.Length + arr2.Length];
            Buffer.BlockCopy(arr1, 0, joined, 0, arr1.Length);
            Buffer.BlockCopy(arr2, 0, joined, arr1.Length, arr2.Length);
            return joined;
        }

        private Bitmap PutMsg(Bitmap ImgToMod, byte[] MsgToPut)
        {
            byte[] tempLen = BitConverter.GetBytes(MsgToPut.Length);
            byte[] msgLen = new byte[2];
            msgLen[0] = tempLen[1];
            msgLen[1] = tempLen[0];
            byte[] NewMsg = JoinTwoByteArrays(msgLen, MsgToPut);

            // error correction coding
            NewMsg = ECC(NewMsg);

            // permutation
            int seed;// = GetSeed();
            Random rX, rY;
            using (SHA512 shaM = new SHA512Managed())
            {
                seed = BitConverter.ToInt32(shaM.ComputeHash(Encoding.UTF8.GetBytes(StegKey.Text)), 0);
                rX = new Random(seed);
                seed = BitConverter.ToInt32(shaM.ComputeHash(Encoding.UTF8.GetBytes(StegKey.Text)), 4);
                rY = new Random(seed);
            }
            //Random rX = new Random(seed),
            //    rY = new Random(seed * 2 / 3 + 1);
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
                    } while (AreCoordsDuplicated(CoordsBase, x, y, (i * 4) + (3 - j)));

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

        private byte[] ECC(byte[] newMsg)
        {
            byte[] ECCMsg = new byte[0];
            foreach (byte b in newMsg)
            {
                byte[] newBytes = MaskByteWithFiveBytes(b);
                byte[] temp = JoinTwoByteArrays(ECCMsg, newBytes);
                ECCMsg = temp;
            }
            return ECCMsg;
        }

        private byte[] MaskByteWithFiveBytes(byte b)
        {
            bool[] bits = new bool[40];
            byte[] bytes = new byte[5];
            bool[] mask1 = new bool[] { true, true, false, true, false };
            bool[] mask0 = new bool[] { false, false, true, false, true };
            for (int bi = 7; bi >= 0; --bi)
            {
                if (GetBit(b, bi)) //1
                {
                    Buffer.BlockCopy(mask1, 0, bits, 5 * (7 - bi), 5);
                }
                else //0
                {
                    Buffer.BlockCopy(mask0, 0, bits, 5 * (7 - bi), 5);
                }
            }

            for (int bi = 0; bi < 5; ++bi)
            {
                bytes[bi] = BitsToByte(SubArray(bits, 8 * bi, 8));
            }
            return bytes;
        }

        private bool[] SubArray(bool[] data, int index, int length)
        {
            bool[] result = new bool[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        private byte BitsToByte(bool[] arr)
        {
            byte val = 0;
            foreach (bool b in arr)
            {
                val <<= 1;
                if (b) val |= 1;
            }
            return val;
        }

        private bool AreCoordsDuplicated(int[,] coordsBase, int x, int y, int idx)
        {
            for (int ci = idx - 1; ci >= 0; ci--)
            {
                if ((coordsBase[ci, 0] == x) && (coordsBase[ci, 1] == y)) return true;
            }
            return false;
        }

        private bool GetBit(byte fromByte, int bitNum)
        { // 0-based, from right
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
                byte[] msg;
                try
                {
                    msg = ReadMsg(ImgToRead);
                    CustomMsg.Text = DecodeMsg(msg);
                    UpdateStatus("Message read and decoded.");
                }
                catch (Exception err)
                {
                    CustomMsg.Text = err.Message;
                    UpdateStatus("Error during decoding. Most probably one of the keys is incorrect. Error msg below.", true);
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
            // first two (ten after ECC) bytes for length
            int seed;// = GetSeed();
            Random rX, rY;
            using (SHA512 shaM = new SHA512Managed())
            {
                seed = BitConverter.ToInt32(shaM.ComputeHash(Encoding.UTF8.GetBytes(StegKey.Text)), 0);
                rX = new Random(seed);
                seed = BitConverter.ToInt32(shaM.ComputeHash(Encoding.UTF8.GetBytes(StegKey.Text)), 4);
                rY = new Random(seed);
            }
            //int seed = GetSeed();
            //Random rX = new Random(seed),
            //    rY = new Random(seed * 2 / 3 + 1);
            int[,] LenCoordsBase = new int[40, 2];

            int x, y;

            for (int ir = 0; ir < 40; ++ir)
            {
                do
                {
                    x = rX.Next(0, imgToRead.Width);
                    y = rY.Next(0, imgToRead.Height);
                } while (AreCoordsDuplicated(LenCoordsBase, x, y, ir));

                LenCoordsBase[ir, 0] = x;
                LenCoordsBase[ir, 1] = y;
            }

            byte lenInByte1 = ReadByteWithoutECC(imgToRead, LenCoordsBase, 0);
            byte lenInByte2 = ReadByteWithoutECC(imgToRead, LenCoordsBase, 20);
            int len = (lenInByte1 * 256 + lenInByte2) * 5; // *5 due to ECC

            // read message, reverse permutation
            if ((len < 0) || (len > 10000))
            {
                UpdateStatus("Incorrect steganographic key.", true);
                throw new Exception("Incorrect steganographic key.");
            }
            else
            {
                byte[] res = new byte[len];

                int[,] CoordsBase = new int[8 + len * 4, 2];
                Array.Copy(LenCoordsBase, CoordsBase, LenCoordsBase.Length);

                for (int ir = 8; ir < len * 4 + 8; ++ir)
                {
                    do
                    {
                        x = rX.Next(0, imgToRead.Width);
                        y = rY.Next(0, imgToRead.Height);
                    } while (AreCoordsDuplicated(CoordsBase, x, y, ir));

                    CoordsBase[ir, 0] = x;
                    CoordsBase[ir, 1] = y;
                }

                for (int i = 0; i < len; i++)
                {
                    res[i] = ReadByte(imgToRead, GetPoints(CoordsBase, i * 4 + 8, i * 4 + 11));
                }

                // reverse error correction coding
                res = ReverseECC(res);
                return res;
            }
        }

        private byte ReadByteWithoutECC(Bitmap imgToRead, int[,] lenCoordsBase, int start)
        {
            byte[] res = new byte[]
            {
                ReadByte(imgToRead, GetPoints(lenCoordsBase, start, start + 3)),
                ReadByte(imgToRead, GetPoints(lenCoordsBase, start + 4, start + 7)),
                ReadByte(imgToRead, GetPoints(lenCoordsBase, start + 8, start + 11)),
                ReadByte(imgToRead, GetPoints(lenCoordsBase, start + 12, start + 15)),
                ReadByte(imgToRead, GetPoints(lenCoordsBase, start + 16, start + 19))
            };
            res = DemaskFiveBytesIntoByte(res);
            return res[0];
        }

        private byte[] ReverseECC(byte[] res)
        {
            byte[] DECCMsg = new byte[0];
            for (int bi = 0; bi < res.Length; bi += 5)
            {
                byte[] newBytes = DemaskFiveBytesIntoByte(new byte[]
                    {
                        res[bi], res[bi + 1], res[bi + 2], res[bi + 3], res[bi + 4]
                    });
                byte[] temp = JoinTwoByteArrays(DECCMsg, newBytes);
                DECCMsg = temp;
            }
            return DECCMsg;
        }

        private byte[] DemaskFiveBytesIntoByte(byte[] bytes)
        {
            bool[] bits = new bool[40];
            bool[] newBits = new bool[8];
            int i = 0;
            foreach (byte b in bytes)
            {
                Buffer.BlockCopy(ByteToBits(b), 0, bits, i, 8);
                i += 8;
            }

            for(i = 0; i < 40; i += 5)
            {
                int ones = (bits[i] ? 1 : 0) + (bits[i + 1] ? 1 : 0) + (bits[i + 2] ? 1 : 0) + (bits[i + 3] ? 1 : 0) + (bits[i + 4] ? 1 : 0);
                if (ones > 2) newBits[i / 5] = true;
                else newBits[i / 5] = false;
            }
            return new byte[] { BitsToByte(newBits) };
        }

        private static bool[] ByteToBits(byte b)
        {
            bool[] result = new bool[8];
            for (int i = 0; i < 8; i++)
                result[i] = (b & (1 << i)) == 0 ? false : true;
            Array.Reverse(result);
            return result;
        }

        private MyPoint[] GetPoints(int[,] arr, int start, int end)
        {
            List<MyPoint> points = new List<MyPoint>();
            for (int xi = start; xi <= end; ++xi)
            {
                points.Add(new MyPoint(arr[xi, 0], arr[xi, 1]));
            }
            return points.ToArray<MyPoint>();
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
        { // discarded due to SHA512 usage
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
