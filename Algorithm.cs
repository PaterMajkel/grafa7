using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace grafa7
{
    public static class Algorithm
    {
        public static Bitmap AnalizeAndBinarize(out string text, Bitmap bmp, int range = 3, bool bruteForce = false)
        {
            var localValues = GetLocalValues(bmp);

            var globalMean = GetGlobalValues(bmp);

            var histogram = getHistogramData(bmp);
            Dictionary<string, double> names = new() { { "Otsu", 0 }, { "Niblack", 0 }, { "Mean Value", 0 }, /*{ "Mean Iterative Selection", 0 },*/ { "Entropy Selection", 0 }, { "Bernsen", 0 }, { "Suavola", 0 } };
            if (bruteForce)
            {
                Bitmap[] bitmaps = new Bitmap[names.Count];
                for (int i = 0; i < names.Count; i++)
                {
                    bitmaps[i] = (Bitmap)bmp.Clone();
                }
                Parallel.For(0, names.Count, (i) =>
                {
                    var newBmp = bitmaps[i];
                    var name = names.ToArray()[i];
                    switch (name.Key)
                    {
                        case "Otsu":
                            {
                                var forHistogram = getHistogramData(GetOtsu(newBmp));
                                names[name.Key] = (forHistogram[0][255] / forHistogram[0][0]);
                                break;
                            }
                        case "Niblack":
                            {
                                var forHistogram = getHistogramData(Niblack(newBmp, range));
                                names[name.Key] = (forHistogram[0][255] / forHistogram[0][0]);
                                break;
                            }
                        case "Mean Value":
                            {
                                var forHistogram = getHistogramData(HandThreshold(newBmp, globalMean));
                                names[name.Key] = (forHistogram[0][255] / forHistogram[0][0]);
                                break;
                            }
                        case "Mean Iterative Selection":
                            {
                                var forHistogram = getHistogramData(MeanISel(newBmp));
                                names[name.Key] = (forHistogram[0][255] / forHistogram[0][0]);
                                break;
                            }
                        case "Entropy Selection":
                            {
                                var forHistogram = getHistogramData(Entropy(newBmp));
                                names[name.Key] = (forHistogram[0][255] / forHistogram[0][0]);
                                break;
                            }
                        case "Bernsen":
                            {
                                var forHistogram = getHistogramData(Bernsen(newBmp, range));
                                names[name.Key] = (forHistogram[0][255] / forHistogram[0][0]);
                                break;
                            }
                        case "Suavola":
                            {
                                var forHistogram = getHistogramData(Sauvola(newBmp, range, 0.25, 50));
                                names[name.Key] = (forHistogram[0][255] / forHistogram[0][0]);
                                break;
                            }

                    }
                });
            }
            else
            if (((localValues[2] + localValues[1]) / 2.0) / globalMean > 1.2)
            {
                //Huge disperities -> locals but Niblack should have been excluded | oh well
                if ((histogram[0][0] + histogram[0][255]) / histogram[0].Length * 100 > 3)
                {
                    //big noise
                    text = "Sauvola";
                    return Sauvola(bmp, range, 0.25, 50);
                }
                else
                {
                    //small noise
                    text = "Niblack";
                    return Niblack(bmp, range);
                }
            }
            else
            {
                //globals ->
                if ((histogram[0][0] + histogram[0][255]) / histogram[0].Length * 100 > 3)
                {
                    //big noise
                    text = "Otsu";
                    return GetOtsu(bmp);
                }
                else
                {
                    //binarize by mean value
                    //small noise
                    text = "Mean value";
                    return HandThreshold(bmp, globalMean);
                }
            }

            var bestAlg = names.Values.OrderBy(p => p).ToArray()[names.Count / 2]; //names.Values.Aggregate((x, y) => Math.Abs(x - 1) < Math.Abs(y - 1) ? x : y);
            var resName = names.First(p => p.Value == bestAlg);
            switch (resName.Key)
            {
                case "Otsu":
                    {
                        text = resName.Key;
                        return GetOtsu(bmp);
                    }
                case "Niblack":
                    {
                        text = resName.Key;
                        return Niblack(bmp, range);
                    }
                case "Mean Value":
                    {
                        text = resName.Key;
                        return HandThreshold(bmp, globalMean);
                    }
                case "Mean Iterative Selection":
                    {
                        text = resName.Key;
                        return MeanISel(bmp);
                    }
                case "Entropy Selection":
                    {
                        text = resName.Key;
                        return Entropy(bmp);
                    }
                case "Bernsen":
                    {
                        text = resName.Key;
                        return Bernsen(bmp, range);
                    }
                case "Suavola":
                    {
                        text = resName.Key;
                        return Sauvola(bmp, range, 0.25, 50);
                    }
            }

            text = "Error";
            return null;

        }

        public static Bitmap HandThreshold(Bitmap bmp, int threshold)
        {
            var data = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Imaging.ImageLockMode.ReadWrite,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb
            );
            var bmpData = new byte[data.Stride * data.Height];

            Marshal.Copy(data.Scan0, bmpData, 0, bmpData.Length);

            for (int i = 0; i < bmpData.Length; i += 3)
            {
                if ((bmpData[i] + bmpData[i + 1] + bmpData[i + 2]) / 3 < threshold)
                    bmpData[i] = bmpData[i + 1] = bmpData[i + 2] = 0;
                else
                    bmpData[i] = bmpData[i + 1] = bmpData[i + 2] = 255;

            }
            Marshal.Copy(bmpData, 0, data.Scan0, bmpData.Length);
            bmp.UnlockBits(data);
            return bmp;
        }
        public static int[] GetLocalValues(Bitmap bmp, int range = 3)
        {
            byte[,] data = ImageTo2DByteArray(bmp);
            List<int> values = new();

            for (int y = 0; y < bmp.Height; y += range)
                for (int x = 0; x < bmp.Width; x += range)
                {
                    int min = 255, max = 0;
                    for (int z = y - range; z <= y + range; ++z)
                    {
                        if (z >= 0 && z < bmp.Height)
                            for (int i = x - range; i <= x + range; ++i)
                            {
                                if (i >= 0 && i < bmp.Width)
                                {
                                    if (data[z, i] > max)
                                        max = data[z, i];
                                    if (data[z, i] < min)
                                        min = data[z, i];
                                }
                            }
                    }
                    values.Add((max + min) / 2);
                    //liczymy contrast measure, ale na chuj???
                }
            return new int[] { (int)values.Average(), values.Min(), values.Max() };
        }

        public static int GetGlobalValues(Bitmap bmp)
        {
            byte[,] data = ImageTo2DByteArray(bmp);

            int mean = 0;
            for (int i = 0; i < data.GetLength(0); i++)
            {
                for (int j = 0; j < data.GetLength(1); j++)
                {

                    mean += data[i, j];
                }
            }
            return mean / data.Length;
        }

        public static Bitmap MeanISel(Bitmap bmp)
        {
            BitmapData bitmapData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);

            var bmpData = new byte[bitmapData.Stride * bitmapData.Height];
            double[] histogramData = new double[256];

            Marshal.Copy(bitmapData.Scan0, bmpData, 0, bmpData.Length);

            for (int i = 0; i < bmpData.Length; i += 3)
            {
                histogramData[(bmpData[i] + bmpData[i + 1] + bmpData[i + 2]) / 3]++;
            }

            double Tk = 0, Tkt = 127;

            while (Tk != Tkt)
            {
                double a = 0, b = 0, c = 0, d = 0;
                for (int i = 0; i < Tkt; i++)
                {
                    a += i * histogramData[i];
                }
                for (int i = 0; i < Tkt; i++)
                {
                    b += histogramData[i];
                }
                if (b == 0) b++;
                b *= 2;
                for (int i = (int)Tkt; i < 255; i++)
                {
                    c += i * histogramData[i];
                }
                for (int i = (int)Tkt; i < 255; i++)
                {
                    d += histogramData[i];
                }
                if (d == 0) b++;
                d *= 2;

                Tkt = Tk;
                Tk = (a / b) + (c / d);
            }

            for (int y = 0; y < bmpData.Length; y += 3)
            {
                if ((bmpData[y] + bmpData[y + 1] + bmpData[y + 2]) / 3 < Tk)
                    bmpData[y] = bmpData[y + 1] = bmpData[y + 2] = 0;
                else
                    bmpData[y] = bmpData[y + 1] = bmpData[y + 2] = 255;
            }

            Marshal.Copy(bmpData, 0, bitmapData.Scan0, bmpData.Length);

            bmp.UnlockBits(bitmapData);

            return bmp;
        }

        public static Bitmap Entropy(Bitmap bmp)
        {
            BitmapData bitmapData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);

            var bmpData = new byte[bitmapData.Stride * bitmapData.Height];
            double[] histogramData = new double[256];

            Marshal.Copy(bitmapData.Scan0, bmpData, 0, bmpData.Length);

            for (int i = 0; i < bmpData.Length; i += 3)
            {
                histogramData[(bmpData[i] + bmpData[i + 1] + bmpData[i + 2]) / 3]++;
            }

            var pixelCount = bitmapData.Width * bitmapData.Height;

            for (int i = 0; i < histogramData.Length; i++)
            {
                histogramData[i] /= (double)pixelCount;
                histogramData[i] *= 100;
            }
            double sum = 0;

            for (int i = 1; i < histogramData.Length; i++)
            {
                if (histogramData[i] != 0)
                    sum += histogramData[i] * Math.Log(histogramData[i]);
            }

            sum *= -1;

            for (int y = 0; y < bmpData.Length; y += 3)
            {
                if ((bmpData[y] + bmpData[y + 1] + bmpData[y + 2]) / 3 < sum)
                    bmpData[y] = bmpData[y + 1] = bmpData[y + 2] = 0;
                else
                    bmpData[y] = bmpData[y + 1] = bmpData[y + 2] = 255;
            }

            Marshal.Copy(bmpData, 0, bitmapData.Scan0, bmpData.Length);

            bmp.UnlockBits(bitmapData);

            return bmp;
        }



        public static double[][] getHistogramData(Bitmap bmp)
        {
            var data = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Imaging.ImageLockMode.ReadWrite,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb
            );
            var bmpData = new byte[data.Stride * data.Height];

            Marshal.Copy(data.Scan0, bmpData, 0, bmpData.Length);
            // Przerzuci z Bitmapy do tablicy

            double[] histogramB = new double[256];
            double[] histogramR = new double[256];
            double[] histogramG = new double[256];
            double[] histogram = new double[256];


            for (int i = 0; i < bmpData.Length; i++)
            {
                ++histogram[bmpData[i]];
                if ((i) % 3 == 0)
                    ++histogramB[bmpData[i]];
                if ((i + 2) % 3 == 0)
                    ++histogramG[bmpData[i]];
                if ((i + 1) % 3 == 0)
                    ++histogramR[bmpData[i]];
            }
            double max = histogram.Max();
            for (int i = 0; i < histogram.Length; i++)
            {
                histogram[i] = (double)(histogram[i] / max * (double)data.Height);
                histogramB[i] = (double)(histogramB[i] / max * (double)data.Height);
                histogramR[i] = (double)(histogramR[i] / max * (double)data.Height);
                histogramG[i] = (double)(histogramG[i] / max * (double)data.Height);

            }


            double[] xRow = new double[histogram.Length];
            for (int i = 0; i < histogram.Length; i++)
            {
                xRow[i] = i;
            }
            bmp.UnlockBits(data);

            return new double[][] { histogram, histogramB, histogramG, histogramR };

        }

        public static Bitmap GetOtsu(Bitmap bmp)
        {
            double[] histogram = getHistogramData(bmp)[0];
            double avgValue = 0;
            for (int i = 0; i < 256; i++)
            {
                avgValue += histogram[i];
            }

            avgValue /= histogram.Length;


            ////Global mean
            //double mg = 0;
            //for (int i = 0; i < 255; i++)
            //{
            //    mg += i * histogram[i];
            //}

            ////Get max between-class variance
            //double bcv = 0;
            //int threshold = 0;
            //for (int i = 0; i < 256; i++)
            //{
            //    double cs = 0;
            //    double m = 0;
            //    for (int j = 0; j < i; j++)
            //    {
            //        cs += histogram[j];
            //        m += j * histogram[j];
            //    }

            //    if (cs == 0)
            //    {
            //        continue;
            //    }

            //    double old_bcv = bcv;
            //    bcv = Math.Max(bcv, Math.Pow(mg * cs - m, 2) / (cs * (1 - cs)));

            //    if (bcv > old_bcv)
            //    {
            //        threshold = i;
            //    }
            //}


            unsafe
            {
                BitmapData bitmapData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);

                int bytesPerPixel = Image.GetPixelFormatSize(bmp.PixelFormat) / 8;
                int heightInPixels = bitmapData.Height;
                int widthInBytes = bitmapData.Width * bytesPerPixel;
                byte* PtrFirstPixel = (byte*)bitmapData.Scan0;


                Parallel.For(0, heightInPixels, y =>
                {
                    byte* currentLine = PtrFirstPixel + (y * bitmapData.Stride);
                    for (int x = 0; x < widthInBytes; x += bytesPerPixel)
                    {
                        if ((currentLine[x] + currentLine[x + 1] + currentLine[x + 2] / 3) > avgValue)
                            currentLine[x] = currentLine[x + 1] = currentLine[x + 2] = byte.MaxValue;
                        else
                            currentLine[x] = currentLine[x + 1] = currentLine[x + 2] = byte.MinValue;


                    }
                });
                bmp.UnlockBits(bitmapData);

                return bmp;
            }
        }

        public static Bitmap Bernsen(Bitmap bmp, int range = 3, int limit = 15)
        {
            byte[,] data = ImageTo2DByteArray(bmp);
            byte[,] mean = new byte[bmp.Height, bmp.Width];
            byte[,] contrast = new byte[bmp.Height, bmp.Width];

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            byte[] vs = new byte[bmpData.Stride * bmpData.Height];
            Marshal.Copy(bmpData.Scan0, vs, 0, vs.Length);

            for (int y = 0; y < bmp.Height; ++y)
                for (int x = 0; x < bmp.Width; ++x)
                {
                    int min = 255, max = 0;
                    for (int z = y - range; z <= y + range; ++z)
                    {
                        if (z >= 0 && z < bmp.Height)
                            for (int i = x - range; i <= x + range; ++i)
                            {
                                if (i >= 0 && i < bmp.Width)
                                {
                                    if (data[z, i] > max)
                                        max = data[z, i];
                                    if (data[z, i] < min)
                                        min = data[z, i];
                                }
                            }
                    }
                    mean[y, x] = (byte)((max + min) / 2);
                    //liczymy contrast measure, ale na chuj???
                    contrast[y, x] = (byte)((max - min));
                }

            //no idea how to use it
            for (int y = 0; y < bmp.Height; ++y)
                for (int x = 0; x < bmp.Width; ++x)
                {
                    if (contrast[y, x] < limit)
                        if (mean[y, x] >= 128)
                            vs[y * bmpData.Stride + (x * 3)] = vs[y * bmpData.Stride + (x * 3 + 1)] = vs[y * bmpData.Stride + (x * 3 + 2)] = byte.MaxValue;
                        else
                            vs[y * bmpData.Stride + (x * 3)] = vs[y * bmpData.Stride + (x * 3 + 1)] = vs[y * bmpData.Stride + (x * 3 + 2)] = byte.MinValue;
                    else
                        if (data[y, x] >= mean[y, x])
                        vs[y * bmpData.Stride + (x * 3)] = vs[y * bmpData.Stride + (x * 3 + 1)] = vs[y * bmpData.Stride + (x * 3 + 2)] = byte.MaxValue;
                    else
                        vs[y * bmpData.Stride + (x * 3)] = vs[y * bmpData.Stride + (x * 3 + 1)] = vs[y * bmpData.Stride + (x * 3 + 2)] = byte.MinValue;
                }
            Marshal.Copy(vs, 0, bmpData.Scan0, vs.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        public static byte[,] ImageTo2DByteArray(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            byte[] bytes = new byte[height * data.Stride];
            try
            {
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            byte[,] result = new byte[height, width];
            for (int y = 0; y < height; ++y)
                for (int x = 0; x < width; ++x)
                {
                    int offset = y * data.Stride + x * 3;
                    result[y, x] = (byte)((bytes[offset + 0] + bytes[offset + 1] + bytes[offset + 2]) / 3);
                }
            return result;
        }
        public static Bitmap Niblack(Bitmap bmp, int range, double k = 0.1)
        {
            byte[,] data = ImageTo2DByteArray(bmp);
            byte[,] mean = new byte[bmp.Height, bmp.Width];
            double[,] standardDeviation = new double[bmp.Height, bmp.Width];

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            byte[] vs = new byte[bmpData.Stride * bmpData.Height];
            Marshal.Copy(bmpData.Scan0, vs, 0, vs.Length);

            for (int y = 0; y < bmp.Height; ++y)
                for (int x = 0; x < bmp.Width; ++x)
                {
                    int min = 255, max = 0;
                    for (int z = y - range; z <= y + range; ++z)
                    {
                        if (z >= 0 && z < bmp.Height)
                            for (int i = x - range; i <= x + range; ++i)
                            {
                                if (i >= 0 && i < bmp.Width)
                                {
                                    if (data[z, i] > max)
                                        max = data[z, i];
                                    if (data[z, i] < min)
                                        min = data[z, i];
                                }
                            }
                    }
                    mean[y, x] = (byte)((max + min) / 2);
                    //liczymy contrast measure, ale na chuj???
                    standardDeviation[y, x] = Math.Sqrt((Math.Pow(data[y, x] - mean[y, x], 2) + Math.Pow(min - mean[y, x], 2) + Math.Pow(max - mean[y, x], 2)) / 2);
                }

            //no idea how to use it
            for (int y = 0; y < bmp.Height; ++y)
                for (int x = 0; x < bmp.Width; ++x)
                {
                    if (data[y, x] < mean[y, x] - k * standardDeviation[y, x])
                        vs[y * bmpData.Stride + (x * 3)] = vs[y * bmpData.Stride + (x * 3 + 1)] = vs[y * bmpData.Stride + (x * 3 + 2)] = byte.MinValue;
                    else
                        vs[y * bmpData.Stride + (x * 3)] = vs[y * bmpData.Stride + (x * 3 + 1)] = vs[y * bmpData.Stride + (x * 3 + 2)] = byte.MaxValue;
                }
            Marshal.Copy(vs, 0, bmpData.Scan0, vs.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }

        public static Bitmap Sauvola(Bitmap bmp, int range, double k, int R)
        {
            byte[,] data = ImageTo2DByteArray(bmp);
            byte[,] mean = new byte[bmp.Height, bmp.Width];
            double[,] standardDeviation = new double[bmp.Height, bmp.Width];

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            byte[] vs = new byte[bmpData.Stride * bmpData.Height];
            Marshal.Copy(bmpData.Scan0, vs, 0, vs.Length);

            for (int y = 0; y < bmp.Height; ++y)
                for (int x = 0; x < bmp.Width; ++x)
                {
                    int min = 255, max = 0;
                    for (int z = y - range; z <= y + range; ++z)
                    {
                        if (z >= 0 && z < bmp.Height)
                            for (int i = x - range; i <= x + range; ++i)
                            {
                                if (i >= 0 && i < bmp.Width)
                                {
                                    if (data[z, i] > max)
                                        max = data[z, i];
                                    if (data[z, i] < min)
                                        min = data[z, i];
                                }
                            }
                    }
                    mean[y, x] = (byte)((max + min) / 2);
                    //liczymy contrast measure, ale na chuj???
                    standardDeviation[y, x] = Math.Sqrt((Math.Pow(data[y, x] - mean[y, x], 2) + Math.Pow(min - mean[y, x], 2) + Math.Pow(max - mean[y, x], 2)) / 2);
                }

            //no idea how to use it
            for (int y = 0; y < bmp.Height; ++y)
                for (int x = 0; x < bmp.Width; ++x)
                {
                    if (data[y, x] < mean[y, x] * (1 + (k * ((standardDeviation[y, x] / R) - 1))))
                        vs[y * bmpData.Stride + (x * 3)] = vs[y * bmpData.Stride + (x * 3 + 1)] = vs[y * bmpData.Stride + (x * 3 + 2)] = byte.MinValue;
                    else
                        vs[y * bmpData.Stride + (x * 3)] = vs[y * bmpData.Stride + (x * 3 + 1)] = vs[y * bmpData.Stride + (x * 3 + 2)] = byte.MaxValue;
                }
            Marshal.Copy(vs, 0, bmpData.Scan0, vs.Length);
            bmp.UnlockBits(bmpData);
            return bmp;
        }
    }
}
