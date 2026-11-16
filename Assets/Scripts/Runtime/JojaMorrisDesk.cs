using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Morris's desk in JojaMart (Buildings action "JoinJoja" at 21, 25), after JojaMart.checkAction:
/// he offers the 5,000g membership, and members get the Joja Community Development form (JojaCDMenu),
/// Drawn at 4x with a check over each finished project.
/// </summary>
internal sealed class JojaMorrisDesk : MonoBehaviour
{
    private const int DeskX = 21, DeskY = 25;
    private const float Width = 1280f, Height = 576f;
    // JojaCDMenu buttons 0-4 and the rooms they replace.
    private static readonly string[] ProjectRooms = { "vault", "boiler", "crafts", "pantry", "fish" };
    private static readonly string[] ProjectNames = { "Xe buýt", "Xe goòng", "Sửa cầu", "Nhà kính", "Đãi quặng" };
    private static readonly long[] ProjectPrices = { 40000, 15000, 25000, 35000, 20000 };

    private static JojaMorrisDesk instance;
    private OnlineGameState online;
    private Texture2D form;
    private bool busy, boughtSomething;
    private float closeAt = -1f;
    private string message;

    internal static bool IsOpen => instance != null && instance.enabled;

    internal static bool TryInteract(Vector2? pointer, Vector3 standing, Vector2 facing)
    {
        if (LocationManager.Instance?.CurrentLocation?.LocationName != "JojaMart" ||
            !NpcShop.HitActionTile(pointer, standing, facing, DeskX, DeskY)) return false;
        var online = LocationManager.Instance.LocalFarmer?.GetComponent<OnlineGameState>();
        var milestones = online?.Snapshot?.milestones;
        if (online == null || milestones == null)
        {
            NpcConversation.ShowNotice("Đang tải dữ liệu.");
            return true;
        }
        if (milestones.HasSeen("joja_opening"))
            Say("Mọi dự án phát triển cộng đồng đã hoàn tất. Cảm ơn quý khách đã luôn tin dùng Joja!");
        else if (milestones.HasSeen("community_opening"))
            Say("Trung tâm Cộng đồng... Thật đáng tiếc. Joja vẫn luôn chào đón quý khách.");
        else if (!milestones.HasSeen("JojaMember"))
        {
            Say("Chào mừng đến với JojaMart! Hãy trở thành hội viên Joja chỉ với 5.000g. Hội viên được dùng " +
                "Phiếu Phát triển Cộng đồng Joja để cải thiện thị trấn, nhanh và tiện hơn hẳn đám chuột trong " +
                "cái nhà văn hóa cũ kia!");
            WhenClosed(() => NpcConversation.ShowChoices("Đăng ký hội viên Joja với giá 5.000g?", new (string, System.Action)[]
            {
                ("Đăng ký (5.000g)", () => Join(online)),
                ("Để sau", () => { })
            }));
        }
        else
        {
            Say("A, hội viên thân mến! Đây là Phiếu Phát triển Cộng đồng Joja. Cứ chọn dự án mà quý khách muốn tài trợ.");
            WhenClosed(() => Open(online));
        }
        return true;
    }

    private static void Say(string line) =>
        NpcConversation.ShowPages("Morris", new List<NpcDialoguePage> { new(line, 0) });

    private static void WhenClosed(System.Action next)
    {
        void Handler()
        {
            NpcConversation.Closed -= Handler;
            next();
        }
        NpcConversation.Closed += Handler;
    }

    private static void Join(OnlineGameState online)
    {
        online.JojaPurchase(-1, error =>
        {
            if (error != null)
            {
                GameAudio.Play("cancel");
                Say(error.Contains("5.000g") ? "Ồ, có vẻ quý khách chưa đủ tiền. Hẹn gặp lại!" : error);
                return;
            }
            // signUpForJoja: "purchase" and Morris's welcome.
            GameAudio.Play("purchaseClick");
            Say("Tuyệt vời! Chào mừng quý khách đến với gia đình Joja. Từ giờ hãy dùng Phiếu Phát triển Cộng đồng tại quầy này nhé.");
        });
    }

