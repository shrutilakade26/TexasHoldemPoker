using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animates chip movements between positions using actual chip sprites.
/// Creates flying chip effects with arc motion.
/// </summary>
public class ChipStackAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float chipFlyDuration = 0.5f;
    [SerializeField] private float delayBetweenChips = 0.08f;
    [SerializeField] private float arcHeight = 80f;
    [SerializeField] private AnimationCurve flyCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Chip Visual Settings")]
    [SerializeField] private float flyingChipSize = 40f;
    [SerializeField] private int maxFlyingChips = 8;

    [Header("Sound (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip chipSound;

    // Chip sprites loaded from Resources
    private Dictionary<int, Sprite> chipSprites = new Dictionary<int, Sprite>();
    private int[] denominations = { 1, 5, 10, 20, 25, 50, 100, 500, 1000, 5000, 10000 };
    private List<GameObject> flyingChips = new List<GameObject>();

    private void Awake()
    {
        LoadChipSprites();
        VerifyCanvasParent();
    }

    private void VerifyCanvasParent()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogError($"ChipStackAnimator on {gameObject.name} is NOT under a Canvas! UI elements (flying chips) will not render!");
            
            // Try to fix it automatically
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                Debug.LogWarning("Moving ChipStackAnimator under Canvas...");
                transform.SetParent(canvas.transform, false);
            }
            else
            {
                Debug.LogError("No Canvas found in scene! Chip animations will not work!");
            }
        }
    }

    private void LoadChipSprites()
    {
        foreach (int denom in denominations)
        {
            Sprite sprite = Resources.Load<Sprite>($"chip{denom}");
            if (sprite != null)
            {
                chipSprites[denom] = sprite;
            }
            else
            {
                // Sprite not found - will use colored circle fallback
            }
        }
        
        if (chipSprites.Count == 0)
        {
            Debug.LogWarning("No chip sprites loaded for animations! Using colored circle fallback. Add chip images to Assets/Resources/ folder.");
        }
    }

    /// <summary>
    /// Animate chips flying from source to destination.
    /// </summary>
    public IEnumerator AnimateChipsMoving(Vector3 fromPosition, Vector3 toPosition, decimal amount, System.Action onComplete = null)
    {
        List<int> chipValues = GetChipValues(amount);
        int chipCount = Mathf.Min(chipValues.Count, maxFlyingChips);

        for (int i = 0; i < chipCount; i++)
        {
            int chipValue = chipValues[i % chipValues.Count];
            StartCoroutine(AnimateSingleChip(fromPosition, toPosition, chipValue, i * delayBetweenChips));

            // Play sound
            if (audioSource != null && chipSound != null)
            {
                audioSource.PlayOneShot(chipSound, 0.5f);
            }
        }

        // Wait for all chips to finish
        yield return new WaitForSeconds(chipFlyDuration + (chipCount - 1) * delayBetweenChips + 0.1f);

        onComplete?.Invoke();
    }

    /// <summary>
    /// Animate chips from player bet area to center pot.
    /// </summary>
    public IEnumerator AnimateBetToPot(Transform playerBetPosition, Transform potPosition, decimal amount)
    {
        if (playerBetPosition == null || potPosition == null)
            yield break;

        yield return StartCoroutine(AnimateChipsMoving(
            playerBetPosition.position,
            potPosition.position,
            amount
        ));
    }

    /// <summary>
    /// Animate chips from pot to winner.
    /// </summary>
    public IEnumerator AnimatePotToWinner(Transform potPosition, Transform winnerPosition, decimal amount)
    {
        if (potPosition == null || winnerPosition == null)
        {
            yield break;
        }

        // For winning, show more chips and make it more dramatic
        List<int> chipValues = GetChipValues(amount);
        int chipCount = Mathf.Min(chipValues.Count + 4, 12);

        // Burst effect - chips go out in slightly different directions first
        for (int i = 0; i < chipCount; i++)
        {
            float angle = (i / (float)chipCount) * 360f;
            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0) * 30f;
            int chipValue = chipValues[i % chipValues.Count];

            StartCoroutine(AnimateSingleChipWithBurst(
                potPosition.position,
                winnerPosition.position,
                chipValue,
                i * delayBetweenChips * 0.5f,
                offset
            ));
        }

        float waitTime = chipFlyDuration * 1.5f + chipCount * delayBetweenChips * 0.5f;
        yield return new WaitForSeconds(waitTime);
    }

    /// <summary>
    /// Get chip values for an amount (greedy algorithm).
    /// </summary>
    private List<int> GetChipValues(decimal amount)
    {
        List<int> result = new List<int>();
        decimal remaining = amount;

        // Sort denominations highest first
        var sorted = new List<int>(denominations);
        sorted.Sort((a, b) => b.CompareTo(a));

        foreach (int denom in sorted)
        {
            while (remaining >= denom && result.Count < maxFlyingChips)
            {
                result.Add(denom);
                remaining -= denom;
            }
        }

        // Ensure at least one chip
        if (result.Count == 0 && denominations.Length > 0)
        {
            result.Add(denominations[0]);
        }

        return result;
    }

    /// <summary>
    /// Animate a single chip with arc motion.
    /// </summary>
    private IEnumerator AnimateSingleChip(Vector3 from, Vector3 to, int chipValue, float delay)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);

        GameObject chip = CreateFlyingChip(chipValue);
        chip.transform.position = from;
        flyingChips.Add(chip);

        float elapsed = 0f;
        Vector3 startPos = from;
        Vector3 endPos = to;

        // Add slight randomness to end position
        endPos += new Vector3(Random.Range(-10f, 10f), Random.Range(-5f, 5f), 0);

        while (elapsed < chipFlyDuration)
        {
            elapsed += Time.deltaTime;
            float t = flyCurve.Evaluate(elapsed / chipFlyDuration);

            // Linear position
            Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);

            // Add arc
            float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
            currentPos.y += arc;

            chip.transform.position = currentPos;

            // Rotate slightly during flight
            chip.transform.Rotate(0, 0, Time.deltaTime * 180f);

            // Scale effect
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
            chip.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        chip.transform.position = endPos;

        // Fade out and destroy
        yield return StartCoroutine(FadeOutChip(chip));

        flyingChips.Remove(chip);
        Destroy(chip);
    }

    /// <summary>
    /// Animate chip with initial burst outward then to target.
    /// </summary>
    private IEnumerator AnimateSingleChipWithBurst(Vector3 from, Vector3 to, int chipValue, float delay, Vector3 burstOffset)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);

        GameObject chip = CreateFlyingChip(chipValue);
        chip.transform.position = from;
        flyingChips.Add(chip);

        float burstDuration = 0.15f;
        float flyDuration = chipFlyDuration;

        // Burst outward
        float elapsed = 0f;
        Vector3 burstTarget = from + burstOffset;

        while (elapsed < burstDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / burstDuration;
            chip.transform.position = Vector3.Lerp(from, burstTarget, t);
            chip.transform.localScale = Vector3.one * (1f + t * 0.3f);
            yield return null;
        }

        // Fly to winner
        elapsed = 0f;
        Vector3 startPos = chip.transform.position;

        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = flyCurve.Evaluate(elapsed / flyDuration);

            Vector3 currentPos = Vector3.Lerp(startPos, to, t);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * arcHeight * 0.5f;

            chip.transform.position = currentPos;
            chip.transform.Rotate(0, 0, Time.deltaTime * 360f);

            yield return null;
        }

        // Quick scale pop at end
        chip.transform.localScale = Vector3.one * 1.3f;
        yield return new WaitForSeconds(0.1f);

        flyingChips.Remove(chip);
        Destroy(chip);
    }

    /// <summary>
    /// Create a flying chip using actual sprite.
    /// </summary>
    private GameObject CreateFlyingChip(int chipValue)
    {
        // Verify we're under a Canvas
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogError("Cannot create flying chip - ChipStackAnimator is not under a Canvas!");
            return null;
        }
        
        GameObject chip = new GameObject($"FlyingChip_{chipValue}");
        chip.transform.SetParent(transform);
        chip.transform.localScale = Vector3.one;

        RectTransform rect = chip.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(flyingChipSize, flyingChipSize);

        // Canvas group for fading
        CanvasGroup canvasGroup = chip.AddComponent<CanvasGroup>();

        // Chip image
        Image image = chip.AddComponent<Image>();
        image.preserveAspect = true;

        // Get sprite for this chip value
        if (chipSprites.ContainsKey(chipValue))
        {
            image.sprite = chipSprites[chipValue];
        }
        else
        {
            // Find closest
            int closest = FindClosestDenomination(chipValue);
            if (chipSprites.ContainsKey(closest))
            {
                image.sprite = chipSprites[closest];
            }
            else
            {
                // Fallback: Use colored circle if no sprites available
                Texture2D texture = new Texture2D(64, 64);
                Color chipColor = GetChipColor(chipValue);
                Color[] pixels = new Color[64 * 64];
                for (int i = 0; i < pixels.Length; i++)
                {
                    int x = i % 64;
                    int y = i / 64;
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
                    pixels[i] = dist < 30 ? chipColor : Color.clear;
                }
                texture.SetPixels(pixels);
                texture.Apply();
                
                image.sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            }
        }

        return chip;
    }

    /// <summary>
    /// Get a color for a chip value (fallback when sprites are missing).
    /// </summary>
    private Color GetChipColor(int value)
    {
        // Color coding by denomination
        if (value >= 5000) return new Color(1f, 0.5f, 0f); // Orange
        if (value >= 1000) return new Color(0.8f, 0f, 0.8f); // Purple
        if (value >= 500) return new Color(0f, 0f, 1f); // Blue
        if (value >= 100) return new Color(1f, 0f, 0f); // Red
        if (value >= 50) return new Color(0f, 1f, 0f); // Green
        if (value >= 25) return new Color(1f, 1f, 0f); // Yellow
        if (value >= 10) return new Color(0f, 1f, 1f); // Cyan
        if (value >= 5) return new Color(1f, 0.5f, 0.5f); // Light red
        return new Color(0.8f, 0.8f, 0.8f); // Gray for $1
    }

    private int FindClosestDenomination(int value)
    {
        int closest = 1;
        int minDiff = int.MaxValue;

        foreach (var key in chipSprites.Keys)
        {
            int diff = Mathf.Abs(key - value);
            if (diff < minDiff)
            {
                minDiff = diff;
                closest = key;
            }
        }

        return closest;
    }

    /// <summary>
    /// Fade out a chip.
    /// </summary>
    private IEnumerator FadeOutChip(GameObject chip)
    {
        CanvasGroup canvasGroup = chip.GetComponent<CanvasGroup>();
        if (canvasGroup == null) yield break;

        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = 1f - (elapsed / duration);
            yield return null;
        }
    }

    /// <summary>
    /// Clean up any remaining flying chips.
    /// </summary>
    public void ClearFlyingChips()
    {
        foreach (var chip in flyingChips)
        {
            if (chip != null)
                Destroy(chip);
        }
        flyingChips.Clear();
    }

    private void OnDestroy()
    {
        ClearFlyingChips();
    }
}
