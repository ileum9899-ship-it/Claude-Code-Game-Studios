using UnityEngine;

namespace NeonHarbor.Runtime
{
    /// <summary>Starts Neon Harbor in any scene (press Play in an empty scene). Skipped when a HarborGame already exists.</summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindAnyObjectByType<HarborGame>() != null) return;
            new GameObject("NeonHarbor").AddComponent<HarborGame>();
        }
    }
}
