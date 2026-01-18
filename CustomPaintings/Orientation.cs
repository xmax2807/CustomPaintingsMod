using System;

namespace CustomPaintings
{
    [Flags]
    public enum Orientation
    {
        None = 0,
        Landscape = 1 << 0,
        Portrait = 1 << 1,
        Square = 1 << 2,
        All = Landscape | Portrait | Square
    }

    public static class OrientationHelper
    {
        public static Orientation GetOrientation(this ImageFileNode node)
        {
            float aspectRatio = (float)node.Width / node.Height;
            if (aspectRatio > 1.3f) return Orientation.Landscape;
            if (aspectRatio < 6f / 7f) return Orientation.Portrait;
            return Orientation.Square;
        }
    }
}