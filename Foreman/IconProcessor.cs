﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Foreman
{
    public struct IconInfo
    {
        public string iconPath;
        public int iconSize;
        public double iconScale;
        public Point iconOffset;
        public Color iconTint;

        public IconInfo(string iconPath, int iconSize)
        {
            this.iconPath = iconPath;
            this.iconSize = iconSize;
            this.iconScale = iconSize > 0? 32/iconSize : 1;
            this.iconOffset = new Point(0, 0);
            iconTint = IconProcessor.NoTint;
        }

        public void SetIconTint(double a, double r, double g, double b)
        {
            a = (a <= 1 ? a * 255 : a);
            r = (r <= 1 ? r * 255 : r);
            g = (g <= 1 ? g * 255 : g);
            b = (b <= 1 ? b * 255 : b);
            iconTint = Color.FromArgb((int)a, (int)r, (int)g, (int)b);
        }
    }

    public static class IconProcessor
    {
        internal static readonly Color NoTint = Color.FromArgb(255, 0, 0, 0);
        internal static readonly int IconCanvasSize = 64;

        private static Bitmap unknownIcon;
        public static Bitmap GetUnknownIcon()
        {
            if (unknownIcon == null)
            {

                unknownIcon = LoadImage("UnknownIcon.png");
                if (unknownIcon == null)
                {
                    unknownIcon = new Bitmap(32, 32);
                    using (Graphics g = Graphics.FromImage(unknownIcon))
                    {
                        g.FillRectangle(Brushes.White, 0, 0, 32, 32);
                    }
                }
            }
            return unknownIcon;
        }

        public static Bitmap GetIcon(IconInfo iinfo, List<IconInfo> iinfos)
        {
            if (iinfos == null)
                iinfos = new List<IconInfo>();
            int mainIconSize = iinfo.iconSize > 0 ? iinfo.iconSize : 32;
            double IconCanvasScale = (double)IconCanvasSize / mainIconSize;
            //if (iinfos.Count > 0 && iinfos[0].iconSize > 0 && iinfos[0].iconScale == 0) mainIconSize = iinfos[0].iconSize;

            if(iinfos.Count == 0) //if there are no icons, use the single icon
                iinfos.Add(iinfo);

            //quick check to ensure it isnt a null icon
            bool empty = true;
            foreach(IconInfo ii in iinfos)
            {
                if (!string.IsNullOrEmpty(ii.iconPath))
                    empty = false;
            }
            if (empty)
                return null;

            Bitmap icon = new Bitmap(IconCanvasSize, IconCanvasSize, PixelFormat.Format32bppArgb);
            //using(Graphics g = Graphics.FromImage(icon)) { g.FillRectangle(Brushes.Gray, new Rectangle(0, 0, icon.Width, icon.Height)); }
            foreach (IconInfo ii in iinfos)
            {
                //load the image and prep it for processing
                int iconSize = ii.iconSize > 0 ? ii.iconSize : iinfo.iconSize;
                int iconDrawSize = (int)(iconSize * (ii.iconScale > 0 ? ii.iconScale : (double)mainIconSize / iconSize));
                iconDrawSize = (int)(iconDrawSize * IconCanvasScale);

                Bitmap iconImage = LoadImage(ii.iconPath, iconDrawSize);

                //apply tint (if necessary)
                //NOTE: tint is applied as pre-multiplied alpha, so: A(result) = A(original); RGB(result) = RGB(tint) + RGB(original) * (255 - A(tint))
                if (ii.iconTint != NoTint)
                {
                    BitmapData bmpData = iconImage.LockBits(new Rectangle(0, 0, iconImage.Width, iconImage.Height), ImageLockMode.ReadWrite, iconImage.PixelFormat);
                    int bytesPerPixel = Bitmap.GetPixelFormatSize(iconImage.PixelFormat) / 8;
                    int byteCount = bmpData.Stride * iconImage.Height;
                    byte[] pixels = new byte[byteCount];
                    IntPtr ptrFirstPixel = bmpData.Scan0;
                    Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);
                    int heightInPixels = bmpData.Height;
                    int widthInBytes = bmpData.Width * bytesPerPixel;

                    for (int y = 0; y < heightInPixels; y++)
                    {
                        int currentLine = y * bmpData.Stride;
                        for (int x = 0; x < widthInBytes; x = x + bytesPerPixel)
                        {
                            int pixelA = pixels[currentLine + x + 3];
                            if (pixelA > 0)
                            {
                                // calculate new pixel value
                                pixels[currentLine + x] = (byte)Math.Min((int)ii.iconTint.B + (pixelA * (255 - ii.iconTint.A) * pixels[currentLine + x] / 65025), 255);
                                pixels[currentLine + x + 1] = (byte)Math.Min((int)ii.iconTint.G + (pixelA * (255 - ii.iconTint.A) * pixels[currentLine + x + 1] / 65025), 255);
                                pixels[currentLine + x + 2] = (byte)Math.Min((int)ii.iconTint.R + (pixelA * (255 - ii.iconTint.A) * pixels[currentLine + x + 2] / 65025), 255);
                            }
                        }
                    }
                    // copy modified bytes back
                    Marshal.Copy(pixels, 0, ptrFirstPixel, pixels.Length);
                    iconImage.UnlockBits(bmpData);
                }

                using(Graphics g = Graphics.FromImage(icon))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    //prepare drawing zone
                    Rectangle iconBorder = new Rectangle(
                        (IconCanvasSize / 2) - (iconDrawSize / 2) + ii.iconOffset.X,
                        (IconCanvasSize / 2) - (iconDrawSize / 2) + ii.iconOffset.Y,
                        iconDrawSize,
                        iconDrawSize);
                    g.DrawImage(iconImage, iconBorder);
                }
            }
            return icon;
        }

        private static Bitmap LoadImage(String fileName, int resultSize = 32)
        {
            if (String.IsNullOrEmpty(fileName))
            { return null; }

            string fullPath = "";
            if (File.Exists(fileName))
            {
                fullPath = fileName;
            }
            else if (File.Exists(Application.StartupPath + "\\" + fileName))
            {
                fullPath = Application.StartupPath + "\\" + fileName;
            }
            else
            {
                string[] splitPath = fileName.Split('/');
                splitPath[0] = splitPath[0].Trim('_');

                if (DataCache.Mods.Any(m => m.Name == splitPath[0]))
                {
                    fullPath = DataCache.Mods.First(m => m.Name == splitPath[0]).dir;
                }

                if (!String.IsNullOrEmpty(fullPath))
                {
                    for (int i = 1; i < splitPath.Count(); i++) //Skip the first split section because it's the mod name, not a directory
                    {
                        fullPath = Path.Combine(fullPath, splitPath[i]);
                    }
                }
            }

            try
            {
                using (Bitmap image = new Bitmap(fullPath)) //If you don't do this, the file is locked for the lifetime of the bitmap
                {
                    Bitmap bmp = new Bitmap(resultSize,resultSize);
                    using (Graphics g = Graphics.FromImage(bmp))
                        g.DrawImage(image, new Rectangle(0, 0, (resultSize * image.Width / image.Height), resultSize));
                    return bmp;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}