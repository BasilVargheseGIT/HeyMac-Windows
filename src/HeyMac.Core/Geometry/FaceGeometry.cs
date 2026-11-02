using System.Drawing;
using System.Drawing.Imaging;

namespace HeyMac.Core.Geometry;

/// <summary>
/// Port of HeyMac's FaceGeometry (Swift).
///
/// - ArcFace 112x112 landmark template (InsightFace canonical).
/// - Least-squares similarity transform (Umeyama via complex-number reduction).
/// - Bilinear warp that maps detected landmarks onto the template.
/// - MiniFASNet anti-spoof crop (2.7x box, clamped/shifted into frame).
/// - cv2-style bilinear resize with half-pixel-center mapping.
/// </summary>
public static class FaceGeometry
{
    /// InsightFace's canonical 112x112 ArcFace landmark template.
    public static readonly IReadOnlyList<PointF> ArcFaceTemplate = new List<PointF>
    {
        new(38.2946f, 51.6963f),
        new(73.5318f, 51.5014f),
        new(56.0252f, 71.7366f),
        new(41.5493f, 92.3655f),
        new(70.7299f, 92.2041f),
    };

    /// <summary>
    /// Least-squares similarity transform mapping `source` onto `destination`.
    /// Uniform scale + rotation + translation. Treats 2D points as complex numbers:
    /// z = sum(conj(p) * q) / sum(|p|^2) on mean-centered points.
    /// Returns a 3x2 affine matrix: [[a, -b], [b, a], [tx, ty]] applied as
    /// x' = a*x - b*y + tx, y' = b*x + a*y + ty.
    /// </summary>
    public static float[,] SimilarityTransform(IReadOnlyList<PointF> source, IReadOnlyList<PointF> destination)
    {
        if (source.Count != destination.Count || source.Count == 0)
            throw new ArgumentException("point sets must match and be non-empty");

        int n = source.Count;
        float sx = 0, sy = 0, dx = 0, dy = 0;
        for (int i = 0; i < n; i++)
        {
            sx += source[i].X; sy += source[i].Y;
            dx += destination[i].X; dy += destination[i].Y;
        }
        float smx = sx / n, smy = sy / n;
        float dmx = dx / n, dmy = dy / n;

        float numRe = 0, numIm = 0, denom = 0;
        for (int i = 0; i < n; i++)
        {
            float px = source[i].X - smx, py = source[i].Y - smy;
            float qx = destination[i].X - dmx, qy = destination[i].Y - dmy;
            numRe += px * qx + py * qy;
            numIm += px * qy - py * qx;
            denom += px * px + py * py;
        }

        denom = Math.Max(denom, 1e-9f);
        float a = numRe / denom, b = numIm / denom;
        float tx = dmx - (a * smx - b * smy);
        float ty = dmy - (b * smx + a * smy);

        return new[,] { { a, -b }, { b, a }, { tx, ty } };
    }

    /// <summary>Inverts a similarity transform matrix [[a,-b],[b,a],[tx,ty]].</summary>
    public static float[,] Invert(float[,] m)
    {
        float a = m[0, 0], b = m[1, 0], tx = m[2, 0], ty = m[2, 1];
        float det = a * a + b * b;
        if (Math.Abs(det) < 1e-9f) det = 1e-9f;
        float invA = a / det, invB = -b / det;
        float invTx = -(invA * tx + invB * ty); // careful: inverse translation
        // inverse of [a -b; b a] is [a b; -b a]/det ; translation part = -R^-1 * t
        float itx = -(a * tx - (-b) * ty) / det; // = -(a*tx + b*ty)/det
        float ity = -((-b) * tx + a * ty) / det; // = -(-b*tx + a*ty)/det
        return new[,] { { invA, invB }, { -invB, invA }, { itx, ity } };
    }

    /// <summary>Applies a 3x2 similarity matrix to a point.</summary>
    public static PointF Apply(float[,] m, PointF p)
    {
        return new PointF(m[0, 0] * p.X + m[0, 1] * p.Y + m[2, 0],
                          m[1, 0] * p.X + m[1, 1] * p.Y + m[2, 1]);
    }

