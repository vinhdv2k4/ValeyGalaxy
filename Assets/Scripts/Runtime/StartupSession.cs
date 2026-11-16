using UnityEngine;
using UnityEngine.SceneManagement;

public static class StartupSession
{
    internal const string ArrivalEventId = "60367";
    private static FarmerIntroMode opening;
    private static OnlineGameSnapshot initialSnapshot;
    internal static bool IsOpening => opening != FarmerIntroMode.ExistingSave;
    public static CreatedFarmer Farmer { get; private set; }
    public static StartupApiClient Api { get; private set; }

    internal static void ReturnToTitle()
    {
        // Leaving the game from the menu keeps the current spot, time and energy.
        PlayerSessionKeeper.Active?.SaveNow();
        Farmer = null;
        Api = null;
        opening = FarmerIntroMode.ExistingSave;
        initialSnapshot = null;
        GameLoadingScreen.Hide();
        SceneManager.LoadScene("Main Menu");
    }

    /// <summary>
    /// Reloads the play scene for the same account so a server-side change (admin day jump,
    /// checkpoint, reset) is read fresh. Saves the current spot first only when asked: a
    /// restored checkpoint carries its own saved spot.
    /// </summary>
    internal static void ReloadGame(bool saveSpot)
    {
        if (Api == null || Farmer == null) return;
        if (saveSpot) PlayerSessionKeeper.Active?.SaveNow();
        GameLoadingScreen.Show();
        EnterGame(Farmer, Api);
    }

    public static void EnterGame(CreatedFarmer farmer, StartupApiClient api,
        FarmerIntroMode intro = FarmerIntroMode.ExistingSave, OnlineGameSnapshot snapshot = null)
    {
        Farmer = farmer;
        Api = api;
        opening = intro;
        initialSnapshot = snapshot;

        SceneManager.sceneLoaded -= ApplyFarmer;
        SceneManager.sceneLoaded += ApplyFarmer;
        SceneManager.LoadScene("GamePlay");
    }

    private static void ApplyFarmer(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "GamePlay") return;
        SceneManager.sceneLoaded -= ApplyFarmer;
        var snapshot = initialSnapshot;
        initialSnapshot = null;
        if (Farmer == null) { GameLoadingScreen.Hide(); return; }
        PlayerAppearance[] appearances = Object.FindObjectsByType<PlayerAppearance>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (appearances.Length > 0) appearances[0].ApplyCreatedFarmer(Farmer);
        else Debug.LogWarning("Character profile loaded, but no PlayerAppearance was found.");
        if (appearances.Length == 0 || Api == null)
        {
            GameLoadingScreen.Hide();
            FinishOpening();
            return;
        }
        var player = appearances[0].gameObject;
        player.AddComponent<OnlineGameState>().Initialize(Api, snapshot);
        // A brand-new farmer starts with the opening; everyone else resumes their last session.
        player.AddComponent<PlayerSessionKeeper>().Initialize(Api, resume: !IsOpening);
        // Removes itself unless the server reports this account as an admin.
        player.AddComponent<AdminPanel>();
        if (IsOpening)
        {
            // The arrival cutscene opens out of its own black screen.
            GameLoadingScreen.Hide();
            FarmDayClock.Instance.IsCutscenePaused = true;
            LocationManager.Instance?.SetCutsceneFade(1f);
            player.AddComponent<StartupFarmArrival>().Initialize(opening);
        }
    }

    internal static void FinishOpening()
    {
        opening = FarmerIntroMode.ExistingSave;
        if (FarmDayClock.Instance != null) FarmDayClock.Instance.IsCutscenePaused = false;
    }
}
