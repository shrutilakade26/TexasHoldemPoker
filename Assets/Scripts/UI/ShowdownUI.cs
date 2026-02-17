using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Handles showdown UI with professional winner panel display and animations.
/// </summary>
public class ShowdownUI : MonoBehaviour
{
    [Header("Winner Panel")]
    [SerializeField] private GameObject winnerPanel;
    [SerializeField] private TextMeshProUGUI winnerNameText;
    [SerializeField] private TextMeshProUGUI winnerAmountText;
    [SerializeField] private TextMeshProUGUI winnerHandText;
    [SerializeField] private Image winnerPanelBackground;
    
    [Header("Visual Effects")]
    [SerializeField] private Image glowEffect;
    [SerializeField] private ParticleSystem confettiEffect;
    [SerializeField] private Image flashOverlay;

    [Header("Countdown Panel")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("Animation Settings")]
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private float slideInDuration = 0.5f;
    [SerializeField] private float glowPulseSpeed = 2f;
    [SerializeField] private AnimationCurve slideInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Colors")]
    [SerializeField] private Color winnerGlowColor = new Color(1f, 0.84f, 0f, 1f); // Gold
    [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 0.5f);

    private Coroutine glowCoroutine;

    private void Start()
    {
        // Hide panels initially
        if (winnerPanel != null) winnerPanel.SetActive(false);
        if (countdownPanel != null) countdownPanel.SetActive(false);
        if (flashOverlay != null) flashOverlay.gameObject.SetActive(false);
    }

    public IEnumerator ShowWinner(string playerName, decimal amount, string handName)
    {
        if (winnerPanel == null)
        {
            Debug.LogWarning("Winner panel not assigned!");
            yield return new WaitForSeconds(displayDuration);
            yield break;
        }

        // Set winner information (removed emoji to avoid TMP font warnings)
        if (winnerNameText != null)
            winnerNameText.text = $"{playerName} WINS!";
        
        if (winnerAmountText != null)
            winnerAmountText.text = $"${amount}";
        
        if (winnerHandText != null)
            winnerHandText.text = handName;

        // Flash effect
        yield return StartCoroutine(FlashScreen());

        // Show panel with slide-in animation
        yield return StartCoroutine(SlideInPanel());

        // Start glow pulse effect
        if (glowEffect != null)
        {
            glowCoroutine = StartCoroutine(PulseGlow());
        }

        // Play confetti
        if (confettiEffect != null)
        {
            confettiEffect.Play();
        }

        // Scale pulse animation
        yield return StartCoroutine(PulseScale(winnerPanel.transform));

        // Display for duration (but keep visible during chip animation)
        yield return new WaitForSeconds(displayDuration - 1f);

        // Stop glow (but keep panel visible for chip animation)
        if (glowCoroutine != null)
        {
            StopCoroutine(glowCoroutine);
        }

        // Don't slide out yet - keep panel visible during chip animation
        // Panel will be hidden by HideWinnerPanel() after chip animation completes
    }

    /// <summary>
    /// Hide the winner panel after chip animation completes.
    /// </summary>
    public void HideWinnerPanel()
    {
        if (winnerPanel != null && winnerPanel.activeSelf)
        {
            StartCoroutine(SlideOutAndHide());
        }
    }

    private IEnumerator SlideOutAndHide()
    {
        // Slide out
        yield return StartCoroutine(SlideOutPanel());

        // Hide panel
        winnerPanel.SetActive(false);
    }

    private IEnumerator FlashScreen()
    {
        if (flashOverlay == null) yield break;

        flashOverlay.gameObject.SetActive(true);
        flashOverlay.color = flashColor;

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(flashColor.a, 0f, elapsed / duration);
            Color color = flashColor;
            color.a = alpha;
            flashOverlay.color = color;
            yield return null;
        }

        flashOverlay.gameObject.SetActive(false);
    }

    private IEnumerator SlideInPanel()
    {
        winnerPanel.SetActive(true);
        
        RectTransform rectTransform = winnerPanel.GetComponent<RectTransform>();
        if (rectTransform == null) yield break;

        Vector2 startPos = new Vector2(0, Screen.height);
        Vector2 endPos = Vector2.zero;
        
        rectTransform.anchoredPosition = startPos;

        float elapsed = 0f;

        while (elapsed < slideInDuration)
        {
            elapsed += Time.deltaTime;
            float t = slideInCurve.Evaluate(elapsed / slideInDuration);
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        rectTransform.anchoredPosition = endPos;
    }

    private IEnumerator SlideOutPanel()
    {
        RectTransform rectTransform = winnerPanel.GetComponent<RectTransform>();
        if (rectTransform == null) yield break;

        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 endPos = new Vector2(0, -Screen.height);

        float elapsed = 0f;

        while (elapsed < slideInDuration)
        {
            elapsed += Time.deltaTime;
            float t = slideInCurve.Evaluate(elapsed / slideInDuration);
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }
    }

    private IEnumerator PulseGlow()
    {
        if (glowEffect == null) yield break;

        glowEffect.gameObject.SetActive(true);
        float timer = 0f;

        while (true)
        {
            timer += Time.deltaTime * glowPulseSpeed;
            float alpha = (Mathf.Sin(timer) + 1f) / 2f; // 0 to 1
            alpha = Mathf.Lerp(0.3f, 0.8f, alpha);

            Color color = winnerGlowColor;
            color.a = alpha;
            glowEffect.color = color;

            yield return null;
        }
    }

    private IEnumerator PulseScale(Transform target)
    {
        if (target == null) yield break;

        Vector3 originalScale = target.localScale;
        float pulseDuration = 0.4f;

        // Scale up
        float elapsed = 0f;
        while (elapsed < pulseDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pulseDuration;
            float scale = Mathf.Lerp(1f, 1.15f, t);
            target.localScale = originalScale * scale;
            yield return null;
        }

        // Scale back
        elapsed = 0f;
        while (elapsed < pulseDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pulseDuration;
            float scale = Mathf.Lerp(1.15f, 1f, t);
            target.localScale = originalScale * scale;
            yield return null;
        }

        target.localScale = originalScale;
    }

    public IEnumerator ShowFoldWinner(string playerName, decimal amount)
    {
        yield return StartCoroutine(ShowWinner(playerName, amount, "All Others Folded"));
    }

    public IEnumerator ShowCountdown()
    {
        if (countdownPanel == null || countdownText == null) 
        {
            Debug.LogWarning("Countdown panel not assigned! Skipping countdown.");
            yield return new WaitForSeconds(3f);
            yield break;
        }

        // Show countdown panel with fade in
        countdownPanel.SetActive(true);
        CanvasGroup canvasGroup = countdownPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = countdownPanel.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        float fadeTime = 0.3f;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeTime);
            yield return null;
        }

        // Countdown from 3 to 1
        for (int i = 3; i >= 1; i--)
        {
            countdownText.text = i.ToString();
            
            // Scale pulse for each number
            countdownText.transform.localScale = Vector3.one * 1.5f;
            float scaleDuration = 0.3f;
            elapsed = 0f;
            
            while (elapsed < scaleDuration)
            {
                elapsed += Time.deltaTime;
                float scale = Mathf.Lerp(1.5f, 1f, elapsed / scaleDuration);
                countdownText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            
            yield return new WaitForSeconds(0.7f);
        }

        // "Starting!" message
        countdownText.text = "GO!";
        countdownText.transform.localScale = Vector3.one * 1.5f;
        yield return new WaitForSeconds(0.5f);

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            yield return null;
        }

        // Hide countdown panel
        countdownPanel.SetActive(false);
        countdownText.transform.localScale = Vector3.one;
    }
}
