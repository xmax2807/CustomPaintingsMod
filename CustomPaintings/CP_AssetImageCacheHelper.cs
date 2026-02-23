using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace CustomPaintings;

public static class CP_AssetImageCacheHelper
{
    private const int MaxSide = 1024;
    private const int HeaderSize = 12;
    public static string GetFileCache(string fullPath)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();

        byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(fullPath));
        var fileName = BitConverter.ToString(hashBytes).Replace("-", string.Empty);

        string folderName = Path.Combine(BepInEx.Paths.CachePath, "CustomPaintings");
        Directory.CreateDirectory(folderName);

        return Path.Combine(folderName, $"{fileName}.raw");
    }

    public static bool TryLoadFromCache(string cachePath, out Texture2D texture)
    {
        texture = null;
        if (!File.Exists(cachePath)) return false;

        try
        {
            using var fs = File.OpenRead(cachePath);
            Span<byte> header = stackalloc byte[HeaderSize];
            fs.Read(header);

            int width = BitConverter.ToInt32(header.Slice(0, 4));
            int height = BitConverter.ToInt32(header.Slice(4, 4));
            TextureFormat format = (TextureFormat)BitConverter.ToInt32(header.Slice(8, 4));

            int rawSize = (int)(fs.Length - HeaderSize);
            using var rawData = new NativeArray<byte>(rawSize, Allocator.Temp);
            int read = fs.Read(rawData.AsSpan());
            if (read != rawSize) throw new EndOfStreamException();

            texture = new Texture2D(width, height, format, mipChain: false);
            texture.LoadRawTextureData(rawData);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return true;
        }
        catch
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            texture = null;
            File.Delete(cachePath);
            return false;
        }
    }

    public async static UniTaskVoid SaveToCache(string originalImagePath, int width, int height, bool hasAlpha = false)
    {
        string cachePath = GetFileCache(originalImagePath);
        if (File.Exists(cachePath)) return;

        var (targetW, targetH) = ComputeTargetSize(width, height);

        // Load raw bytes and decode into Texture2D
        var request = UnityWebRequestTexture.GetTexture($"file://{originalImagePath}", nonReadable: true);
        request.timeout = 15;
        await request.SendWebRequest();

        var sourceFormat = hasAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24;
        if(request.error != null)
        {
            request.Dispose();
            throw new Exception(request.error);
        }
        var source = DownloadHandlerTexture.GetContent(request);
        request.Dispose();

        // Blit to resize on GPU
        var rt = RenderTexture.GetTemporary(targetW, targetH, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        UnityEngine.Object.DestroyImmediate(source);
        source = null;

        // Read back resized pixels
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var resized = new Texture2D(targetW, targetH, sourceFormat, mipChain: false);
        resized.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0, recalculateMipMaps: false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        // Compress and finalize
        resized.Compress(highQuality: false);
        resized.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        // Write header + raw data
        using var fs = new FileStream(cachePath, FileMode.Create);
        Span<byte> header = stackalloc byte[HeaderSize];
        BitConverter.TryWriteBytes(header.Slice(0, 4), resized.width);
        BitConverter.TryWriteBytes(header.Slice(4, 4), resized.height);
        BitConverter.TryWriteBytes(header.Slice(8, 4), (int)resized.format);
        NativeArray<byte> rawData = resized.GetRawTextureData<byte>();
        
        fs.Write(header);
        fs.Write(rawData.AsReadOnlySpan());

        UnityEngine.Object.DestroyImmediate(resized);
        resized = null;
    }

    private static (int width, int height) ComputeTargetSize(int srcWidth, int srcHeight)
    {
        float scale = 1f;
        if (srcWidth > MaxSide || srcHeight > MaxSide)
        {
            scale = MaxSide / (float)Mathf.Max(srcWidth, srcHeight);
        }
        // align to 4 for DXT block compression
        int w = (int)(srcWidth * scale) & ~3;
        int h = (int)(srcHeight * scale) & ~3;
        return (w, h);
    }
}