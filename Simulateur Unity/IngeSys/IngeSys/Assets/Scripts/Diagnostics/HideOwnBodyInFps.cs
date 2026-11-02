using UnityEngine;

namespace IngeSys.Diagnostics
{
    /// <summary>Hide the CAD casing only while rendering the onboard camera inside it.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class HideOwnBodyInFps : MonoBehaviour
    {
        public Renderer[] bodyRenderers;
        bool[] previous;
        bool hidden;

        void OnPreCull()
        {
            Restore();
            if (bodyRenderers == null) return;
            previous = new bool[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (!bodyRenderers[i]) continue;
                previous[i] = bodyRenderers[i].forceRenderingOff;
                bodyRenderers[i].forceRenderingOff = true;
            }
            hidden = true;
        }

        void OnPostRender() { Restore(); }
        void OnDisable() { Restore(); }

        void Restore()
        {
            if (!hidden) return;
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i]) bodyRenderers[i].forceRenderingOff = previous[i];
            hidden = false;
        }
    }
}