    /// <summary>
    /// Warps `frame` so `landmarks` land on the ArcFace template, producing the aligned
    /// 112x112 crop the embedding model was trained on. Bilinear sampling, edge-clamped.
    /// </summary>
    public static Bitmap AlignFace(Bitmap frame, IReadOnlyList<PointF> landmarks, int size = 112)
    {
        float[,] forward = SimilarityTransform(landmarks, ArcFaceTemplate);
        float[,] inverse = Invert(forward);

        Bitmap output = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(output);
        g.Clear(Color.White);

        // Lock bits for direct pixel access (matches Swift's byte buffer approach).
        var srcData = frame.LockBits(new Rectangle(0, 0, frame.Width, frame.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, frame.PixelFormat);
        var dstData = output.LockBits(new Rectangle(0, 0, size, size),
            System.Drawing.Imaging.ImageLockMode.WriteOnly, output.PixelFormat);

        unsafe
        {
            byte* src = (byte*)srcData.Scan0;
            byte* dst = (byte*)dstData.Scan0;
            int srcStride = srcData.Stride;
            int dstStride = dstData.Stride;
            int maxX = frame.Width - 1, maxY = frame.Height - 1;

            for (int oy = 0; oy < size; oy++)
            {
                for (int ox = 0; ox < size; ox++)
                {
                    PointF p = Apply(inverse, new PointF(ox, oy));
                    float cx = Math.Clamp(p.X, 0, maxX);
                    float cy = Math.Clamp(p.Y, 0, maxY);
                    int x0 = (int)Math.Floor(cx), y0 = (int)Math.Floor(cy);
                    int x1 = Math.Min(x0 + 1, maxX), y1 = Math.Min(y0 + 1, maxY);
                    float fx = cx - x0, fy = cy - y0;

                    for (int c = 0; c < 3; c++)
                    {
                        float p00 = src[(y0 * srcStride) + x0 * 3 + c];
                        float p10 = src[(y0 * srcStride) + x1 * 3 + c];
                        float p01 = src[(y1 * srcStride) + x0 * 3 + c];
                        float p11 = src[(y1 * srcStride) + x1 * 3 + c];
                        float top = p00 + (p10 - p00) * fx;
                        float bottom = p01 + (p11 - p01) * fx;
                        byte v = (byte)Math.Clamp(Math.Round(top + (bottom - top) * fy), 0, 255);
                        dst[(oy * dstStride) + ox * 3 + c] = v;
                    }
                    dst[(oy * dstStride) + ox * 3 + 3] = 255; // alpha
                }
            }
        }

        output.UnlockBits(dstData);
        frame.UnlockBits(srcData);
        return output;
    }

    /// <summary>
    /// Exact port of Silent-Face-Anti-Spoofing's CropImage._get_new_box:
    /// grow the face box by `scale` around its center, then shift (not truncate)
    /// it back inside the frame. Returns the inclusive pixel region.
    /// </summary>
    public static Rectangle AntiSpoofCropRect(Rectangle box, int imageWidth, int imageHeight, float scale)
    {
        float srcW = imageWidth, srcH = imageHeight;
        float boxW = Math.Max(box.Width, 1), boxH = Math.Max(box.Height, 1);
        float s = Math.Min((srcH - 1) / boxH, Math.Min((srcW - 1) / boxW, scale));
        float newW = boxW * s, newH = boxH * s;
        float centerX = box.Left + boxW / 2, centerY = box.Top + boxH / 2;

        float left = centerX - newW / 2, top = centerY - newH / 2;
        float right = centerX + newW / 2, bottom = centerY + newH / 2;

        if (left < 0) { right -= left; left = 0; }
        if (top < 0) { bottom -= top; top = 0; }
        if (right > srcW - 1) { left -= (right - srcW + 1); right = srcW - 1; }
        if (bottom > srcH - 1) { top -= (bottom - srcH + 1); bottom = srcH - 1; }

        int l = Math.Max(0, (int)left), t = Math.Max(0, (int)top);
        int r = Math.Min(imageWidth - 1, (int)right), b = Math.Min(imageHeight - 1, (int)bottom);
        return new Rectangle(l, t, Math.Max(1, r - l + 1), Math.Max(1, b - t + 1));
    }

    /// <summary>
    /// cv2.resize(INTER_LINEAR) of `rect` within `frame` to outWidth x outHeight
    /// using half-pixel-center mapping, no antialiasing.
    /// </summary>
    public static Bitmap Resize(Bitmap frame, Rectangle region, int outWidth, int outHeight)
    {
        float scaleX = region.Width / (float)outWidth;
        float scaleY = region.Height / (float)outHeight;

        Bitmap output = new Bitmap(outWidth, outHeight, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        var srcData = frame.LockBits(new Rectangle(0, 0, frame.Width, frame.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, frame.PixelFormat);
        var dstData = output.LockBits(new Rectangle(0, 0, outWidth, outHeight),
            System.Drawing.Imaging.ImageLockMode.WriteOnly, output.PixelFormat);

        unsafe
        {
            byte* src = (byte*)srcData.Scan0;
            byte* dst = (byte*)dstData.Scan0;
            int srcStride = srcData.Stride;
            int dstStride = dstData.Stride;

            float rMinX = region.Left, rMaxX = region.Right - 1;
            float rMinY = region.Top, rMaxY = region.Bottom - 1;

            for (int oy = 0; oy < outHeight; oy++)
            {
                float sy = rMinY + (oy + 0.5f) * scaleY - 0.5f;
                for (int ox = 0; ox < outWidth; ox++)
                {
                    float sx = rMinX + (ox + 0.5f) * scaleX - 0.5f;
                    sx = Math.Clamp(sx, rMinX, rMaxX);
                    sy = Math.Clamp(sy, rMinY, rMaxY);
                    int x0 = (int)Math.Floor(sx), y0 = (int)Math.Floor(sy);
                    int x1 = Math.Min(x0 + 1, frame.Width - 1), y1 = Math.Min(y0 + 1, frame.Height - 1);
                    float fx = sx - x0, fy = sy - y0;

                    for (int c = 0; c < 3; c++)
                    {
                        float p00 = src[(y0 * srcStride) + x0 * 3 + c];
                        float p10 = src[(y0 * srcStride) + x1 * 3 + c];
                        float p01 = src[(y1 * srcStride) + x0 * 3 + c];
                        float p11 = src[(y1 * srcStride) + x1 * 3 + c];
                        float top = p00 + (p10 - p00) * fx;
                        float bottom = p01 + (p11 - p01) * fx;
                        dst[(oy * dstStride) + ox * 3 + c] = (byte)Math.Clamp(Math.Round(top + (bottom - top) * fy), 0, 255);
                    }
                    dst[(oy * dstStride) + ox * 3 + 3] = 255;
                }
            }
        }

        output.UnlockBits(dstData);
        frame.UnlockBits(srcData);
        return output;
    }
}
