using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CustomPaintings
{
    public sealed class ImagePathHierarchy
    {
        public readonly string RootPath;
        readonly List<Package> m_packages = new();

        public ImagePathHierarchy(string rootPath)
        {
            if(Directory.Exists(rootPath) == false) throw new System.ArgumentException($"Path does not exist: {rootPath}");
            RootPath = rootPath;
        }

        public string GetValidPath(string packageName) => Path.Combine(RootPath, packageName, "CustomPaintings");

        public void AddNewPackage(string name, List<Node<string>> content)
        {
            if(m_packages.Exists(x => x.Name == name)) return;
            var newPackage = new Package(new Node<string>(name, content));
            m_packages.Add(newPackage);
        }

        public void GetPaths(Orientation orientation, int startIndex, int size, in List<string> result)
        {
            result.Clear();
            result.Capacity = System.Math.Max(result.Capacity, size);

            var sb = new StringBuilder();
            int skip = startIndex;
            int take = size;

            int totalCount = GetTotalImageCount(orientation);

            int maxIterations = Mathf.CeilToInt((float)(startIndex + size) / totalCount) * m_packages.Count;;
            int packageIndex = 0;
            
            int i = 0;
            for (;take > 0 && i < maxIterations; ++i)
            {
                var package = m_packages[packageIndex];
                int packageCount = package.GetCount(orientation);

                if (skip >= packageCount)
                {
                    skip -= packageCount;
                }
                else
                {
                    sb.Append(RootPath).Append('\\');
                    package.BuildFilteredPaths(orientation, ref skip, ref take, result, sb);
                    sb.Clear();
                }

                packageIndex = (packageIndex + 1) % m_packages.Count;
            }
        }

        public int GetTotalImageCount(Orientation orientation)
        {
            int count = 0;
            foreach (var package in m_packages)
            {
                count += package.GetCount(orientation);
            }
            return count;
        }

        private class Package
        {
            const string k_customPaintingsFolderName = "CustomPaintings";
            private readonly Node<string> m_root;
            private int m_landscapeCount, m_portraitCount, m_squareCount;

            public string Name => m_root.Value;
            
            public Package(Node<string> root)
            {
                // assert root is not null
                m_root = root;
                CacheOrientations(root);
            }

            private void CacheOrientations(Node<string> root)
            {
                if(root is null) return;
                if(root is ImageFileNode imageFileNode)
                {
                    switch(imageFileNode.GetOrientation())
                    {
                        case Orientation.Landscape: ++m_landscapeCount; break;
                        case Orientation.Portrait: ++m_portraitCount; break;
                        case Orientation.Square: ++m_squareCount; break;
                    }
                }

                int childCount = root.ChildCount;
                for(int i = 0; i < childCount; ++i)
                {
                    CacheOrientations(root[i]);
                }
            }

            public int GetCount(Orientation orientation)
            {
                int count = 0;
                if ((orientation & Orientation.Landscape) != 0) count += m_landscapeCount;
                if ((orientation & Orientation.Portrait) != 0) count += m_portraitCount;
                if ((orientation & Orientation.Square) != 0) count += m_squareCount;
                return count;
            }

            /// <param name="skip">Modified in-place, returns remaining skip after this package</param>
            /// <param name="take">Modified in-place, returns remaining take after this package</param>
            public void BuildFilteredPaths(
                Orientation orientation,
                ref int skip,
                ref int take,
                List<string> result,
                StringBuilder sb)
            {
                if (take <= 0) return;

                sb.Append(Path.Combine(m_root.Value, k_customPaintingsFolderName)).Append('\\');
                int lengthBefore = sb.Length;
                for(int i = 0; take > 0 && i < m_root.ChildCount; ++i)
                {
                    var node = m_root[i];
                    BuildFilteredPathsRecursive(node, orientation, ref skip, ref take, result, sb);
                    sb.Length = lengthBefore;
                }
            }

            private void BuildFilteredPathsRecursive(
                Node<string> node,
                Orientation orientation,
                ref int skip,
                ref int take,
                List<string> result,
                StringBuilder sb)
            {
                if (take <= 0) return;

                int lengthBefore = sb.Length;
                sb.Append(node.Value);

                if (node is ImageFileNode imageNode)
                {
                    if ((imageNode.GetOrientation() & orientation) != 0)
                    {
                        if (skip > 0)
                            --skip;
                        else
                        {
                            result.Add(sb.ToString());
                            --take;
                        }
                    }
                }
                else if (node.HasChildren)
                {
                    sb.Append('\\');
                    for (int i = 0; take > 0 && i < node.ChildCount; ++i)
                    {
                        BuildFilteredPathsRecursive(node[i], orientation, ref skip, ref take, result, sb);
                        sb.Length = lengthBefore + node.Value.Length + 1; // reset to parent path + '/'
                    }
                }

                sb.Length = lengthBefore;
            }
        }
    }

    public sealed class ImageFileNode : Node<string>
    {
        public readonly int Width, Height;
        public ImageFileNode(string value, int width, int height) : base(value)
        {
            Width = width;
            Height = height;
        }
    }
}