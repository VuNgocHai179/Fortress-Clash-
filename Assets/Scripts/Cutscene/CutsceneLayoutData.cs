using System;
using UnityEngine;

namespace FortressClash.Cutscene
{
    /// <summary>A rectangle in slide pixels, measured from the top-left of the slide image.</summary>
    [Serializable]
    public class CutsceneRect
    {
        public int x;
        public int y;
        public int w;
        public int h;

        public Rect ToRect() => new Rect(x, y, w, h);
    }

    /// <summary>One comic panel on a page, plus the parchment caption cards printed on it.</summary>
    [Serializable]
    public class CutscenePanelLayout
    {
        public int x;
        public int y;
        public int w;
        public int h;
        public CutsceneRect[] captions = Array.Empty<CutsceneRect>();
    }

    /// <summary>The pop-up layout of one page: which pieces lift off the paper, in reading order.</summary>
    [Serializable]
    public class CutscenePageLayout
    {
        public string slide;
        public int w;
        public int h;
        public CutscenePanelLayout[] panels = Array.Empty<CutscenePanelLayout>();
    }

    [Serializable]
    public class CutsceneLayoutDocument
    {
        public CutscenePageLayout[] pages = Array.Empty<CutscenePageLayout>();
    }

    /// <summary>
    /// Loads <c>Assets/Resources/CutsceneLayout.json</c>, produced by the detection pass over the
    /// slide artwork.  Returned object is <c>null</c> when the asset is missing or unreadable, and
    /// callers treat that as "cannot play the cutscene" rather than throwing.
    /// </summary>
    public static class CutsceneLayoutData
    {
        public const string ResourcePath = "CutsceneLayout";

        private static CutsceneLayoutDocument _cached;

        public static CutsceneLayoutDocument Load()
        {
            if (_cached != null)
            {
                return _cached;
            }

            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError($"[Cutscene] Missing Resources/{ResourcePath}.json - the pop-up layout could not be loaded.");
                return null;
            }

            try
            {
                _cached = JsonUtility.FromJson<CutsceneLayoutDocument>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Cutscene] Could not parse Resources/{ResourcePath}.json: {e.Message}");
                return null;
            }

            if (_cached == null || _cached.pages == null || _cached.pages.Length == 0)
            {
                Debug.LogError($"[Cutscene] Resources/{ResourcePath}.json contains no pages.");
                return null;
            }

            return _cached;
        }
    }
}
