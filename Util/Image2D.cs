﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace Research.GraphBasedShapePrior.Util
{
    public static class Image2D
    {
        public static Image ToRegularImage(Image2D<Color> image)
        {
            return ToRegularImage(image, x => x);
        }

        public static Image ToRegularImage(Image2D<bool> image)
        {
            return ToRegularImage(image, x => x ? Color.White : Color.Black);
        }

        public static Image ToRegularImage(Image2D<bool?> image)
        {
            return ToRegularImage(image, x => x.HasValue ? (x.Value ? Color.Red : Color.Blue) : Color.Black);
        }

        public static Image ToRegularImage(Image2D<double> image, double min, double max)
        {
            if (image == null)
                throw new ArgumentNullException("image");
            if (max <= min)
                throw new ArgumentException("Min should be less than max.");

            double diff = max - min;
            return ToRegularImage(image, x => ZeroOneToRedBlue((MathHelper.Trunc(x, min, max) - min) / diff));
        }

        public static Image ToRegularImage(Image2D<double> image)
        {
            if (image == null)
                throw new ArgumentNullException("image");

            return ToRegularImage(image, image.Min(), image.Max());
        }

        public static Image ToRegularImage(Image2D<ObjectBackgroundTerm> image, double min, double max)
        {
            if (image == null)
                throw new ArgumentNullException("image");
            if (max <= min)
                throw new ArgumentException("Min should be less than max.");

            double diff = max - min;
            return ToRegularImage(image, x => ZeroOneToRedBlue((MathHelper.Trunc(x.ObjectTerm - x.BackgroundTerm, min, max) - min) / diff));
        }

        public static Image ToRegularImage(Image2D<ObjectBackgroundTerm> image)
        {
            if (image == null)
                throw new ArgumentNullException("image");

            double min = image.Min(t => t.ObjectTerm - t.BackgroundTerm);
            double max = image.Max(t => t.ObjectTerm - t.BackgroundTerm);
            return ToRegularImage(image, min, max);
        }

        private static Image ToRegularImage<T>(Image2D<T> image, Func<T, Color> converter)
        {
            if (image == null)
                throw new ArgumentNullException("image");
            if (converter == null)
                throw new ArgumentNullException("converter");
            
            Bitmap result = new Bitmap(image.Width, image.Height);
            for (int i = 0; i < image.Width; ++i)
                for (int j = 0; j < image.Height; ++j)
                    result.SetPixel(i, j, converter(image[i, j]));
            return result;
        }

        public static Image2D<Color> LoadFromFile(string fileName)
        {
            return LoadFromFile(fileName, 1);
        }

        public static Image2D<Color> LoadFromFile(string fileName, double scaleCoeff)
        {
            using (Bitmap image = new Bitmap(fileName))
                return FromRegularImage(image, scaleCoeff);
        }

        public static Image2D<Color> FromRegularImage(Bitmap image)
        {
            return FromRegularImage(image, 1);
        }
        
        public static Image2D<Color> FromRegularImage(Bitmap image, double scaleCoeff)
        {
            if (image == null)
                throw new ArgumentNullException("image");
            
            using (Bitmap scaledImage = new Bitmap(
                image,
                (int)Math.Round(image.Width * scaleCoeff),
                (int)Math.Round(image.Height * scaleCoeff)))
            {
                Image2D<Color> result = new Image2D<Color>(scaledImage.Width, scaledImage.Height);
                for (int x = 0; x < scaledImage.Width; ++x)
                    for (int y = 0; y < scaledImage.Height; ++y)
                        result[x, y] = scaledImage.GetPixel(x, y);

                return result;
            }
        }

        public static void SaveToFile(Image2D<Color> image, string fileName)
        {
            ToRegularImage(image).Save(fileName);
        }

        public static void SaveToFile(Image2D<bool> image, string fileName)
        {
            ToRegularImage(image).Save(fileName);
        }

        public static void SaveToFile(Image2D<bool?> image, string fileName)
        {
            ToRegularImage(image).Save(fileName);
        }

        public static void SaveToFile(Image2D<double> image, double min, double max, string fileName)
        {
            ToRegularImage(image, min, max).Save(fileName);
        }

        public static void SaveToFile(Image2D<double> image, string fileName)
        {
            ToRegularImage(image).Save(fileName);
        }

        public static void SaveToFile(Image2D<ObjectBackgroundTerm> image, double min, double max, string fileName)
        {
            ToRegularImage(image, min, max).Save(fileName);
        }

        public static void SaveToFile(Image2D<ObjectBackgroundTerm> image, string fileName)
        {
            ToRegularImage(image).Save(fileName);
        }

        private static Color ZeroOneToRedBlue(double brightness)
        {
            Debug.Assert(brightness >= 0 && brightness <= 1);

            if (brightness >= 0.5)
            {
                brightness = 2 * brightness - 1;
                int color = (int)Math.Round(255 * brightness);
                return Color.FromArgb(color, 0, 0);
            }
            else
            {
                brightness = 2 * (0.5 - brightness);
                int color = (int)Math.Round(255 * brightness);
                return Color.FromArgb(0, 0, color);
            }
        }
    }

    public class Image2D<T> : IEnumerable<T>
    {
        private readonly T[,] data;

        public Image2D(int width, int height)
        {
            this.Width = width;
            this.Height = height;
            this.data = new T[width, height];
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public Size Size
        {
            get { return new Size(this.Width, this.Height); }
        }

        public Rectangle Rectangle
        {
            get { return new Rectangle(0, 0, this.Width, this.Height); }
        }

        public T this[int i, int j]
        {
            get { return this.data[i, j]; }
            set { this.data[i, j] = value; }
        }

        public Image2D<T> Shrink(Rectangle takeWhat)
        {
            Debug.Assert(this.Rectangle.Contains(takeWhat));

            Image2D<T> result = new Image2D<T>(takeWhat.Width, takeWhat.Height);

            for (int i = takeWhat.Left; i < takeWhat.Right; ++i)
                for (int j = takeWhat.Top; j < takeWhat.Bottom; ++j)
                    result[i - takeWhat.Left, j - takeWhat.Top] = this.data[i, j];

            return result;
        }

        public static int DifferentValueCount(Image2D<T> mask1, Image2D<T> mask2)
        {
            Debug.Assert(mask1.Width == mask2.Width);
            Debug.Assert(mask1.Height == mask2.Height);

            int count = 0;
            for (int i = 0; i < mask1.Width; ++i)
            {
                for (int j = 0; j < mask1.Height; ++j)
                {
                    if (!Equals(mask1[i, j], mask2[i, j]))
                        ++count;
                }
            }

            return count;
        }

        public Image2D<T> Clone()
        {
            Image2D<T> result = new Image2D<T>(this.Width, this.Height);
            for (int i = 0; i < this.Width; ++i)
                for (int j = 0; j < this.Height; ++j)
                    result[i, j] = this.data[i, j];
            return result;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < this.Width; ++i)
                for (int j = 0; j < this.Height; ++j)
                    yield return this.data[i, j];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
