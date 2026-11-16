using System;
using UnityEngine;

/// <summary>
/// Điều khiển nhân vật phụ (Puppet) của người chơi khác:
/// - Nội suy mượt mà vị trí bằng Vector3.Lerp.
/// - Tự động phát hoạt ảnh bước đi khi di chuyển.
/// - Phát lại hành động (cuốc, chém, chặt...) và hiện vật phẩm đang cầm.
/// - Hiển thị Name Tag với tên người chơi trên đầu nhân vật.
/// </summary>
public sealed class RemotePlayerPuppet : MonoBehaviour
{
    private CharacterMotor motor;
    private PlayerAnimationController playerAnimation;
    private PlayerInventory inventory;
    private Vector3 targetPosition;
    private Vector2 targetFacing = Vector2.down;
    private Vector3 lastPosition;
    private int lastActionSeq = -1;
    private float actionUntil;
    private string heldItemId;

    public string UserId { get; private set; }
    public string DisplayName { get; private set; }

    /// <summary>
    /// Nhân bản từ Local Player trong Scene để giữ nguyên cấu trúc Sprite và Animation, rồi bỏ
    /// mọi thứ điều khiển người chơi cục bộ. Null khi chưa có người chơi cục bộ để nhân bản.
    /// </summary>
    internal static RemotePlayerPuppet Spawn(OnlineGameState template, StartupApiClient.RemotePlayerDto player)
    {
        if (template == null) return null;
        var go = Instantiate(template.gameObject, new Vector3(player.x, player.y, 0f), Quaternion.identity);
        go.name = "RemotePlayer_" + player.username;

        RemoveBehaviour<InputController>(go);
        RemoveBehaviour<NetworkPlayerController>(go);
        RemoveBehaviour<PlayerWarpDetector>(go);
        RemoveBehaviour<OnlineGameState>(go);
        RemoveBehaviour<FishingController>(go);

        // Remote avatars are visual representations, not physical players.
        foreach (var collider in go.GetComponentsInChildren<Collider2D>(true))
            collider.enabled = false;
        var body = go.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        var puppet = go.AddComponent<RemotePlayerPuppet>();
        puppet.Initialize(player.userId, player.username);

        // Áp dụng trang phục, kiểu tóc, màu da đúng theo hồ sơ của người chơi đó
        var appearance = go.GetComponent<PlayerAppearance>();
        if (appearance != null && player.farmer != null) appearance.ApplyCreatedFarmer(player.farmer);
        return puppet;
    }

    private static void RemoveBehaviour<T>(GameObject go) where T : Behaviour
    {
        var component = go.GetComponent<T>();
        if (component == null) return;
        component.enabled = false;
        Destroy(component);
    }

    private void Initialize(string playerId, string playerName)
    {
        UserId = playerId;
        DisplayName = playerName;
        motor = GetComponent<CharacterMotor>();
        playerAnimation = GetComponent<PlayerAnimationController>();
        inventory = GetComponent<PlayerInventory>();
        // The clone copied the local farmer's bag; it must only show what this farmer holds.
        ApplyHeldItem(null, null);
        targetPosition = transform.position;
        lastPosition = transform.position;

        CreateNameTag(playerName);
    }

    private void CreateNameTag(string playerName)
    {
        var tagGo = new GameObject("NameTag");
        tagGo.transform.SetParent(transform, false);
        tagGo.transform.localPosition = new Vector3(0f, 1.15f, 0f);

        var textMesh = tagGo.AddComponent<TextMesh>();
        textMesh.text = playerName;
        textMesh.fontSize = 24;
        textMesh.characterSize = 0.05f;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.color = new Color(1f, 0.95f, 0.7f, 1f); // Màu vàng nhạt dịu mắt

        var meshRenderer = tagGo.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = 32000; // Luôn hiển thị trên cùng, không bị cây cỏ che khuất
        }
    }

    public void UpdateTarget(Vector2 pos, Vector2 facing)
    {
        targetPosition = new Vector3(pos.x, pos.y, 0f);
        targetFacing = facing;
    }

    /// <summary>Farmer.CurrentTool / ActiveObject: the item drawn in this farmer's hands.</summary>
    internal void ApplyHeldItem(string itemId, Func<string, ItemData> resolve)
    {
        itemId ??= "";
        if (inventory == null || itemId == heldItemId) return;
        heldItemId = itemId;
        OnlineStack[] held = itemId.Length == 0 || resolve == null ? null : new[]
        {
            new OnlineStack { slot = inventory.SelectedIndex, itemId = itemId, amount = 1 }
        };
        inventory.ReplaceFromServer(held, resolve ?? (_ => null));
    }

    /// <summary>
    /// Replays the other farmer's action when their counter moves (NetEvent0.Fire on
    /// fireToolEvent): the swing, chop, hoe or watering plays once, facing their way.
    /// </summary>
    internal void ApplyAction(int sequence, string actionName, Vector2 facing)
    {
        // First sight, or the other farmer reloaded and restarted counting: nothing to replay.
        if (lastActionSeq < 0 || sequence < lastActionSeq)
        {
            lastActionSeq = sequence;
            return;
        }
        if (sequence == lastActionSeq) return;
        lastActionSeq = sequence;
        if (playerAnimation == null || string.IsNullOrEmpty(actionName) ||
            !Enum.TryParse(actionName, out FarmerAnimationAction action)) return;

        if (facing.sqrMagnitude > 0.01f) targetFacing = facing;
        if (motor != null)
        {
            motor.SetMovement(Vector2.zero);
            motor.FaceTowards(targetFacing);
        }
        float duration = playerAnimation.Play(action);
        actionUntil = Time.time + Mathf.Max(0.25f, duration);
    }

    private void Update()
    {
        // Nội suy mượt mà về vị trí đích
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 12f);

        // Tính toán vận tốc để bật hoạt ảnh bước đi
        Vector3 delta = (transform.position - lastPosition) / Mathf.Max(0.0001f, Time.deltaTime);
        lastPosition = transform.position;

        if (motor != null)
        {
            // Hold still while an action plays, as the other farmer did.
            if (Time.time < actionUntil)
            {
                motor.SetMovement(Vector2.zero);
                motor.FaceTowards(targetFacing);
                return;
            }
            if (delta.sqrMagnitude > 0.04f)
            {
                // Walking away ends a held pose such as casting a rod.
                if (playerAnimation != null && playerAnimation.ActiveAction.HasValue) playerAnimation.StopAction();
                motor.SetMovement(new Vector2(delta.x, delta.y).normalized);
            }
            else
            {
                motor.SetMovement(Vector2.zero);
                motor.FaceTowards(targetFacing);
            }
        }
    }
}
