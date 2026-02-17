using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual chip stack display using actual chip sprite images from Resources.
/// Displays stacked poker chips based on bet amount.
/// </summary>
public class ChipStack : MonoBehaviour
{
    [Header("Chip Settings")]
    [SerializeField] private int maxVisibleChips = 8;
    [SerializeField] private float chipStackOffset = 5f; // Vertical offset between stacked chips
    [SerializeField] private float chipSize = 40f;
    [SerializeField] private float stackSpacing = 35f; // Horizontal spacing between stacks

    [Header("Chip Denominations")]
    [SerializeField] private ChipDenomination[] denominations;

    private List<GameObject> activeChips = new List<GameObject>();
    private decimal currentAmount = 0;
    private Dictionary<int, Sprite> chipSprites = new Dictionary<int, Sprite>();

    [System.Serializable]
    public class ChipDenomination
    {
        public int value;
        public string spriteName; // e.g., "chip100" for chip100.png
    }

    private void Awake()
    {
        // Setup default denominations using YOUR chip images
        if (denominations == null || denominations.Length == 0)
        {
            denominations = new ChipDenomination[]
            {
                new ChipDenomination { value = 1, spriteName = "chip1" },
                new ChipDenomination { value = 5, spriteName = "chip5" },
                new ChipDenomination { value = 10, spriteName = "chip10" },
                new ChipDenomination { value = 20, spriteName = "chip20" },
                new ChipDenomination { value = 25, spriteName = "chip25" },
                new ChipDenomination { value = 50, spriteName = "chip50" },
                new ChipDenomination { value = 100, spriteName = "chip100" },
                new ChipDenomination { value = 500, spriteName = "chip500" },
                new ChipDenomination { value = 1000, spriteName = "chip1000" },
                new ChipDenomination { value = 5000, spriteName = "chip5000" },
                new ChipDenomination { value = 10000, spriteName = "chip10000" },
            };
        }

        // Pre-load all chip sprites
        LoadChipSprites();
    }

    private void LoadChipSprites()
    {
        foreach (var denom in denominations)
        {
            Sprite sprite = Resources.Load<Sprite>(denom.spriteName);
            if (sprite != null)
            {
                chipSprites[denom.value] = sprite;
            }
            else
            {
                Debug.LogError($"❌ Could not load chip sprite: {denom.spriteName} from Resources folder!");
            }
        }
        
        if (chipSprites.Count == 0)
        {
            Debug.LogError("❌ NO CHIP SPRITES LOADED! Make sure chip images are in Assets/Resources/ folder!");
        }
        else
        {
            Debug.Log($"✅ Loaded {chipSprites.Count} chip sprites");
        }
    }

    /// <summary>
    /// Set the chip stack to display a specific amount.
    /// </summary>
    public void SetAmount(decimal amount)
    {
        if (amount == currentAmount && activeChips.Count > 0)
            return;

        currentAmount = amount;
        ClearChips();

        if (amount <= 0)
            return;

        // Calculate chips needed
        List<int> chipsToShow = CalculateChips(amount);

        // Create chip visuals
        CreateChipVisuals(chipsToShow);
    }

    /// <summary>
    /// Calculate which chip denominations to display for a given amount.
    /// Uses greedy algorithm - highest denominations first.
    /// </summary>
    private List<int> CalculateChips(decimal amount)
    {
        List<int> result = new List<int>();
        decimal remaining = amount;

        // Sort denominations from highest to lowest
        var sortedDenoms = new List<ChipDenomination>(denominations);
        sortedDenoms.Sort((a, b) => b.value.CompareTo(a.value));

        foreach (var denom in sortedDenoms)
        {
            while (remaining >= denom.value && result.Count < maxVisibleChips)
            {
                result.Add(denom.value);
                remaining -= denom.value;
            }
        }

        return result;
    }

    /// <summary>
    /// Create the visual chip GameObjects.
    /// </summary>
    private void CreateChipVisuals(List<int> chipValues)
    {
        // Group chips by denomination for stacking
        Dictionary<int, int> chipCounts = new Dictionary<int, int>();

        foreach (int value in chipValues)
        {
            if (!chipCounts.ContainsKey(value))
                chipCounts[value] = 0;
            chipCounts[value]++;
        }

        float xOffset = 0;

        // Sort by denomination value for consistent display (highest first, visually on right)
        var sortedValues = new List<int>(chipCounts.Keys);
        sortedValues.Sort((a, b) => a.CompareTo(b)); // Lowest on left

        foreach (int denomValue in sortedValues)
        {
            int count = chipCounts[denomValue];

            // Create stack of chips for this denomination
            for (int i = 0; i < count; i++)
            {
                GameObject chipObj = CreateSingleChip(denomValue);
                if (chipObj != null)
                {
                    RectTransform rect = chipObj.GetComponent<RectTransform>();
                    rect.anchoredPosition = new Vector2(xOffset, i * chipStackOffset);
                    activeChips.Add(chipObj);
                }
            }

            xOffset += stackSpacing;
        }
    }

    /// <summary>
    /// Create a single chip visual using the actual sprite.
    /// </summary>
    private GameObject CreateSingleChip(int denomination)
    {
        if (!chipSprites.ContainsKey(denomination))
        {
            // Try to find closest denomination
            int closest = FindClosestDenomination(denomination);
            if (!chipSprites.ContainsKey(closest))
                return null;
            denomination = closest;
        }

        GameObject chipObj = new GameObject($"Chip_{denomination}");
        chipObj.transform.SetParent(transform);
        chipObj.transform.localScale = Vector3.one;
        chipObj.transform.localRotation = Quaternion.identity;

        RectTransform rect = chipObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(chipSize, chipSize);
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = chipObj.AddComponent<Image>();
        image.sprite = chipSprites[denomination];
        image.preserveAspect = true;

        return chipObj;
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
    /// Clear all chip visuals.
    /// </summary>
    public void ClearChips()
    {
        foreach (var chip in activeChips)
        {
            if (chip != null)
                Destroy(chip);
        }
        activeChips.Clear();
    }

    /// <summary>
    /// Get the current displayed amount.
    /// </summary>
    public decimal GetCurrentAmount()
    {
        return currentAmount;
    }

    /// <summary>
    /// Get the world position of the chip stack (for animations).
    /// </summary>
    public Vector3 GetStackPosition()
    {
        return transform.position;
    }

    private void OnDestroy()
    {
        ClearChips();
    }
}
