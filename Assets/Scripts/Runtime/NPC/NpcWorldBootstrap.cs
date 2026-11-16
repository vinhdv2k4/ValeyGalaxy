using UnityEngine;
using UnityEngine.SceneManagement;

public static class NpcWorldBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SpawnForCurrentScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SpawnForCurrentScene();

    private static void SpawnForCurrentScene()
    {
        if (LocationManager.Instance == null || GameObject.Find("NPC World") != null) return;
        NpcCatalog catalog = Resources.Load<NpcCatalog>("NpcCatalog");
        if (catalog == null) return;

        var root = new GameObject("NPC World");
        foreach (NpcDefinition npc in catalog.characters)
        {
            if (npc?.schedule != null) Spawn(root.transform, npc);
        }
        Spawn(root.transform, catalog.marlon);
    }

    private static void Spawn(Transform root, NpcDefinition npc)
    {
        if (npc?.animationFrames == null || npc.animationFrames.Length == 0) return;
        var actorObject = new GameObject(npc.name);
        actorObject.transform.SetParent(root, false);
        var visual = new GameObject("Visual");
        visual.transform.SetParent(actorObject.transform, false);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.enabled = false;
        actorObject.AddComponent<NpcActor>().Configure(npc);
    }
}
