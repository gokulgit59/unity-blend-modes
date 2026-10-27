namespace BlendMode.Showcase
{
    // Station 1: the gallery wall. All 27 modes on the same two layers (Compositor).
    // The grid itself is a BlendGallery built into the station's canvas, so it is visible in Edit mode too.
    public class S01_GalleryStation : Station
    {
        public BlendGallery gallery;

        protected override void Build() { }

        protected override void OnRefresh()
        {
            if (gallery == null) return;
            gallery.blendOn = blendOn;
            gallery.SetOpacity(opacity);
        }
    }
}
