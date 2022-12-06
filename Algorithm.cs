using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace grafa7
{
    public static class Algorithm
    {

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

            if (histPlot == null)
                return new double[][] { histogram, histogramB, histogramG, histogramR };

            histPlot.Reset();
            histPlot.Plot.AddScatter(xRow, histogram, Color.Black);
            histPlot.Plot.AddScatter(xRow, histogramB, Color.Blue);
            histPlot.Plot.AddScatter(xRow, histogramR, Color.Red);
            histPlot.Plot.AddScatter(xRow, histogramG, Color.Green);

            histPlot.Refresh();

            return new double[][] { histogram, histogramB, histogramG, histogramR };
        }

        public static Bitmap GetOtsu(Bitmap bmp, WpfPlot plot)
        {
            double[] histogram = getHistogramData(bmp, null)[0];
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
                _ = getHistogramData(bmp, plot);

                return bmp;
            }
        }
    }
}
