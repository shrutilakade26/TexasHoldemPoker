using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Animates pot transfer with flying chip effects.
/// </summary>
public class PotAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 1.5f;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("References")]
    [SerializeField] private Transform centerPotPosition;
    [SerializeField] private TextMeshProUGUI centerPotText;
    [SerializeField] private ChipStackAnimator chipAnimator;
    
    [Header("Settings")]
    [SerializeField] private bool useChipAnimations = true;

    private void Awake()
    {
        EnsureChipAnimatorSetup();
    }

    private void Start()
    {
        // Double-check setup in Start (after all Awake calls)
        EnsureChipAnimatorSetup();
    }

    private void EnsureChipAnimatorSetup()
    {
        // Auto-find or create ChipStackAnimator if chip animations are enabled
        if (useChipAnimations && chipAnimator == null)
        {
            chipAnimator = FindFirstObjectByType<ChipStackAnimator>();
            
            if (chipAnimator == null)
            {
                // CRITICAL: ChipStackAnimator creates UI elements, so it MUST be under a Canvas!
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas == null)
                {
                    Debug.LogError("No Canvas found in scene! ChipStackAnimator needs a Canvas parent for UI elements to render!");
                    return;
                }
                
                GameObject animObj = new GameObject("ChipStackAnimator");
                animObj.transform.SetParent(canvas.transform, false);
                chipAnimator = animObj.AddComponent<ChipStackAnimator>();
            }
            else
            {
                // Verify it's under a Canvas
                Canvas parentCanvas = chipAnimator.GetComponentInParent<Canvas>();
                if (parentCanvas == null)
                {
                    Debug.LogWarning("ChipStackAnimator is not under a Canvas! Moving it to Canvas...");
                    Canvas canvas = FindFirstObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        chipAnimator.transform.SetParent(canvas.transform, false);
                    }
                }
            }
        }
        
        // Warn if chip animations are enabled but setup failed
        if (useChipAnimations && chipAnimator == null)
        {
            Debug.LogWarning("PotAnimator: useChipAnimations is TRUE but chipAnimator is still null! Chip animations may not work.");
        }
        
        // Warn if centerPotPosition is not set
        if (centerPotPosition == null)
        {
            Debug.LogWarning("PotAnimator: centerPotPosition is not assigned! Pot animations will not work. Assign it in the Inspector.");
        }
    }

    /// <summary>
    /// Animate chips flying from a bet position to the center pot.
    /// </summary>
    public IEnumerator AnimateBetToPot(Transform betPosition, decimal amount)
    {
        if (betPosition == null || centerPotPosition == null)
            yield break;

        if (useChipAnimations && chipAnimator != null)
        {
            yield return StartCoroutine(chipAnimator.AnimateChipsMoving(
                betPosition.position,
                centerPotPosition.position,
                amount
            ));
        }
    }

    /// <summary>
    /// Animate all player bets flying to center pot.
    /// </summary>
    public IEnumerator AnimateCollectBets(BetDisplay[] betDisplays)
    {
        if (betDisplays == null)
        {
            yield break;
        }
        
        if (!useChipAnimations)
        {
            yield break;
        }
        
        if (chipAnimator == null)
        {
            chipAnimator = FindFirstObjectByType<ChipStackAnimator>();
            
            if (chipAnimator == null)
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas == null)
                {
                    Debug.LogError("No Canvas found! Cannot create ChipStackAnimator!");
                    yield break;
                }
                
                GameObject animObj = new GameObject("ChipStackAnimator");
                animObj.transform.SetParent(canvas.transform, false);
                chipAnimator = animObj.AddComponent<ChipStackAnimator>();
            }
        }
        
        // Start all animations at once (they run in parallel)
        foreach (var betDisplay in betDisplays)
        {
            if (betDisplay != null && betDisplay.GetCurrentBet() > 0)
            {
                StartCoroutine(chipAnimator.AnimateChipsMoving(
                    betDisplay.transform.position,
                    centerPotPosition.position,
                    betDisplay.GetCurrentBet()
                ));
            }
        }

        // Wait for animations to complete
        yield return new WaitForSeconds(0.8f);
    }

    /// <summary>
    /// Animate chips from saved positions to center pot.
    /// Used when bet amounts are captured before UI update clears them.
    /// </summary>
    public IEnumerator AnimateBetsFromPositions(List<(Transform position, decimal amount)> bets)
    {
        if (bets == null || bets.Count == 0)
        {
            yield break;
        }
        
        if (centerPotPosition == null)
        {
            yield break;
        }
        
        // Ensure we have chip animator
        if (chipAnimator == null)
        {
            chipAnimator = FindFirstObjectByType<ChipStackAnimator>();
            if (chipAnimator == null)
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas == null)
                {
                    Debug.LogError("No Canvas found! Cannot create ChipStackAnimator!");
                    yield break;
                }
                
                GameObject animObj = new GameObject("ChipStackAnimator");
                animObj.transform.SetParent(canvas.transform, false);
                chipAnimator = animObj.AddComponent<ChipStackAnimator>();
            }
        }
        
        // Start all animations at once
        foreach (var (position, amount) in bets)
        {
            if (position != null && amount > 0)
            {
                StartCoroutine(chipAnimator.AnimateChipsMoving(
                    position.position,
                    centerPotPosition.position,
                    amount
                ));
            }
        }
        
        // Wait for animations
        yield return new WaitForSeconds(0.8f);
    }

    public IEnumerator AnimatePotToWinner(Transform winnerPosition, decimal amount)
    {
        if (centerPotPosition == null || winnerPosition == null)
        {
            yield break;
        }

        // Ensure chip animator is set up if chip animations are enabled
        if (useChipAnimations && chipAnimator == null)
        {
            chipAnimator = FindFirstObjectByType<ChipStackAnimator>();
            
            if (chipAnimator == null)
            {
                // CRITICAL: Must be under a Canvas for UI elements to render!
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas == null)
                {
                    Debug.LogError("No Canvas found! ChipStackAnimator cannot create UI elements without a Canvas parent!");
                    yield break;
                }
                
                GameObject animObj = new GameObject("ChipStackAnimator");
                animObj.transform.SetParent(canvas.transform, false);
                chipAnimator = animObj.AddComponent<ChipStackAnimator>();
            }
        }

        // Use chip animations
        if (useChipAnimations && chipAnimator != null)
        {
            yield return StartCoroutine(chipAnimator.AnimatePotToWinner(
                centerPotPosition,
                winnerPosition,
                amount
            ));
        }
        else
        {
            // Fallback to text animation
            GameObject potVisual = CreatePotVisual(amount);
            if (potVisual != null)
            {
                yield return StartCoroutine(MovePotVisual(potVisual, centerPotPosition.position, winnerPosition.position));
                Destroy(potVisual);
            }
        }

        // Update center pot display to show 0
        if (centerPotText != null)
        {
            centerPotText.text = "POT: $0";
        }
    }

    private GameObject CreatePotVisual(decimal amount)
    {
        GameObject potVisual = new GameObject("PotTransfer");
        potVisual.transform.SetParent(transform);
        
        var textComponent = potVisual.AddComponent<TextMeshProUGUI>();
        textComponent.text = $"${amount}";
        textComponent.fontSize = 24;
        textComponent.color = Color.yellow;
        textComponent.alignment = TextAlignmentOptions.Center;
        
        potVisual.transform.position = centerPotPosition.position;
        
        return potVisual;
    }

    private IEnumerator MovePotVisual(GameObject potVisual, Vector3 startPos, Vector3 endPos)
    {
        float elapsed = 0f;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / animationDuration;
            float curveValue = movementCurve.Evaluate(progress);
            
            potVisual.transform.position = Vector3.Lerp(startPos, endPos, curveValue);
            
            float scale = 1f + Mathf.Sin(progress * Mathf.PI) * 0.2f;
            potVisual.transform.localScale = Vector3.one * scale;
            
            yield return null;
        }
        
        potVisual.transform.position = endPos;
        potVisual.transform.localScale = Vector3.one;
    }

    public IEnumerator AnimateStackUpdate(Transform playerPosition, decimal oldAmount, decimal newAmount)
    {
        GameObject stackVisual = new GameObject("StackIncrease");
        stackVisual.transform.SetParent(transform);
        
        var textComponent = stackVisual.AddComponent<TextMeshProUGUI>();
        decimal increase = newAmount - oldAmount;
        textComponent.text = $"+${increase}";
        textComponent.fontSize = 28;
        textComponent.fontStyle = FontStyles.Bold;
        textComponent.color = new Color(0.2f, 1f, 0.2f, 1f);
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.outlineWidth = 0.2f;
        textComponent.outlineColor = Color.black;
        
        RectTransform rect = stackVisual.GetComponent<RectTransform>();
        if (rect != null) rect.sizeDelta = new Vector2(200, 50);
        
        stackVisual.transform.position = playerPosition.position;
        
        Vector3 startPos = playerPosition.position;
        Vector3 endPos = startPos + Vector3.up * 80f;
        
        float elapsed = 0f;
        float duration = 2f;
        
        stackVisual.transform.localScale = Vector3.zero;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            
            float moveProgress = Mathf.SmoothStep(0, 1, progress);
            stackVisual.transform.position = Vector3.Lerp(startPos, endPos, moveProgress);
            
            // Scale pop effect
            float scale;
            if (progress < 0.15f)
                scale = Mathf.Lerp(0, 1.3f, progress / 0.15f);
            else if (progress < 0.25f)
                scale = Mathf.Lerp(1.3f, 1f, (progress - 0.15f) / 0.1f);
            else
                scale = 1f;
            stackVisual.transform.localScale = Vector3.one * scale;
            
            // Fade out in last 40%
            if (progress > 0.6f)
            {
                Color color = textComponent.color;
                color.a = 1f - ((progress - 0.6f) / 0.4f);
                textComponent.color = color;
            }
            
            yield return null;
        }
        
        Destroy(stackVisual);
    }
}