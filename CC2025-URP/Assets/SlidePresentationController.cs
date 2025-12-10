using System.Collections;
using UnityEngine;

public class SlidePresentationController : MonoBehaviour
{
    [Header("Slide Easing")]
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Slide Movement")]
    [Tooltip("The object that moves between slides (often your Camera). If empty, this GameObject will be used.")]
    public Transform target;

    [Tooltip("X positions for each slide. Index 0 is slide 0, index 1 is slide 1, etc.")]
    public float[] slideXPositions;

    [Tooltip("How long it takes to slide from one X to another (seconds).")]
    public float moveDuration = 0.5f;

    [Header("Input")]
    public KeyCode nextKey = KeyCode.RightArrow;
    public KeyCode prevKey = KeyCode.LeftArrow;

    [Header("Music")]
    [Tooltip("How long to fade between music groups (seconds).")]
    public float musicFadeDuration = 1.0f;

    [Tooltip("Music groups based on slide index ranges.")]
    public SlideGroup[] slideGroups;

    [System.Serializable]
    public class SlideGroup
    {
        public string name;
        [Tooltip("First slide index in this group (inclusive).")]
        public int startSlideIndex;
        [Tooltip("Last slide index in this group (inclusive).")]
        public int endSlideIndex;
        [Tooltip("Music to play when you are inside this range of slides.")]
        public AudioClip musicClip;
    }

    // Internal state
    private int currentSlideIndex = 0;
    private int currentGroupIndex = -1;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;

    private Coroutine moveRoutine;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (target == null)
        {
            target = transform;
        }

        // Create two AudioSources for crossfading
        sourceA = gameObject.AddComponent<AudioSource>();
        sourceB = gameObject.AddComponent<AudioSource>();

        sourceA.loop = true;
        sourceB.loop = true;

        sourceA.playOnAwake = false;
        sourceB.playOnAwake = false;

        activeSource = sourceA;
    }

    private void Start()
    {
        // Snap to starting slide if we have any
        if (slideXPositions != null && slideXPositions.Length > 0)
        {
            currentSlideIndex = Mathf.Clamp(currentSlideIndex, 0, slideXPositions.Length - 1);
            Vector3 pos = target.position;
            pos.x = slideXPositions[currentSlideIndex];
            target.position = pos;
        }

        // Set initial music (no fade on first play)
        UpdateMusicForCurrentSlide(instant: true);
    }

    private void Update()
    {
        if (slideXPositions == null || slideXPositions.Length == 0)
            return;

        if (Input.GetKeyDown(nextKey))
        {
            GoToSlide(currentSlideIndex + 1);
        }
        else if (Input.GetKeyDown(prevKey))
        {
            GoToSlide(currentSlideIndex - 1);
        }
    }

    /// <summary>
    /// Public method if you ever want to jump to a slide from UI buttons, etc.
    /// </summary>
    public void GoToSlide(int index)
    {
        if (slideXPositions == null || slideXPositions.Length == 0)
            return;

        int clamped = Mathf.Clamp(index, 0, slideXPositions.Length - 1);
        if (clamped == currentSlideIndex)
            return;

        currentSlideIndex = clamped;

        // Move target smoothly to new X
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveToSlideX(slideXPositions[currentSlideIndex]));

        // Update music based on new slide
        UpdateMusicForCurrentSlide(instant: false);
    }

    private IEnumerator MoveToSlideX(float targetX)
    {
        Vector3 startPos = target.position;
        Vector3 endPos = startPos;
        endPos.x = targetX;

        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float normalized = moveDuration > 0f ? Mathf.Clamp01(t / moveDuration) : 1f;

            // Use the curve to control easing
            float eased = movementCurve.Evaluate(normalized);

            target.position = Vector3.Lerp(startPos, endPos, eased);
            yield return null;
        }

        target.position = endPos;
    }


    private void UpdateMusicForCurrentSlide(bool instant)
    {
        int newGroupIndex = GetGroupIndexForSlide(currentSlideIndex);
        if (newGroupIndex == currentGroupIndex)
            return; // still in same group, no change

        currentGroupIndex = newGroupIndex;

        AudioClip newClip = null;
        if (currentGroupIndex != -1 && slideGroups != null && currentGroupIndex < slideGroups.Length)
        {
            newClip = slideGroups[currentGroupIndex].musicClip;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(CrossfadeToClip(newClip, instant ? 0f : musicFadeDuration));
    }

    private int GetGroupIndexForSlide(int slideIndex)
    {
        if (slideGroups == null) return -1;

        for (int i = 0; i < slideGroups.Length; i++)
        {
            if (slideIndex >= slideGroups[i].startSlideIndex &&
                slideIndex <= slideGroups[i].endSlideIndex)
            {
                return i;
            }
        }

        return -1; // no group for this slide
    }

    private IEnumerator CrossfadeToClip(AudioClip newClip, float duration)
    {
        AudioSource from = activeSource;
        AudioSource to = (activeSource == sourceA) ? sourceB : sourceA;

        // No music for this group: just fade out current
        if (newClip == null)
        {
            if (from.clip == null || !from.isPlaying || duration <= 0f)
            {
                if (from.isPlaying) from.Stop();
                from.clip = null;
                yield break;
            }

            float startVol = from.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / duration);
                from.volume = Mathf.Lerp(startVol, 0f, lerp);
                yield return null;
            }

            from.Stop();
            from.clip = null;
            from.volume = startVol;
            yield break;
        }

        // If we’re already playing this clip, don’t do anything
        if (from.clip == newClip)
            yield break;

        // Prepare "to" source
        to.Stop();
        to.clip = newClip;
        to.volume = 0f;
        to.Play();

        if (duration <= 0f)
        {
            // Instant switch
            if (from.isPlaying) from.Stop();
            from.clip = null;
            to.volume = 1f;
            activeSource = to;
            yield break;
        }

        float fromStartVol = from.clip != null ? from.volume : 0f;
        float t2 = 0f;

        while (t2 < duration)
        {
            t2 += Time.deltaTime;
            float lerp = Mathf.Clamp01(t2 / duration);

            if (from.clip != null)
                from.volume = Mathf.Lerp(fromStartVol, 0f, lerp);

            to.volume = Mathf.Lerp(0f, 1f, lerp);
            yield return null;
        }

        if (from.clip != null)
        {
            from.Stop();
            from.clip = null;
        }

        to.volume = 1f;
        activeSource = to;
    }
}
