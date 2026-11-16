using UnityEngine;

/// <summary>World object for one server-owned, player-crafted chest.</summary>
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public sealed class PlacedPlayerChest : MonoBehaviour
{
    public string ChestId { get; private set; }

    public void Bind(string id, int tileX, int tileY)
    {
        ChestId = id;
        transform.position = new Vector3(tileX + 0.5f, -tileY - 0.5f, 0f);
        var renderer = GetComponent<SpriteRenderer>();
        renderer.sortingOrder = WorldSortOrder.GetOrder(transform.position.y);
    }
}
