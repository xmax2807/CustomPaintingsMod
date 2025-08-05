using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;
using UnityEngine;
using System.Reflection;

namespace CustomPaintings
{
    public class CP_GifVidManager
    {
        private readonly CP_Logger logger;
        public CP_GifVidManager(CP_Logger logger)
        {
            this.logger = logger;
            logger.LogInfo("CP_GifVidManager initialized.");
        }

    }
}
