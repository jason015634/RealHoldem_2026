using UnityEngine;
using UnityEngine.SceneManagement;

public static class SampleLobbyGameFlowBootstrap
{
    private const string LobbySceneName = "SampleLobbyScene";
    private const string GameSceneName = "SampleGameScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureSceneController(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSceneController(scene);
    }

    private static void EnsureSceneController(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        if (scene.name == LobbySceneName && UnityEngine.Object.FindObjectOfType<LobbyRoomListController>(true) == null)
        {
            new GameObject(nameof(LobbyRoomListController)).AddComponent<LobbyRoomListController>();
        }
        else if (scene.name == GameSceneName && UnityEngine.Object.FindObjectOfType<SampleGameRoomEntryLogger>(true) == null)
        {
            new GameObject(nameof(SampleGameRoomEntryLogger)).AddComponent<SampleGameRoomEntryLogger>();
        }
    }
}
