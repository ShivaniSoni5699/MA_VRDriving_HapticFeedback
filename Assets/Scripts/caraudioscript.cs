using System.Collections;
using UnityEngine;

public class CarStartIdleAudio : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource startSource;   // 1-shot start clip (Play On Awake = OFF)
    public AudioSource idleSource;    // looping idle clip (Play On Awake = OFF, Loop = ON)

    [Header("Timing & Mixing")]
    [Tooltip("Extra delay after start clip before idle begins (seconds).")]
    public float idleDelayAfterStart = 0f;
    [Tooltip("Fade time when stopping idle after first input.")]
    public float idleFadeOut = 0.2f;

    private bool hasInteracted;
    private Coroutine idleFadeRoutine;

    void Start()
    {
        // Safety resets in case Play On Awake was left on in the inspector
        if (startSource) startSource.Stop();
        if (idleSource)
        {
            idleSource.Stop();
            idleSource.loop = true;
        }

        StartCoroutine(StartThenIdle());
    }

    IEnumerator StartThenIdle()
    {
        // Play start immediately
        if (startSource && startSource.clip)
        {
            startSource.Play();
            yield return new WaitForSeconds(startSource.clip.length + Mathf.Max(0f, idleDelayAfterStart));
        }

        // Begin idle only if the player hasn't interacted yet
        if (!hasInteracted && idleSource && idleSource.clip)
        {
            idleSource.Play();
        }
    }

    void Update()
    {
        if (hasInteracted) return;

        if (PressedThisFrame())
        {
            hasInteracted = true;

            // Stop (or fade out) idle the instant there is input
            if (idleSource && idleSource.isPlaying)
            {
                if (idleFadeOut > 0f)
                {
                    if (idleFadeRoutine != null) StopCoroutine(idleFadeRoutine);
                    idleFadeRoutine = StartCoroutine(FadeOut(idleSource, idleFadeOut));
                }
                else
                {
                    idleSource.Stop();
                }
            }
        }
    }

    bool PressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        // New Input System (Keyboard/Gamepad/Mouse)
        var kbd = UnityEngine.InputSystem.Keyboard.current;
        if (kbd != null && kbd.anyKey.wasPressedThisFrame) return true;

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame ||
                              mouse.rightButton.wasPressedThisFrame ||
                              mouse.middleButton.wasPressedThisFrame)) return true;

        var gamepad = UnityEngine.InputSystem.Gamepad.current;
        if (gamepad != null)
        {
            // Any gamepad button
            foreach (var c in gamepad.allControls)
            {
                if (c is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame)
                    return true;
            }
        }
#endif
        // Legacy Input Manager fallback
        return Input.anyKeyDown;
    }

    IEnumerator FadeOut(AudioSource src, float time)
    {
        float startVol = src.volume;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(startVol, 0f, t / time);
            yield return null;
        }
        src.Stop();
        src.volume = startVol; // restore for next time
    }
}
