using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(TMP_Text))]
public class ComboUI : MonoBehaviour
{
    public static ComboUI Instance { get; private set; }

    private TMP_Text comboText;
    private Vector3 baseScale;

    [Header("Combo Settings")]
    public float displayDuration = 1.5f; // How long it stays before fading
    public float fadeSpeed = 3f;         // How fast it fades out
    public float bumpScale = 1.4f;       // How big the heartbeat pop gets

    private float fadeDelayTimer = 0f;
    private bool isFading = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        comboText = GetComponent<TMP_Text>();
        baseScale = transform.localScale;

        // Start completely invisible
        SetAlpha(0f);
    }

    // Call this from GameManager whenever the combo goes up!
    public void UpdateCombo(int currentCombo)
    {
        // Optional: Don't show a combo if it's less than 2
        if (currentCombo < 2)
        {
            SetAlpha(0f);
            return;
        }

        comboText.text = "x" + currentCombo;
        SetAlpha(1f);

        // Reset timers
        fadeDelayTimer = displayDuration;
        isFading = false;

        // Trigger the heartbeat bump
        StopAllCoroutines();
        StartCoroutine(HeartbeatBump());
    }

    // Call this to force clear it (e.g., when the player misses)
    public void BreakCombo()
    {
        isFading = true;
        fadeDelayTimer = 0f;
    }

    void Update()
    {
        // Countdown the display timer
        if (fadeDelayTimer > 0)
        {
            fadeDelayTimer -= Time.deltaTime;
            if (fadeDelayTimer <= 0)
            {
                isFading = true;
            }
        }

        // Fade out alpha
        if (isFading && comboText.color.a > 0)
        {
            SetAlpha(comboText.color.a - (fadeSpeed * Time.deltaTime));
        }
    }

    void SetAlpha(float alpha)
    {
        Color c = comboText.color;
        c.a = Mathf.Clamp01(alpha);
        comboText.color = c;
    }

    IEnumerator HeartbeatBump()
    {
        float t = 0;
        float popUpTime = 0.05f;  // Really fast pop up
        float settleTime = 0.15f; // Slightly slower settle down

        // Pop up
        while (t < popUpTime)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(baseScale, baseScale * bumpScale, t / popUpTime);
            yield return null;
        }

        // Settle down
        t = 0;
        while (t < settleTime)
        {
            t += Time.deltaTime;
            // Using a slight ease-out for the settle looks more organic
            float lerpT = t / settleTime;
            lerpT = Mathf.Sin(lerpT * Mathf.PI * 0.5f);

            transform.localScale = Vector3.Lerp(baseScale * bumpScale, baseScale, lerpT);
            yield return null;
        }

        transform.localScale = baseScale;
    }
}