using UnityEngine;

/// <summary>
/// Plays an 8-frame water splash animation at a delicate, properly proportioned size
/// matching the bobber and water physics.
/// </summary>
public sealed class FishingSplashEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float frameDuration = 0.065f;

    private float timer;
    private int currentFrame;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        timer = 0f;
        currentFrame = 0;
        if (spriteRenderer != null && frames != null && frames.Length > 0)
        {
            spriteRenderer.sprite = frames[0];
        }
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;

        timer += Time.deltaTime;
        int frame = Mathf.FloorToInt(timer / frameDuration);
        if (frame >= frames.Length)
        {
            gameObject.SetActive(false);
        }
        else if (frame != currentFrame)
        {
            currentFrame = frame;
            if (spriteRenderer != null)
                spriteRenderer.sprite = frames[currentFrame];
        }
    }

    public void PlayAt(Vector3 position, int sortingOrder)
    {
        transform.position = position;
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingLayerName = "Default";
            spriteRenderer.sortingOrder = sortingOrder;
        }
        gameObject.SetActive(true);
        OnEnable();
    }
}
