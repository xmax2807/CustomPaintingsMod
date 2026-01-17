using System;
using System.Collections.Generic;
using System.IO;

namespace CustomPaintings
{
    public enum ShapeType : byte
    {
        Landscape,
        Square,
        Portrait
    }

    [Flags]
    public enum DisplayMode : byte
    {
        None           = 0,
        Normal         = 1 << 0,
        RugsAndBanners = 1 << 1,
        Chaos          = 1 << 2
    }

    public readonly struct PaintingData
    {
        public readonly ShapeType Shape;
        public readonly DisplayMode Mode;
        public readonly string Name;

        public PaintingData(string name, ShapeType shape, DisplayMode mode)
        {
            Name = name;
            Shape = shape;
            Mode = mode;
        }

        public override int GetHashCode() => Name.GetHashCode();
    }

    public static class PaintingDataReader
    {
        public static void Read(string path, in List<PaintingData> result)
        {
            result.Clear();
            var lines = File.ReadAllLines(path);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',');
                if (parts.Length < 3) continue;

                var name = parts[0].Trim();
                var shape = Enum.Parse<ShapeType>(parts[1].Trim());

                var modes = DisplayMode.None;
                for (int i = 2; i < parts.Length; i++)
                {
                    var trimmed = parts[i].Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {   
                        modes |= Enum.Parse<DisplayMode>(trimmed);
                    }
                }

                result.Add(new PaintingData(name, shape, modes));
            }
        }

        public static Dictionary<string, PaintingData> Read(string path)
        {
            using var pool = UnityEngine.Pool.ListPool<PaintingData>.Get(out var list);

            Read(path, list);

            var result = new Dictionary<string, PaintingData>(list.Count, StringComparer.OrdinalIgnoreCase);

            for (int i = list.Count - 1; i >= 0; --i)
            {
                result.Add(list[i].Name, list[i]);
            }

            return result;
        }
    }
}