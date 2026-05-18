using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
// SampleScene에 포커 데모 실행에 필요한 루트 오브젝트와 핵심 컴포넌트를 자동 설치하는 에디터 유틸리티입니다.
public static class PokerDemoSceneInstaller
{
    private const string RootName = "PokerDemoRoot";
    private const string InstalledKey = "RealPoker2026.PokerDemo.SampleSceneInstalled";

    static PokerDemoSceneInstaller()
    {
        EditorApplication.delayCall += InstallIfNeeded;
    }

    [MenuItem("Tools/Poker Demo/Install In Current Scene")]
    public static void InstallMenu()
    {
        Install(forceSave: true);
    }

    private static void InstallIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "SampleScene")
        {
            return;
        }

        GameObject root = GameObject.Find(RootName);
        PokerUIManager ui = root != null ? root.GetComponent<PokerUIManager>() : null;
        if (SessionState.GetBool(InstalledKey, false)
            && root != null
            && root.GetComponent<BettingManager>() != null
            && root.GetComponent<HumanLikePokerAI>() != null
            && root.GetComponent<PokerBetChipAnimator>() != null
            && ui != null
            && ui.HasPrebuiltSceneUi()
            && Object.FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        Install(forceSave: true);
        SessionState.SetBool(InstalledKey, true);
    }

    private static void Install(bool forceSave)
    {
        GameObject root = GameObject.Find(RootName);
        bool changed = false;
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Poker Demo Root");
            changed = true;
        }

        if (root.GetComponent<PokerGameManager>() == null)
        {
            Undo.AddComponent<PokerGameManager>(root);
            changed = true;
        }

        if (root.GetComponent<BettingManager>() == null)
        {
            Undo.AddComponent<BettingManager>(root);
            changed = true;
        }

        if (root.GetComponent<HumanLikePokerAI>() == null)
        {
            Undo.AddComponent<HumanLikePokerAI>(root);
            changed = true;
        }

        if (root.GetComponent<PokerBetChipAnimator>() == null)
        {
            Undo.AddComponent<PokerBetChipAnimator>(root);
            changed = true;
        }

        PokerUIManager ui = root.GetComponent<PokerUIManager>();
        if (ui == null)
        {
            ui = Undo.AddComponent<PokerUIManager>(root);
            changed = true;
        }

        changed |= RemoveMissingScriptsFromOpenScenes();

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
            changed = true;
        }

        bool hasPrebuiltSceneUi = ui.HasPrebuiltSceneUi();
        if (!hasPrebuiltSceneUi)
        {
            Debug.LogError($"[PokerDemo] Missing prebuilt {PokerUIManager.CanvasName} hierarchy. Add the scene UI before running the poker demo.");
        }

        if (!changed)
        {
            Debug.Log(hasPrebuiltSceneUi
                ? "[PokerDemo] PokerDemoRoot is already installed."
                : "[PokerDemo] PokerDemoRoot is installed, but the required scene UI is missing.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (forceSave)
        {
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        Debug.Log("[PokerDemo] Installed PokerDemoRoot with PokerGameManager in current scene.");
    }

    private static bool RemoveMissingScriptsFromOpenScenes()
    {
        bool changed = false;
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject target = transform.gameObject;
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target) <= 0)
                    {
                        continue;
                    }

                    Undo.RegisterCompleteObjectUndo(target, "Remove Missing Scripts");
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
                    changed = true;
                }
            }
        }

        return changed;
    }
}
