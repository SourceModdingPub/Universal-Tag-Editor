using System.Windows.Forms;
using TagTool.Cache;
using TagTool.Tags.Definitions;

namespace Sentinel.Forms
{
    internal class SoundControl
    {
        private GameCache cache;
        private CachedTag tag;
        private Sound definition;

        public SoundControl(GameCache cache, CachedTag tag, Sound definition)
        {
            this.cache = cache;
            this.tag = tag;
            this.definition = definition;
        }

        public DockStyle Dock { get; set; }
    }
}