using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Ending the day: the sleep prompt, passing out, the overnight results and mine knockouts.</summary>
public sealed partial class NetworkPlayerController
{
    private void OpenSleepPrompt()
    {
        motor.SetMovement(Vector2.zero);
        inputSourceBehaviour.SuppressUseUntilReleased();
        sleepPrompt = true;
    }

    internal void TakeMineContactDamage(int amount)
    {
        if (endingDay || characterStats?.TryTakeMineHit(amount) != true) return;
        GameAudio.Play("ow");
        if (characterStats.Health != 0) return;
        StartCoroutine(MineKnockout());
    }

    // Picks who carried the farmer out.
    private static readonly (string Name, string Line)[] Rescuers =
    {
        ("Linus", "Cháu tỉnh rồi à? Ta tìm thấy cháu nằm bất tỉnh dưới mỏ nên cõng cháu lên đây. Lần sau nhớ cẩn thận hơn nhé."),
        ("Linus", "Đừng cử động vội... Ta thấy cháu nằm giữa đám quái vật dưới kia. May mà ta tới kịp."),
        ("Linus", "Mỏ sâu nguy hiểm lắm. Hãy mang theo đồ ăn và đừng tham xuống quá sâu."),
        ("Linus", "Cháu ngủ say thật đấy. Ta đã đưa cháu ra cửa mỏ rồi, nghỉ ngơi đi."),
        ("Robin", "Ôi, may quá cháu tỉnh rồi! Cô đang tìm đá thì thấy cháu bất tỉnh dưới đó nên đưa cháu ra ngoài."),
        ("Clint", "Ờ... tôi thấy cậu nằm dưới mỏ nên kéo lên đây. Lần sau nhớ mang theo đồ ăn đấy."),
        ("Maru", "Cậu tỉnh rồi! Mình đang lấy mẫu khoáng thì thấy cậu ngất. Đừng liều lĩnh quá nhé.")
    };


    /// <summary>Farmer.checkForExhaustion: at -15 energy the farmer passes out and the day ends.</summary>
    private void PassOutFromExhaustion()
    {
        if (endingDay || IsKnockedOut) return;
        NpcConversation.Close();
        StartNight(true);
    }

    /// <summary>The local farmer is in the knockout sequence (played privately, like a cutscene).</summary>
    internal static bool IsKnockedOut { get; private set; }

