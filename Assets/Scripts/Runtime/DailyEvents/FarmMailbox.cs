using UnityEngine;

internal sealed class FarmMailbox : MonoBehaviour
{
    private const float Reach = 1.45f;
    private Transform unreadMarker;
    private SpriteRenderer box;

    internal static FarmMailbox Attach(GameLocation farm)
    {
        var mailbox = new GameObject("Mailbox");
        mailbox.transform.SetParent(farm.transform, false);
        Vector2Int tile = farm.ContentMapName == "Farm_Ranching" ? new(85, 20) : new(68, 16);
        mailbox.transform.position = farm.TileCenter(tile.x, tile.y);
        return mailbox.AddComponent<FarmMailbox>();
    }

    private void Awake()
    {
        var texture = Resources.Load<Texture2D>("DailyEvents/Mailbox");
        var sprite = MakeSprite(texture, new Rect(0, 0, 16, 32), new Vector2(0.5f, 0.25f));
        box = gameObject.AddComponent<SpriteRenderer>();
        box.sprite = sprite;
        box.sortingOrder = WorldSortOrder.GetOrder(transform.position.y);
        gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;

        var atlas = StardewUiAtlas.Cursors;
        if (atlas == null) return;
        unreadMarker = new GameObject("UnreadMail").transform;
        unreadMarker.SetParent(transform, false);
        // Farm.draw offsets, relative to the center of the mailbox's action tile.
        AddMarkerSprite("Bubble", atlas, new Rect(141, 465, 20, 24), new Vector2(-0.5f, 2.75f),
            new Vector2(0, 1), new Color(1f, 1f, 1f, 0.75f), 1);
        AddMarkerSprite("Envelope", atlas, new Rect(189, 423, 15, 13), new Vector2(0.0625f, 2f),
            new Vector2(7f / 15f, 7f / 13f), Color.white, 2);
        SetUnread(false);
    }

    internal bool IsInReach(Vector2 standing) =>
        ((Vector2)transform.position - standing).sqrMagnitude <= Reach * Reach;

    internal bool ContainsPointer(Vector2 pointer) => box != null && box.bounds.Contains(
        new Vector3(pointer.x, pointer.y, box.bounds.center.z));

    internal bool CanInteract(Vector2? pointer, Vector2 standing, Vector2 facing) =>
        IsInReach(standing) && (pointer.HasValue ? ContainsPointer(pointer.Value) :
            Vector2.Dot(((Vector2)transform.position - standing).normalized, facing) >= 0.35f);

    internal void SetUnread(bool unread)
    {
        if (unreadMarker != null && unreadMarker.gameObject.activeSelf != unread)
            unreadMarker.gameObject.SetActive(unread);
    }

    private void LateUpdate()
    {
        if (unreadMarker != null && unreadMarker.gameObject.activeSelf)
            unreadMarker.localPosition = Vector3.up * (Mathf.Sin(Time.time * 4f) / 16f);
    }

    private void AddMarkerSprite(string name, Texture2D atlas, Rect source, Vector2 position,
        Vector2 pivot, Color color, int order)
    {
        var child = new GameObject(name);
        child.transform.SetParent(unreadMarker, false);
        child.transform.localPosition = position;
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = MakeSprite(atlas, source, pivot);
        renderer.color = color;
        renderer.sortingOrder = box.sortingOrder + order;
    }

    private static Sprite MakeSprite(Texture2D texture, Rect source, Vector2 pivot)
    {
        if (texture == null) return null;
        source.y = texture.height - source.yMax;
        return AtlasSprites.Get(texture, source, pivot, 16f, true);
    }
}
