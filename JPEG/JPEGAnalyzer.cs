using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace JPEG
{
    class JPEGAnalyzer
    {
        private List<Point> _points;
        private Bitmap _modBitmap;
        private LockBitmap _lockOrig;
        private LockBitmap _lockMod;
        public JPEGAnalyzer (string path)
        {
            _points = new List<Point>();
            Bitmap _origBitmap = new Bitmap(path);
            MemoryStream memoryStream = new MemoryStream();
            _origBitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Jpeg);
            _modBitmap = new Bitmap(memoryStream);
            memoryStream.Dispose();
            _lockOrig = new LockBitmap(_origBitmap);
            _lockMod = new LockBitmap(_modBitmap);
        }

        public List<Point> GetModifiedPixelsList()
        {
            //for(int i=0; i < _origBitmap.Width; ++i)
            //{
            //    for (int j = 0; j < _origBitmap.Height; ++j)
            //    {
            //        if (_origBitmap.GetPixel(i, j) != _modBitmap.GetPixel(i, j)) _points.Add(new Point(i, j));
            //        //if ((_origBitmap.GetPixel(i, j).R != _modBitmap.GetPixel(i, j).R) || (_origBitmap.GetPixel(i, j).G != _modBitmap.GetPixel(i, j).G) || (_origBitmap.GetPixel(i, j).B != _modBitmap.GetPixel(i, j).B)) _points.Add(new Point(i, j));
            //    }
            //}
            _lockOrig.LockBits();
            _lockMod.LockBits();
            for (int y = 0; y < _lockOrig.Height; y++)
            {
                for (int x = 0; x < _lockOrig.Width; x++)
                {
                    if (_lockOrig.GetPixel(x, y) != _lockMod.GetPixel(x, y))
                    {
                        _points.Add(new Point(x, y));
                        _lockMod.SetPixel(x, y, Color.Red);
                    }
                }
            }
            _lockMod.UnlockBits();
            _lockOrig.UnlockBits();
            return _points;
        }

        public int GetNumOfModifiedPixels()
        {
            return _points.Count;
        }

        public Bitmap MarkPixels()
        {
            return _modBitmap;
        }
    }
}
