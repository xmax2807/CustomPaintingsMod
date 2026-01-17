using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CustomPaintings
{
    public class Node<T>
    {
        public T Value;
        private readonly Node<T>[] m_children;
        public bool HasChildren => m_children is not null && m_children.Length > 0;
        public int ChildCount => m_children is null ? 0 : m_children.Length;
        public Node<T> this [int index]
        {
            get
            {
                if (m_children is null) return null;
                return m_children[index];
            }
        }

        public Node(T value) => Value = value;

        public Node(T value, params Node<T>[] children)
        {
            Value = value;
            m_children = children;
        }

        public Node(T value, List<Node<T>> children) : this(value, children.ToArray()){}

        public override string ToString()
        {
            string valStr = Value.ToString();

            if (!HasChildren)
            {
                return valStr;
            }

            string childStr = string.Join(", ", m_children.Select(x => x.ToString()));
            return $"{valStr} with {m_children.Length} children ({childStr})";
        }
    }

    public static class NodeExtensions
    {
        public static int GetTotalLeafCount(this Node<string> node)
        {
            int count = 0;
            if (node.HasChildren)
            {
                for (int i = 0; i < node.ChildCount; ++i)
                {
                    count += node[i].GetTotalLeafCount();
                }
            }
            else
            {
                count = 1;
            }
            return count;
        }

        public static void GetChildrenAtDepth(this Node<string> node, int depth, in List<Node<string>> result)
        {
            while(depth > 0 && node.HasChildren)
            {
                for(int i = 0; i < node.ChildCount; ++i)
                {
                    node[i].GetChildrenAtDepth(depth - 1, result);
                }
            }
        }

        public static void BuildPath(this Node<string> node, in StringBuilder sb, in List<string> result, int maxSize)
        {
            sb.Append(node.Value);
            if (node.HasChildren)
            {
                string currentPath = sb.ToString();
                for(int i = 0; result.Count < maxSize && i < node.ChildCount; ++i)
                {
                    sb.Append('/');
                    node[i].BuildPath(sb, result, maxSize);
                    sb.Clear();
                    sb.Append(currentPath);
                }
            }
            else
            {
                result.Add(sb.ToString());
            }
        }
    }
}