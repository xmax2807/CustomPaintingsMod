using System.Collections.Generic;
using TMPro;


namespace CustomPaintings
{
    public class CP_GroupList
    {
        private readonly IReadOnlyDictionary<string,PaintingData> m_dataSource;
        private CP_Logger m_logger;

        public CP_GroupList(CP_Logger logger, IReadOnlyDictionary<string, PaintingData> dataSource)
        {
            m_dataSource = dataSource ?? throw new System.ArgumentNullException(nameof(dataSource));
            m_logger = logger;

            m_logger.LogInfo($"CP_GroupList initialized with {m_dataSource.Count} entries.");
        }

        internal bool HasDisplayMode(string matName, DisplayMode displayMode)
        {
            if(m_dataSource.TryGetValue(matName, out PaintingData paintingData))
            {
                m_logger.LogInfo($"HasDisplayMode: {matName} {paintingData.Mode} {displayMode}");
                return paintingData.Mode.HasFlag(displayMode);
            }
            return false;
        }

        internal bool IsShape(string matName, ShapeType shapeType)
        {
            if (m_dataSource.TryGetValue(matName, out PaintingData paintingData))
            {
                return paintingData.Shape == shapeType;
            }
            return false;
        }

        internal bool TryGetShapeOf(string matName, out ShapeType shape)
        {
            if (m_dataSource.TryGetValue(matName, out PaintingData paintingData))
            {
                shape = paintingData.Shape;
                return true;
            }
            shape = default;
            return false;
        }
    }
}
