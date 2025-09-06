using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Convolve
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var convolveEngine = new ConvolveEngine();
            bool cont = true;
            while (cont)
            {
                Console.Write("Enter sigma: ");
                double sigma = double.Parse(Console.ReadLine());
                Console.WriteLine();
                Console.Write("Enter radius: ");
                int radius = int.Parse(Console.ReadLine());
                Console.WriteLine();
                Console.Write("Enter image filename: ");
                Bitmap img;
                try
                {
                    var fileName = Console.ReadLine();
                    img = LoadImageFromPath(fileName);
                    var result = convolveEngine.Convolve2D(img, sigma, radius);
                    result.Save(@"D:\Users\Joe-Mar Gonzales\Desktop\result-image.png", ImageFormat.Png);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.Write("Try again? (y/n): ");
                    cont = Console.ReadLine() == "y";
                    continue;
                }

                Console.Write("Try again? (y/n): ");
                cont = Console.ReadLine() == "y";
            }
        }

        private static Bitmap LoadImageFromPath(string filename)
        {
            Bitmap img = new Bitmap(filename);
            return img;
        }
    }

    public class ConvolveEngine
    {
        public double[] ComputeGaussian1DKernel(double sigma, int radius)
        {
            var arrayLength = radius * 2 + 1;
            var kernel = new double[arrayLength];

            //portion of the normal distribution function.
            var c = 1.0 / (sigma * Math.Sqrt(2 * Math.PI));

            //keep track of the total value for normalization.
            double totalValue = 0;
            for (int i = -radius; i <= radius; i++)
            {
                double exponentOfEuler = (i * i) / (2 * sigma * sigma);
                double euler = Math.Exp(-exponentOfEuler);
                double elementValue = c * euler;
                kernel[i + radius] = elementValue;
                totalValue += elementValue;
            }
            //normalize the whole set so the sum of all values equal 1
            for (int i = 0; i < arrayLength; i++)
            {
                kernel[i] = kernel[i] * (1 / totalValue);
            }

            return kernel;
        }

        public double[,] ComputeGaussian2DKernel(double sigma, int radius)
        {
            var arrayLength = (radius * 2) + 1;
            var kernel = new double[arrayLength, arrayLength];

            //portion of the gaussian function
            //the gaussian function we're using is the product of two gaussian functions for x and y ( f(x)f(y) )
            double c = 1.0 / (2 * Math.PI * sigma * sigma);

            //keep track of the total value for normalization.
            double totalValue = 0;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    //portion of the normal distribution function that is dependent on the current x and y coordinates.
                    double exponentOfEuler = ((x * x) + (y * y)) / (2 * sigma * sigma);
                    kernel[y + radius, x + radius] = c * Math.Exp(-exponentOfEuler);
                    totalValue += kernel[y + radius, x + radius];
                }
            }
            //normalize the whole set so the sum of all values equal 1
            for (int y = 0; y < arrayLength; y++)
            {
                for (int x = 0; x < arrayLength; x++)
                {
                    kernel[y, x] = kernel[y, x] * (1.0 / totalValue);
                }
            }
            return kernel;
        }

        public Bitmap Convolve2D(Bitmap sourceBitmap, double sigma, int radius)
        {
            //lock bitmap data to memory.
            BitmapData imgData = sourceBitmap.LockBits(
                new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            //array to hold bitmap pixel data.
            byte[] pixelBuffer = new byte[imgData.Stride * imgData.Height];
            byte[] resultPixelBuffer = new byte[imgData.Stride * imgData.Height];

            //copy from memory to pixel array.
            Marshal.Copy(imgData.Scan0, pixelBuffer, 0, pixelBuffer.Length);
            //release bitmap data from memory.
            sourceBitmap.UnlockBits(imgData);

            var kernel = ComputeGaussian2DKernel(sigma, radius);

            //this operation becomes more expensive as the size of the kernel and/or image increases. O(image height * image width * kernel width^2)
            //a 2-pass one dimensional convolution is better.
            for (int imgY = 0; imgY < sourceBitmap.Height; imgY++)
            {
                for (int imgX = 0; imgX < sourceBitmap.Width; imgX++)
                {
                    //init color values;
                    double red = 0;
                    double green = 0;
                    double blue = 0;

                    //this is the location of the byte we're currently computing the convolution for.
                    //we multiply the x index by 4 since each pixel has 4 bytes for the 4 channels (rbga)
                    var byteOffset = imgY * imgData.Stride + imgX * 4;

                    //ignore kernel matrix values that lie outside the image.
                    //e.g. on the first row and column of pixels (imgY = 0, imgX = 0) ignore the the rows and columns of the kernel at the top and left of the center.
                    var kernelYStart = imgY < radius ? -imgY : -radius;
                    var kernelXStart = imgX < radius ? -imgX : -radius;
                    var kernelYLimit = (imgY + radius + 1) > sourceBitmap.Height ? sourceBitmap.Height - (imgY + 1) : radius;
                    var kernelXLimit = (imgX + radius + 1) > sourceBitmap.Width ? sourceBitmap.Width - (imgX + 1) : radius;

                    //loop through the kernel
                    for (int kernelY = kernelYStart; kernelY <= kernelYLimit; kernelY++)
                    {
                        for (int kernelX = kernelXStart; kernelX <= kernelXLimit; kernelX++)
                        {
                            //we start calculation from this position in the image pixel array.
                            var calculationOffset = byteOffset + (kernelX * 4) + (kernelY * imgData.Stride);

                            //the color channels are arranged in the pixel array in this order B G R Alpha
                            blue += pixelBuffer[calculationOffset] * kernel[kernelY + radius, kernelX + radius];
                            green += pixelBuffer[calculationOffset + 1] * kernel[kernelY + radius, kernelX + radius];
                            red += pixelBuffer[calculationOffset + 2] * kernel[kernelY + radius, kernelX + radius];
                        }
                    }

                    //our kernel is normalized so color values won't exceed 255, but check just in case.
                    blue = blue > 255 ? 255 : (blue < 0 ? 0 : blue);
                    green = green > 255 ? 255 : (green < 0 ? 0 : green);
                    red = red > 255 ? 255 : (red < 0 ? 0 : red);

                    resultPixelBuffer[byteOffset] = (byte)blue;
                    resultPixelBuffer[byteOffset + 1] = (byte)green;
                    resultPixelBuffer[byteOffset + 2] = (byte)red;
                    resultPixelBuffer[byteOffset + 3] = 255; //this is for alpha
                }
            }

            var result = new Bitmap(sourceBitmap.Width, sourceBitmap.Height);

            BitmapData resultBitmapData = result.LockBits(new Rectangle(0, 0, result.Width, result.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(resultPixelBuffer, 0, resultBitmapData.Scan0, resultPixelBuffer.Length);
            result.UnlockBits(resultBitmapData);

            return result;
        }
    }
}