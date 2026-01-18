using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.FileType;
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
        readonly ImagePathHierarchy m_hierarchy;

        readonly List<Request> m_requests = new();
        readonly Action<Func<IEnumerator>> m_coroutineRunner;
        readonly Dictionary<string, Texture2D> m_cache = new();

        public CP_LoaderV2(CP_Logger logger, MaterialPropertyBlock propertyBlock, string rootPath, Action<Func<IEnumerator>> coroutineRunner)
        {
            m_propertyBlock = propertyBlock;
            m_logger = logger;
            m_hierarchy = new ImagePathHierarchy(rootPath);
            m_coroutineRunner = coroutineRunner;
            LoadAllImagePaths(m_hierarchy);
        }

        void LoadAllImagePaths(in ImagePathHierarchy hierarchy)
        {            

            foreach (string directoryPath in System.IO.Directory.EnumerateDirectories(hierarchy.RootPath))
            {

                var directoryName = Path.GetFileName(directoryPath);
                string validPath = hierarchy.GetValidPath(directoryName);
                if(!System.IO.Directory.Exists(validPath)) continue;

                using var __ = UnityEngine.Pool.ListPool<Node<string>>.Get(out var subNodeList);
                RetrieveAllFilePaths(validPath, subNodeList, CreateImageNode);

                if(subNodeList.Count > 0)
                {
                    hierarchy.AddNewPackage(directoryName, subNodeList);
                    m_logger.LogInfo($"Created package: {directoryName}");
                }
            }
        }

        private ImageFileNode CreateImageNode(string imagePath)
        {
            string extension = Path.GetExtension(imagePath).ToLower();

            if(extension != ".png" && extension != ".jpg" && extension != ".jpeg") return null;

            string fileName = Path.GetFileName(imagePath);
            var metaData = ImageMetadataReader.ReadMetadata(imagePath);
            var fileTypeDir = metaData.OfType<FileTypeDirectory>().First();
            string extensionString = fileTypeDir.GetString(FileTypeDirectory.TagExpectedFileNameExtension);
            
            switch (extensionString)
            {
                case "png":
                    var dir = metaData.OfType<PngDirectory>().First();
                    int width = dir.GetInt32(PngDirectory.TagImageWidth);
                    int height = dir.GetInt32(PngDirectory.TagImageHeight);
                    m_logger.LogInfo($"Extracted {width}x{height} from {fileName}");
                    return new ImageFileNode(fileName,width,height);
                case "jpg":
                case "jpeg":
                    var jpegDir = metaData.OfType<JpegDirectory>().First();
                    int jpegWidth = jpegDir.GetImageWidth();
                    int jpegHeight = jpegDir.GetImageHeight();
                    m_logger.LogInfo($"Extracted {jpegWidth}x{jpegHeight} from {fileName}");
                    return new ImageFileNode(fileName,jpegWidth,jpegHeight);
                default:
                    return null;
            }
        }

        private void RetrieveAllFilePaths(string folderPath, in List<Node<string>> nodeListResult, in Func<string, ImageFileNode> fileFactory)
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

        public void LogImage(Orientation orientation, int size, int startIndex = 0)
        {
            using var _ = UnityEngine.Pool.ListPool<string>.Get(out var paths);
            m_hierarchy.GetPaths(orientation, startIndex, size, paths);

            foreach (var path in paths)
            {
                m_logger.LogInfo(path);
            }
        }
        public void RequestLoadTexture(string fullPath, Renderer renderer, int matIndex)
        {
            if(m_cache.TryGetValue(fullPath, out Texture2D texture))
            {
                ApplyToRenderer(renderer, matIndex, texture);
            }
            else
            {
                string localPath = $"file://{fullPath}";
                var req = UnityWebRequestTexture.GetTexture(localPath);
                req.timeout = 15;
                m_requests.Add(new Request(req.SendWebRequest(), renderer, matIndex));
            }

            if(m_requests.Count > 0)
            {
                // m_logger.LogInfo($"Starting UpdateRenderers coroutine.");
                m_coroutineRunner(null);
            }
        }

        public void CleanUp()
        {
            foreach(var texture in m_cache.Values)
            {
                UnityEngine.Object.Destroy(texture);
            }
            m_cache.Clear();
            m_requests.Clear();
        }

        public bool UpdateRenderers()
        {
            if( m_requests.Count == 0) return false;
            int lastIndex = m_requests.Count - 1;

            for(int i = 0; i <= lastIndex; ++i)
            {
                var request = m_requests[i];

                var renderer = request.renderer;
                var matIndex = request.matIndex;
                var opt = request.opt;
                if (!opt.isDone)
                {
                    continue;
                }

                if (opt.webRequest.result == UnityWebRequest.Result.Success)
                {
                    m_logger.LogInfo($"Loaded {opt.webRequest.uri.LocalPath}");
                    Texture2D texture;
                    if (!m_cache.ContainsKey(opt.webRequest.uri.LocalPath))
                    {                    
                        texture = DownloadHandlerTexture.GetContent(opt.webRequest);
                        m_cache.Add(opt.webRequest.uri.LocalPath, texture);
                    }
                    else
                    {
                        texture = m_cache[opt.webRequest.uri.LocalPath];
                    }
                    ApplyToRenderer(renderer, matIndex, texture);
                }
                else
                {
                    m_logger.LogWarning($"Failed to load {opt.webRequest.uri.LocalPath}");
                }
                
                opt.webRequest.Dispose();
                
                //swap
                m_requests[i] = m_requests[lastIndex];
                m_requests.RemoveAt(lastIndex);
                --lastIndex;
            }
            return lastIndex >= 0;
        }

        private void ApplyToRenderer(Renderer renderer, int matIndex, Texture2D texture)
        {
            if(texture == null)
            {
                m_logger.LogError($"Failed to load texture for {renderer.name}");
                return;
            }
            renderer.GetPropertyBlock(m_propertyBlock, matIndex);
            m_propertyBlock.SetTexture("_MainTex", texture);
            renderer.SetPropertyBlock(m_propertyBlock, matIndex);
        }

        internal void RetrieveImagePaths(ShapeType shape, int startIndex, int size, in List<string> paths)
        {
            Orientation orientation = shape switch
            {
                ShapeType.Square => Orientation.Square,
                ShapeType.Landscape => Orientation.Landscape,
                ShapeType.Portrait => Orientation.Portrait,
                _ => Orientation.Square
            };
            m_hierarchy.GetPaths(orientation, startIndex, size, paths);
        }

        internal int GetTotalImageCount(ShapeType shape)
        {
            Orientation orientation = shape switch
            {
                ShapeType.Square => Orientation.Square,
                ShapeType.Landscape => Orientation.Landscape,
                ShapeType.Portrait => Orientation.Portrait,
                _ => Orientation.Square
            };
            return m_hierarchy.GetTotalImageCount(orientation);
        }

        private readonly struct Request
        {
            public readonly UnityWebRequestAsyncOperation opt;
            public readonly Renderer renderer;
            public readonly int matIndex;

            public Request(UnityWebRequestAsyncOperation opt, Renderer renderer, int matIndex)
            {
                this.opt = opt;
                this.renderer = renderer;
                this.matIndex = matIndex;
            }
        }
    }
}