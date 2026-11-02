using System.Drawing;
using System.Drawing.Imaging;

namespace HeyMac.Core.Tensors;

/// <summary>
/// Converts a System.Drawing.Bitmap into an ONNX DenseTensor<float>.
///
/// Supports the two layouts HeyMac's models actually consume:
///   * NCHW  [1, C, H, W]  — typical for exported ArcFace / MiniFASNet ONNX
///   * NHWC  [1, H, W, C]
///
/// Channel order and normalization are controlled by the caller so the port can
/// reproduce the Core ML preprocessing exactly (RGB + (x-127.5)/127.5 for ArcFace;
/// BGR raw 0-255 for MiniFASNet).
/// </summary>
public static class TensorHelper
{
    public enum Layout { NCHW, NHWC }

    /// <summary>
    /// Builds a DenseTensor from a bitmap. `channelOrder` is a length-3 permutation
    /// of {0,1,2} selecting which source channel goes into each tensor channel
    /// (e.g. {2,1,0} = BGR from an RGB bitmap). `normalize` applies (x - 127.5)/127.5.
    /// </summary>
    public static Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float> FromBitmap(
        Bitmap bitmap, Layout layout, int[] channelOrder, bool normalize)
    {
        int width = bitmap.Width, height = bitmap.Height;
        int channels = 3;

        int[] shape = layout == Layout.NCHW
            ? new[] { 1, channels, height, width }
            : new[] { 1, height, width, channels };

        var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(shape);

        var bmp = bitmap;
        // Ensure a directly-lockable pixel format.
        if (bmp.PixelFormat != System.Drawing.Imaging.PixelFormat.Format24bppRgb &&
            bmp.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppRgb &&
            bmp.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
        {
            var converted = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(converted))
                g.DrawImage(bmp, 0, 0, width, height);
            bmp = converted;
        }

        var data = bmp.LockBits(new Rectangle(0, 0, width, height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);
        try
        {
            unsafe
            {
                byte* src = (byte*)data.Scan0;
                int stride = data.Stride;
                int bytesPerPixel = Image.GetPixelFormatSize(bmp.PixelFormat) / 8;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        byte* px = src + y * stride + x * bytesPerPixel;
                        // GDI+ stores pixels as B,G,R (and possibly A).
                        byte[] sample = { px[0], px[1], px[2] }; // B, G, R
                        float[] channelsF = new float[3];
                        for (int c = 0; c < 3; c++)
                        {
                            float v = sample[c];
                            if (normalize) v = (v - 127.5f) / 127.5f;
                            channelsF[c] = v;
                        }

                        if (layout == Layout.NCHW)
                        {
                            for (int c = 0; c < 3; c++)
                                tensor[0, c, y, x] = channelsF[channelOrder[c]];
                        }
                        else
                        {
                            for (int c = 0; c < 3; c++)
                                tensor[0, y, x, c] = channelsF[channelOrder[c]];
                        }
                    }
                }
            }
        }
        finally
        {
            bmp.UnlockBits(data);
            if (bmp != bitmap) bmp.Dispose();
        }

        return tensor;
    }
}
