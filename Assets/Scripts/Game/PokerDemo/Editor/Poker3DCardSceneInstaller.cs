using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
// SampleScene에 3D 카드 루트와 좌석/커뮤니티 카드 오브젝트를 만들고 PokerUIManager와 연결하는 에디터 유틸리티입니다.
public static class Poker3DCardSceneInstaller
{
    private const string InstalledKey = "RealPoker2026.PokerDemo.SampleScene3DCardsInstalled";

    static Poker3DCardSceneInstaller()
    {
        EditorApplication.delayCall += InstallIfNeeded;
    }

    [MenuItem("Tools/Poker Demo/Install 3D Cards In SampleScene")]
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

        Poker3DCardTableView existingTable = Object.FindObjectOfType<Poker3DCardTableView>();
        if (SessionState.GetBool(InstalledKey, false) && existingTable != null && existingTable.HasSeatCards())
        {
            return;
        }

        Install(forceSave: true);
        SessionState.SetBool(InstalledKey, true);
    }

    private static void Install(bool forceSave)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "SampleScene")
        {
            Debug.LogWarning("[Poker3DCardSceneInstaller] Open SampleScene before installing 3D poker cards.");
            return;
        }

        bool changed = false;
        Poker3DCardTableView existingTable = Object.FindObjectOfType<Poker3DCardTableView>(true);
        GameObject root = existingTable != null ? existingTable.gameObject : null;
        if (root == null)
        {
            root = new GameObject(Poker3DCardTableView.RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Poker 3D Card Root");
            changed = true;
        }

        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Poker3DCardTableView table = root.GetComponent<Poker3DCardTableView>();
        if (table == null)
        {
            table = Undo.AddComponent<Poker3DCardTableView>(root);
            changed = true;
        }

        for (int seatIndex = 0; seatIndex < 6; seatIndex++)
        {
            changed |= EnsureCard(root.transform, $"Seat{seatIndex}Card3D_1");
            changed |= EnsureCard(root.transform, $"Seat{seatIndex}Card3D_2");
        }

        for (int i = 1; i <= 5; i++)
        {
            changed |= EnsureCard(root.transform, $"CommunityCard3D_{i}");
        }

        changed |= DeleteLegacyCard(root.transform, "PlayerCard3D_1");
        changed |= DeleteLegacyCard(root.transform, "PlayerCard3D_2");
        changed |= DeleteLegacyCard(root.transform, "OpponentCard3D_1");
        changed |= DeleteLegacyCard(root.transform, "OpponentCard3D_2");

        table.ResolveCardsFromChildren();
        table.ApplyDefaultLayout();
        EditorUtility.SetDirty(table);

        changed |= LinkUiTo3DTable(table);

        if (!changed)
        {
            Debug.Log("[Poker3DCardSceneInstaller] 3D poker cards are already installed in SampleScene.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        if (forceSave)
        {
            EditorSceneManager.SaveScene(activeScene);
        }

        Debug.Log("[Poker3DCardSceneInstaller] Installed Poker3DCardRoot and linked it to PokerUIManager.");
    }

    private static bool EnsureCard(Transform parent, string name)
    {
        bool changed = false;
        Transform child = parent.Find(name);
        GameObject cardObject;
        if (child == null)
        {
            cardObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(cardObject, $"Create {name}");
            cardObject.transform.SetParent(parent, false);
            changed = true;
        }
        else
        {
            cardObject = child.gameObject;
        }

        if (cardObject.GetComponent<MeshFilter>() == null)
        {
            Undo.AddComponent<MeshFilter>(cardObject);
            changed = true;
        }

        if (cardObject.GetComponent<MeshRenderer>() == null)
        {
            Undo.AddComponent<MeshRenderer>(cardObject);
            changed = true;
        }

        if (cardObject.GetComponent<Poker3DCardView>() == null)
        {
            Undo.AddComponent<Poker3DCardView>(cardObject);
            changed = true;
        }

        return changed;
    }

    private static bool DeleteLegacyCard(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            return false;
        }

        Undo.DestroyObjectImmediate(child.gameObject);
        return true;
    }

    private static bool LinkUiTo3DTable(Poker3DCardTableView table)
    {
        PokerUIManager ui = Object.FindObjectOfType<PokerUIManager>();
        if (ui == null)
        {
            return false;
        }

        SerializedObject serializedUi = new SerializedObject(ui);
        SerializedProperty tableProperty = serializedUi.FindProperty("card3DTable");
        bool changed = false;

        if (tableProperty != null && tableProperty.objectReferenceValue != table)
        {
            tableProperty.objectReferenceValue = table;
            changed = true;
        }

        if (changed)
        {
            serializedUi.ApplyModifiedProperties();
            EditorUtility.SetDirty(ui);
        }

        return changed;
    }
}
