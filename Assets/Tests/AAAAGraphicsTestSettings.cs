using UnityEngine;
using UnityEngine.TestTools.Graphics;

namespace Tests
{
    internal sealed class AAAAGraphicsTestSettings : GraphicsTestSettings
    {
        [Min(0)]
        public int WaitFrames;
    }
}