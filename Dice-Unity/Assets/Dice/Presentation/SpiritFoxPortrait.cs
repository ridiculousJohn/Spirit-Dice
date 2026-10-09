using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    public sealed class SpiritFoxPortrait : MonoBehaviour
    {
        public const float ReactionDuration = 1.8f;
        private const float HeadSize = 190f;
        private readonly Vector2 bodyPosition = new Vector2(300f, 724f);
        private readonly Vector2 headPosition = new Vector2(232f, 792f);
        private RectTransform portraitRoot;
        private RectTransform bodyRect;
        private RectTransform physicalHead;
        private RectTransform spiritHead;
        private RawImage physicalFront;
        private RawImage physicalTurn;
        private RawImage cyanFront;
        private RawImage cyanTurn;
        private RawImage goldFront;
        private RawImage goldTurn;
        private SpiritPortraitImage physicalBody;
        private SpiritPortraitImage cyanTail;
        private SpiritPortraitImage goldTail;
        private Material silhouetteMaterial;
        private float idleAge;
        private float nextEarTwitch = 4.1f;
        private float nextTailSwish = 9.2f;
        private float earAge = -1f;
        private float tailAge = -1f;
        private float reactionAge = -1f;
        private int reactionCount;
        private int earCount;
        private int tailCount;
        private float physicalPose;
        private float physicalTail;
        private float physicalEars;
        private float initialPose;
        private float initialTail;
        private float initialEars;

        public bool IsReacting => reactionAge >= 0f;
        public event Action SpiritGesture;

        public void Build(RectTransform parent, Texture2D body, Texture2D head, Texture2D turnedHead)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (portraitRoot != null) return;

            // Authored positions share the room's 540 x 960 coordinate space and table contact line.
            portraitRoot = Rect("Spirit Fox Portrait", parent, Vector2.zero, Vector2.zero);
            portraitRoot.pivot = Vector2.zero;
            portraitRoot.sizeDelta = new Vector2(540f, 960f);
            FitPortrait();
            Shader shader = Resources.Load<Shader>("SpiritSilhouette") ?? Shader.Find("Dice/Spirit Silhouette");
            if (shader != null)
                silhouetteMaterial = new Material(shader) { name = "Spirit Fox Silhouette", hideFlags = HideFlags.DontSave };

            goldTail = TailSilhouette("Gold Tail", body);
            cyanTail = TailSilhouette("Cyan Tail", body);
            physicalBody = (SpiritPortraitImage)Picture("Fox Body", portraitRoot, body, new Vector2(460f, 460f * 2f / 3f));
            physicalBody.DeformationRegion = SpiritPortraitImage.Region.Body;
            bodyRect = physicalBody.rectTransform;
            bodyRect.anchorMin = bodyRect.anchorMax = Vector2.zero;
            bodyRect.anchoredPosition = bodyPosition;

            spiritHead = HeadRoot("Spirit Head", portraitRoot);
            goldFront = Silhouette("Gold Front", spiritHead, head, new Vector2(2f, -1f));
            goldTurn = Silhouette("Gold Turn", spiritHead, turnedHead, new Vector2(2f, -1f));
            cyanFront = Silhouette("Cyan Front", spiritHead, head, Vector2.zero);
            cyanTurn = Silhouette("Cyan Turn", spiritHead, turnedHead, Vector2.zero);

            physicalHead = HeadRoot("Physical Head", portraitRoot);
            physicalFront = Picture("Fox Head", physicalHead, head, new Vector2(HeadSize, HeadSize));
            physicalTurn = Picture("Fox Turned Head", physicalHead, turnedHead, new Vector2(HeadSize, HeadSize));
            ApplyPose();
        }

        public void React()
        {
            if (portraitRoot == null || !portraitRoot.gameObject.activeInHierarchy) return;
            initialPose = physicalPose;
            initialTail = physicalTail;
            initialEars = physicalEars;
            reactionAge = 0f;
            reactionCount++;
            earAge = tailAge = -1f;
            nextEarTwitch = idleAge + 4.1f + reactionCount % 3 * 0.7f;
            nextTailSwish = idleAge + 9.2f + reactionCount % 3 * 1.3f;
            ApplyPose();
            SpiritGesture?.Invoke();
        }

        public void SetVisible(bool visible)
        {
            if (portraitRoot == null) return;
            if (portraitRoot.gameObject.activeSelf == visible) return;
            ResetPose();
            portraitRoot.gameObject.SetActive(visible);
        }

        public void ResetPose()
        {
            idleAge = 0f;
            reactionAge = -1f;
            earAge = tailAge = -1f;
            reactionCount = earCount = tailCount = 0;
            initialPose = initialTail = initialEars = 0f;
            nextEarTwitch = 4.1f;
            nextTailSwish = 9.2f;
            ApplyPose();
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        public void Advance(float deltaTime)
        {
            if (portraitRoot == null || !portraitRoot.gameObject.activeInHierarchy
                || deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            FitPortrait();
            idleAge += deltaTime;
            if (reactionAge >= 0f)
            {
                reactionAge += deltaTime;
                if (reactionAge >= ReactionDuration) reactionAge = -1f;
            }
            else
            {
                if (idleAge >= nextEarTwitch)
                {
                    earAge = idleAge - nextEarTwitch;
                    nextEarTwitch += 5.3f + earCount++ % 3 * 0.8f;
                }
                else if (earAge >= 0f) earAge += deltaTime;
                if (idleAge >= nextTailSwish)
                {
                    tailAge = idleAge - nextTailSwish;
                    nextTailSwish += 10.8f + tailCount++ % 3 * 1.1f;
                    if (tailAge < 1.88f) SpiritGesture?.Invoke();
                }
                else if (tailAge >= 0f) tailAge += deltaTime;
                if (earAge >= 0.52f) earAge = -1f;
                if (tailAge >= 1.88f) tailAge = -1f;
            }
            ApplyPose();
        }

        private void FitPortrait()
        {
            var parent = (RectTransform)portraitRoot.parent;
            const float scale = 1.16f;
            Vector2 fit = new Vector2(parent.rect.width / 540f, parent.rect.height / 960f);
            // Scale every layer about the planted contact, then align it with the room's table edge.
            portraitRoot.localScale = new Vector3(fit.x * scale, fit.y * scale, 1f);
            portraitRoot.anchoredPosition = Vector2.Scale(new Vector2(270f, 595.2f)
                - new Vector2(270f, 624f) * scale, fit);
        }

        private void ApplyPose()
        {
            if (physicalHead == null) return;
            float spiritPose = 0f;
            float spiritAlpha = 0f;
            float spiritTail = tailAge >= 0f ? Gesture(tailAge, 1.7f) * 0.55f : 0f;
            physicalPose = 0f;
            physicalTail = tailAge >= 0f ? Gesture(tailAge - 0.18f, 1.7f) * 0.55f : 0f;
            physicalEars = earAge >= 0f ? Gesture(earAge, 0.52f) * 0.55f : 0f;
            if (reactionAge >= 0f)
            {
                // Each spirit gesture leads its physical counterpart by 0.18 seconds.
                spiritPose = Mathf.Lerp(initialPose, 1f, Ease(reactionAge, 0f, 0.3f))
                    * (1f - Ease(reactionAge, 1.15f, 1.55f));
                physicalPose = Mathf.Lerp(initialPose, 1f, Ease(reactionAge, 0.18f, 0.48f))
                    * (1f - Ease(reactionAge, 1.33f, 1.73f));
                spiritTail = ScoredTail(reactionAge, initialTail);
                physicalTail = ScoredTail(reactionAge - 0.18f, initialTail);
                physicalEars = initialEars * (1f - Ease(reactionAge, 0f, 0.22f))
                    + Gesture(reactionAge - 0.18f, 0.65f) * 0.7f;
                float outgoing = Ease(reactionAge, 0f, 0.05f) * (1f - Ease(reactionAge, 0.3f, 0.6f));
                float returning = Ease(reactionAge, 1.15f, 1.2f) * (1f - Ease(reactionAge, 1.63f, 1.8f));
                spiritAlpha = Mathf.Max(outgoing, returning);
            }

            float breathing = Mathf.Sin(idleAge * 1.15f);
            Vector2 idleOffset = new Vector2(0f, breathing * 0.65f);
            // Planted elbows stay at the table while the independently layered head moves.
            bodyRect.anchoredPosition = bodyPosition;
            physicalBody.SetPose(breathing * 0.65f, physicalTail, 0f);
            physicalHead.anchoredPosition = headPosition + idleOffset + new Vector2(12f, 1f) * physicalPose;
            physicalHead.localRotation = Quaternion.Euler(0f, 0f, -6f * physicalPose + breathing * 0.18f);
            ((SpiritPortraitImage)physicalFront).SetPose(0f, 0f, physicalEars);
            ((SpiritPortraitImage)physicalTurn).SetPose(0f, 0f, physicalEars);

            float lead = spiritPose - physicalPose;
            spiritHead.anchoredPosition = headPosition + idleOffset + new Vector2(12f, 1f) * spiritPose
                + new Vector2(lead * 6f, Mathf.Abs(lead) * 5f);
            spiritHead.localRotation = Quaternion.Euler(0f, 0f, -6f * spiritPose - lead * 3f + breathing * 0.18f);

            physicalFront.color = new Color(1f, 1f, 1f, 1f - physicalPose);
            physicalTurn.color = new Color(1f, 1f, 1f, physicalPose);
            SetGhostColor(cyanFront, cyanTurn, new Color(0.46f, 0.88f, 1f), spiritAlpha * 0.24f, spiritPose);
            SetGhostColor(goldFront, goldTurn, new Color(1f, 0.76f, 0.37f), spiritAlpha * 0.12f, spiritPose);
            cyanTail.SetPose(0f, spiritTail, 0f);
            goldTail.SetPose(0f, spiritTail, 0f);
            float tailAlpha = Mathf.Abs(spiritTail - physicalTail)
                * Ease(reactionAge >= 0f ? reactionAge : tailAge, 0f, 0.05f);
            cyanTail.color = new Color(0.46f, 0.88f, 1f, tailAlpha * (IsReacting ? 0.23f : 0.16f));
            goldTail.color = new Color(1f, 0.76f, 0.37f, tailAlpha * (IsReacting ? 0.11f : 0.075f));
        }

        private static float Gesture(float age, float duration)
        {
            if (age <= 0f || age >= duration) return 0f;
            float progress = age / duration;
            return Mathf.Sin(progress * Mathf.PI * 2f) * Mathf.Sin(progress * Mathf.PI);
        }

        private static float ScoredTail(float age, float start)
        {
            return Mathf.Lerp(start, 0.9f, Ease(age, 0f, 0.28f))
                - Ease(age, 0.36f, 0.74f) * 1.22f + Ease(age, 0.86f, 1.3f) * 0.32f;
        }

        private static void SetGhostColor(RawImage front, RawImage turned, Color color, float alpha, float pose)
        {
            color.a = alpha * (1f - pose);
            front.color = color;
            color.a = alpha * pose;
            turned.color = color;
        }

        private static float Ease(float age, float start, float end)
        {
            float progress = Mathf.Clamp01((age - start) / (end - start));
            return progress * progress * (3f - 2f * progress);
        }

        private static RectTransform Rect(string objectName, Transform parent, Vector2 min, Vector2 max)
        {
            RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform HeadRoot(string objectName, Transform parent)
        {
            RectTransform rect = Rect(objectName, parent, Vector2.zero, Vector2.zero);
            rect.sizeDelta = new Vector2(HeadSize, HeadSize);
            return rect;
        }

        private static RawImage Picture(string objectName, Transform parent, Texture texture, Vector2 size)
        {
            RectTransform rect = Rect(objectName, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rect.sizeDelta = size;
            var picture = rect.gameObject.AddComponent<SpiritPortraitImage>();
            picture.DeformationRegion = SpiritPortraitImage.Region.Head;
            picture.texture = texture;
            picture.raycastTarget = false;
            picture.enabled = texture != null;
            return picture;
        }

        private SpiritPortraitImage TailSilhouette(string objectName, Texture texture)
        {
            var picture = (SpiritPortraitImage)Picture(objectName, portraitRoot, texture, new Vector2(460f, 460f * 2f / 3f));
            picture.DeformationRegion = SpiritPortraitImage.Region.Body;
            picture.TailOnly = true;
            picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = Vector2.zero;
            picture.rectTransform.anchoredPosition = bodyPosition;
            picture.material = silhouetteMaterial;
            picture.enabled = texture != null && silhouetteMaterial != null;
            return picture;
        }

        private RawImage Silhouette(string objectName, Transform parent, Texture texture, Vector2 offset)
        {
            RawImage picture = Picture(objectName, parent, texture, new Vector2(HeadSize, HeadSize));
            picture.rectTransform.anchoredPosition = offset;
            picture.material = silhouetteMaterial;
            picture.enabled = texture != null && silhouetteMaterial != null;
            return picture;
        }

        private void OnDestroy()
        {
            if (portraitRoot != null) DestroyOwned(portraitRoot.gameObject);
            if (silhouetteMaterial != null) DestroyOwned(silhouetteMaterial);
        }

        private static void DestroyOwned(UnityEngine.Object owned)
        {
            if (Application.isPlaying) Destroy(owned);
            else DestroyImmediate(owned);
        }
    }
}
