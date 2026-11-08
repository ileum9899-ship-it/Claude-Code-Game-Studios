using UnityEngine;

namespace CityGame.Runtime
{
    /// <summary>
    /// Starts the game in any scene: pressing Play in an empty scene is enough.
    /// Skipped when the scene already contains a <see cref="CityGameController"/>.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindAnyObjectByType<CityGameController>() != null) return;
            new GameObject("CityGame").AddComponent<CityGameController>();
        }
    }
}
