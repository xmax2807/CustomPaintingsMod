using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using UnityEngine;
using UnityEngine.Networking;

namespace CustomPaintings
{
    public sealed class CP_LoaderV2
    {
        readonly MaterialPropertyBlock m_propertyBlock;
        readonly CP_Logger m_logger;

        readonly List<(UnityWebRequest, Renderer)> m_requests = new();
        private Node<string> m_pathHierarchy;

        public CP_LoaderV2(CP_Logger logger, MaterialPropertyBlock propertyBlock)
        {
            m_propertyBlock = propertyBlock;
            m_logger = logger;
        }

        public void LoadAllImagePaths(string root)
        {
            if (!System.IO.Directory.Exists(root))
            {
                m_logger.LogWarning($"Plugins directory not found: {root}");
                return;
            }

            using var _ = UnityEngine.Pool.ListPool<Node<string>>.Get(out var nodeList);
            

            foreach (string directoryPath in System.IO.Directory.EnumerateDirectories(root))
            {
                m_logger.LogInfo($"Loading files from: {directoryPath}");

                string validPath = Path.Combine(directoryPath, "CustomPaintings");
                if(!System.IO.Directory.Exists(validPath)) continue;

                var directoryName = Path.GetFileName(directoryPath);

                using var __ = UnityEngine.Pool.ListPool<Node<string>>.Get(out var subNodeList);
                RetrieveAllFilePaths(validPath, subNodeList, CreateImageNode);

                if(subNodeList.Count > 0)
                {
                    nodeList.Add(new Node<string>(Path.Combine(directoryName, "CustomPaintings"), subNodeList));
                }
            }

            m_pathHierarchy = new Node<string>(root, nodeList);
            m_logger.LogToFileOnly("DEBUG", $"Loaded Path hierarchy: {m_pathHierarchy}");
        }

        private Node<string> CreateImageNode(string imagePath)
        {
            string extension = Path.GetExtension(imagePath).ToLower();

            if(extension != ".png" && extension != ".jpg" && extension != ".jpeg") return null;

            string fileName = Path.GetFileName(imagePath);

            switch (extension)
            {
                case ".png":
                    var meta = PngMetadataReader.ReadMetadata(imagePath);
                    var dir = meta.OfType<PngDirectory>().First();
                    m_logger.LogInfo($"Extracted {dir.GetInt32(PngDirectory.TagImageWidth)}x{dir.GetInt32(PngDirectory.TagImageHeight)} from {fileName}");
                    return new Node<string>(fileName, new List<Node<string>>());
                case ".jpg":
                case ".jpeg":
                    var jpegMeta = JpegMetadataReader.ReadMetadata(imagePath);
                    var jpegDir = jpegMeta.OfType<JpegDirectory>().First();
                    m_logger.LogInfo($"Extracted {jpegDir.GetImageWidth()}x{jpegDir.GetImageHeight()} from {fileName}");
                    return new Node<string>(fileName, new List<Node<string>>());
                default:
                    return null;
            }
        }

        private void RetrieveAllFilePaths(string folderPath, in List<Node<string>> nodeListResult, in System.Func<string, Node<string>> fileFactory)
        {
            foreach(string path in System.IO.Directory.EnumerateFiles(folderPath))
            {
                if(System.IO.Directory.Exists(path))
                {
                    using var _ = UnityEngine.Pool.ListPool<Node<string>>.Get(out var nodeList);
                    RetrieveAllFilePaths(path, nodeList, fileFactory);
                    nodeListResult.Add(new Node<string>(path, nodeList));
                }
                else
                {
                    var imageNode = fileFactory(path);
                    if(imageNode is not null) nodeListResult.Add(imageNode);
                }
            }
        }

        public void RequestLoadTexture(Renderer renderer)
        {
        }
    }
}