    /// <summary>
    /// Farmer.Update with health 0 and GameLocation.checkForEvents (killScreen): a red glow and the
    /// collapse, then the farmer wakes at the mine entrance (22, 9) with a rescuer, having lost
    /// gold and items, with 10 health and at most 2 energy.
    /// </summary>
    private IEnumerator MineKnockout()
    {
        int floor = MineManager.CurrentFloor;
        endingDay = true;
        IsKnockedOut = true;
        var manager = LocationManager.Instance;
        try
        {
            inventory.SetOpen(false);
            GameUi.CloseMenus();
            if (isFishing) fishingController.CancelFishing();
            motor.SetMovement(Vector2.zero);
            motor.IsMovementEnabled = false;
            inputSourceBehaviour.SuppressUseUntilReleased();
            var clock = FarmDayClock.Instance;
            if (clock != null) clock.IsCutscenePaused = true;

            // screenGlowOnce(Red, hold) and the music stops; pauseTime 3000.
            GameAudio.PlayMusic("none");
            float collapse = playerAnimation?.Play(FarmerAnimationAction.PassOutTired) ?? 0f;
            for (float t = 0f; t < Mathf.Max(3f, collapse); t += Time.unscaledDeltaTime)
            {
                manager?.SetCutsceneFade(Mathf.Min(0.3f, t * 0.3f), Color.red);
                yield return null;
            }

            long goldLost = 0;
            var itemsLost = new System.Collections.Generic.List<(string ItemId, int Amount)>();
            string error = null;
            if (onlineGame != null && floor > 0)
            {
                bool settled = false;
                onlineGame.MineKnockout(floor, (gold, items, failure) =>
                {
                    goldLost = gold;
                    itemsLost = items;
                    error = failure;
                    settled = true;
                });
                float giveUp = Time.unscaledTime + 20f;
                while (!settled && Time.unscaledTime < giveUp) yield return null;
            }
            if (error != null) Debug.LogWarning("Mine knockout was not saved: " + error);

            // Fade to black, then wake at the mine entrance.
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
            {
                manager?.SetCutsceneFade(Mathf.Lerp(0.3f, 1f, t), Color.Lerp(Color.red, Color.black, t));
                yield return null;
            }
            manager?.SetCutsceneFade(1f, Color.black);
            if (manager != null && manager.WarpFarmerFromBlack("Mine", 22, 9, Vector2.down))
            {
                yield return null;
                while (manager.IsWarping) yield return null;
            }
            characterStats.RestoreVitals(Mathf.Min(characterStats.Stamina, 2), 10);
            if (clock != null) clock.IsCutscenePaused = false;
            GameAudio.RefreshLocationMusic();
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 2f)
            {
                manager?.SetCutsceneFade(1f - t, Color.black);
                yield return null;
            }
            manager?.SetCutsceneFade(0f);

            var rescuer = Rescuers[Random.Range(0, Rescuers.Length)];
            NpcConversation.ShowPages(rescuer.Name, new System.Collections.Generic.List<NpcDialoguePage>
                { new(rescuer.Line, 0) });
            while (NpcConversation.IsOpen) yield return null;

            // ItemsLost list.
            string message = "Bạn đã bị đánh bất tỉnh trong mỏ.";
            if (goldLost > 0 && itemsLost.Count > 0)
                message += $" Bạn bị mất {goldLost:N0}g và {itemsLost.Count} món đồ.";
            else if (goldLost > 0) message += $" Bạn bị mất {goldLost:N0}g.";
            else if (itemsLost.Count > 0) message += $" Bạn bị mất {itemsLost.Count} món đồ.";
            if (itemsLost.Count > 0)
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (var (itemId, amount) in itemsLost)
                {
                    string name = onlineGame?.ResolveItem(itemId)?.displayName ?? itemId;
                    names.Add(amount > 1 ? $"{name} ×{amount}" : name);
                }
                message += "^^Đồ bị mất: " + string.Join(", ", names) + ".";
            }
            NpcConversation.ShowNotice(message);
            while (NpcConversation.IsOpen) yield return null;
        }
        finally
        {
            manager?.SetCutsceneFade(0f);
            if (FarmDayClock.Instance != null) FarmDayClock.Instance.IsCutscenePaused = false;
            motor.IsMovementEnabled = true;
            endingDay = false;
            IsKnockedOut = false;
            inputSourceBehaviour.SuppressUseUntilReleased();
        }
    }

    private void StartNight(bool passedOut)
    {
        if (endingDay) return;
        sleepPrompt = false;
        endingDay = true;
        // Farmer.performPassoutWarp charges for passing out anywhere but home; the server decides.
        if (passedOut && onlineGame != null)
            onlineGame.PassedOutAt = LocationManager.Instance?.CurrentLocation?.LocationName;
        StartCoroutine(FinishNight(passedOut));
    }

    private IEnumerator FinishNight(bool passedOut)
    {
        var clock = FarmDayClock.Instance;
        int closingDay = clock?.Day ?? 1;
        // Farmer.timeWentToBed, or the time of passing out (2600 at 2 AM, earlier when exhausted).
        int bedtime = clock?.TimeOfDay ?? 2200;
        if (clock != null) clock.IsChangingDay = true;
        inventory.SetOpen(false);
        GameUi.CloseMenus();
        if (isFishing) fishingController.CancelFishing();
        motor.SetMovement(Vector2.zero);
        motor.IsMovementEnabled = false;
        inputSourceBehaviour.SuppressUseUntilReleased();
        if (passedOut)
        {
            float duration = playerAnimation?.Play(FarmerAnimationAction.PassOutTired) ?? 0f;
            yield return new WaitForSeconds(Mathf.Max(0.5f, duration));
        }

        IEnumerator StartMorning()
        {
            int day = clock?.Day ?? 1;
            OnlineGameSnapshot before = onlineGame != null ? onlineGame.Snapshot : null;
            var shipments = before?.gameDay == day ? before.shipping : null;
            // Farmer.dayupdate sets the morning energy first; each level-up then refills it
            // (LevelUpMenu.okButtonClicked), so the order matters.
            characterStats?.WakeUp(bedtime);
            if (nightResults == null) nightResults = gameObject.AddComponent<NightResultsMenu>();
            yield return nightResults.Show(day, shipments, onlineGame);
        }

        var manager = LocationManager.Instance;
        if (manager != null) yield return manager.WakeAtFarmHouse(StartMorning, ShowMorningCutscene);
        else yield return StartMorning();
        if (clock != null) clock.IsChangingDay = false;
        wasOnBed = true;
        motor.IsMovementEnabled = true;
        endingDay = false;
        inputSourceBehaviour.SuppressUseUntilReleased();
        StartCoroutine(ShowOvernightCharges());

        IEnumerator ShowMorningCutscene()
        {
            if (cutscenes != null) yield return cutscenes.PlayMineOpeningAtDawn(closingDay);
        }
    }

    /// <summary>
    /// Gold taken overnight (passing out away from home, and the farm rent) is taken
    /// automatically, so the farmer is only told about it on waking, one notice at a time.
    /// </summary>
    private IEnumerator ShowOvernightCharges()
    {
        var snapshot = onlineGame != null ? onlineGame.Snapshot : null;
        if (snapshot == null) yield break;
        var passOut = snapshot.passOutCharge;
        if (passOut != null && passOut.day > 0 && passOut.day == snapshot.gameDay)
        {
            NpcConversation.ShowNotice(passOut.goldLost > 0
                ? "Đêm qua bạn kiệt sức và ngất xỉu. Có người đã tìm thấy và đưa bạn về nhà, " +
                  $"nhưng bạn bị mất {passOut.goldLost:N0}g."
                : "Đêm qua bạn kiệt sức và ngất xỉu. Có người đã tìm thấy và đưa bạn về nhà.");
            while (NpcConversation.IsOpen) yield return null;
        }
        var charge = snapshot.rentCharge;
        if (charge == null || charge.day <= 0 || charge.day != snapshot.gameDay) yield break;
        string message;
        if (charge.paid > 0 && charge.stillOwed <= 0)
            message = $"Đã tự động trừ {charge.paid:N0}g tiền thuê trại.";
        else if (charge.paid > 0)
            message = $"Đã tự động trừ {charge.paid:N0}g tiền thuê trại. Còn nợ {charge.stillOwed:N0}g, " +
                      "sẽ trừ tiếp vào sáng mai.";
        else
            message = $"Không đủ vàng để trả {charge.stillOwed:N0}g tiền thuê trại. Số nợ sẽ được trừ " +
                      "tự động vào sáng mai; trong lúc nợ, bạn chưa thể cuốc ô mới hay gieo hạt.";
        GameAudio.Play(charge.paid > 0 ? "purchase" : "cancel");
        NpcConversation.ShowNotice(message);
    }

    private void OnGUI()
    {
        DrawChargeGauge();
        if (!sleepPrompt) return;
        GUI.depth = -55;
        float width = Mathf.Min(Screen.width - 80f, 460f);
        Rect box = new((Screen.width - width) / 2f, Screen.height * 0.70f, width, 104f);
        StardewUiAtlas.DrawStardewDialogueBox(box);
        NpcBitmapText.Draw(StardewUiAtlas.FontBold, new Rect(box.x + 16f, box.y + 10f, width - 32f, 44f),
            "Go to sleep for the night?", 2f, true);
        if (SleepChoice(new Rect(box.center.x - 120f, box.y + 54f, 104f, 42f), "Yes"))
            StartNight(false);
        if (SleepChoice(new Rect(box.center.x + 16f, box.y + 54f, 104f, 42f), "No"))
            sleepPrompt = false;
    }

    private static bool SleepChoice(Rect rect, string label)
    {
        StardewUiAtlas.DrawTextureBox(rect, StardewUiAtlas.Cursors, new Rect(432, 439, 9, 9),
            rect.Contains(Event.current.mousePosition) ? Color.yellow : Color.white, 4f, false);
        NpcBitmapText.Draw(StardewUiAtlas.FontBold, new Rect(rect.x, rect.y + 9f, rect.width, rect.height),
            label, 2f, true);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }
}