    private static void Open(OnlineGameState online)
    {
        if (instance == null)
        {
            var host = new GameObject("JojaCDMenu");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<JojaMorrisDesk>();
        }
        var catalog = Resources.Load<NpcCatalog>("NpcCatalog");
        instance.form = catalog != null ? catalog.jojaCDForm : null;
#if UNITY_EDITOR
        if (instance.form == null)
            instance.form = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/LooseSprites/JojaCDForm.png");
#endif
        instance.online = online;
        instance.busy = instance.boughtSomething = false;
        instance.closeAt = -1f;
        instance.message = null;
        instance.enabled = true;
        GameAudio.Play("bigSelect");
        InputController.SuppressWorldUseAfterModal();
    }

    private void Close()
    {
        enabled = false;
        InputController.SuppressWorldUseAfterModal();
        // onExitFunction: Morris thanks the farmer after a purchase.
        if (boughtSomething)
            Say("Cảm ơn quý khách đã tài trợ! Đội ngũ Joja sẽ bắt tay vào việc ngay. Hẹn gặp lại!");
    }

    private void Update()
    {
        if (closeAt >= 0f && Time.unscaledTime >= closeAt) { Close(); return; }
        if (busy || closeAt >= 0f) return;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Keyboard.current?.eKey.wasPressedThisFrame == true ||
            Gamepad.current?.buttonEast.wasPressedThisFrame == true)
            Close();
    }

    private bool Done(int project) => online?.Snapshot?.milestones?.rooms is { } rooms &&
        System.Array.Exists(rooms, room => room != null && room.done && room.id == ProjectRooms[project]);

    private void OnGUI()
    {
        GUI.depth = -60;
        float scale = Mathf.Min(1f, (Screen.width - 32f) / Width, (Screen.height - 32f) / Height);
        Vector2 origin = new((Screen.width - Width * scale) / 2f, (Screen.height - Height * scale) / 2f);
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        if (Event.current.type == EventType.Repaint)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        GUI.matrix = Matrix4x4.TRS(origin, Quaternion.identity, new Vector3(scale, scale, 1f)) * previousMatrix;
        Vector2 mouse = Event.current.mousePosition;
        string hover = null;

        if (Event.current.type == EventType.Repaint) Draw(new Rect(0f, 0f, Width, Height), new Rect(0, 0, 320, 144));
        for (int project = 0; project < 5; project++)
        {
            var box = new Rect(4f + project % 2 * 592f, 208f + project / 2 * 120f, 588f, 120f);
            bool done = Done(project);
            if (box.Contains(mouse)) hover = done ? ProjectNames[project] + " (đã tài trợ)" : $"{ProjectNames[project]}: {ProjectPrices[project]:N0}g";
            if (done && Event.current.type == EventType.Repaint)
                Draw(new Rect(box.x + 16f, box.y + 16f, 64f, 64f), new Rect(0, 144, 16, 16));
            if (!done && !busy && closeAt < 0f && Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                box.Contains(mouse))
            {
                Event.current.Use();
                Buy(project);
            }
        }
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && !busy && closeAt < 0f)
        {
            Event.current.Use();
            Close();
        }

        GUI.matrix = previousMatrix;
        if (Event.current.type == EventType.Repaint)
        {
            string text = message ?? hover;
            // Draw hover text.
            if (!string.IsNullOrEmpty(text)) GameText.DrawTooltip(text, null, Screen.width, Screen.height);
        }
        GUI.color = previousColor;
    }

    private void Buy(int project)
    {
        busy = true;
        message = null;
        online.JojaPurchase(project, error =>
        {
            busy = false;
            if (error != null)
            {
                // dayTimeMoneyBox.moneyShakeTimer: not enough gold.
                GameAudio.Play("cancel");
                message = error;
                return;
            }
            GameAudio.Play("reward");
            boughtSomething = true;
            closeAt = Time.unscaledTime + 1f; // exitTimer = 1000
        });
    }

    private void Draw(Rect destination, Rect source)
    {
        if (form == null) return;
        GUI.DrawTextureWithTexCoords(destination, form, new Rect(source.x / form.width,
            1f - (source.y + source.height) / form.height, source.width / form.width, source.height / form.height));
    }
}
