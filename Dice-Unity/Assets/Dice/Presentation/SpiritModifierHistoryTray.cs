using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** The current hand's modifier activations form an ordered, scrollable tray beneath the dice. */
    public sealed class SpiritModifierHistoryTray : MonoBehaviour
    {
        public const float TokenSize = 40;
        public const float ArrivalDuration = .30f;
        private const float TokenSpacing = 42;
        [SerializeField] private SpiritTrayPresentationAsset trayPresentation;
        private readonly List<ModifierContribution> contributions = new List<ModifierContribution>();
        private readonly List<Token> tokens = new List<Token>();
        private IReadOnlyList<ModifierContribution> readOnlyContributions;
        private RectTransform root;
        private RectTransform content;
        private ScrollRect scroll;
        private bool interactable = true;
        private bool scrollToLatest;
        private string encounterId;
        private int stageIndex;
        private int handIndex;
        private SpiritTrayScene sharedScene;
        private RectTransform frame;
        private RawImage image;

        public int Count => contributions.Count;
        public IReadOnlyList<ModifierContribution> Contributions
            => readOnlyContributions ?? (readOnlyContributions = contributions.AsReadOnly());
        public RectTransform Content => content;
        public ScrollRect Scroll => scroll;
        public SpiritModifierHistoryRenderer Renderer { get; private set; }
        public event Action<ModifierContribution, ModifierDefinition> TokenSelected;

        private sealed class Token
        {
            internal RectTransform Rect;
            internal Button Button;
        }

        public void Configure(SpiritTrayPresentationAsset presentation)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            presentation.Validate();
            if (trayPresentation != presentation || sharedScene != null) ReleasePresentation();
            trayPresentation = presentation;
            Renderer?.Configure(presentation);
        }

        public void Configure(SpiritTrayPresentationAsset presentation, SpiritTrayScene scene)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            presentation.ValidateScene();
            if (scene.Presentation != presentation)
                throw new InvalidOperationException("The modifier tray must use the shared scene's presentation.");
            if (trayPresentation != presentation || sharedScene != scene) ReleasePresentation();
            trayPresentation = presentation;
            sharedScene = scene;
            Renderer?.Configure(presentation, scene);
        }

        public void ReleasePresentation()
        {
            Clear();
            Renderer?.ReleasePresentation();
            if (image != null) { image.texture = null; image.enabled = false; }
            sharedScene = null;
        }

        public void Build(RectTransform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (root != null)
            {
                if (root.parent != parent) throw new InvalidOperationException("The modifier tray already has a parent.");
                InitializeRenderer();
                return;
            }

            if (trayPresentation == null) throw new InvalidOperationException("Configure the modifier tray presentation before building.");
            trayPresentation.Validate();
            root = SpiritUi.Full("ModifierHistoryTray", parent);
            Renderer = gameObject.AddComponent<SpiritModifierHistoryRenderer>();

            frame = SpiritUi.Rect("HistoryScroll", root, new Vector2(.055f, .06f), new Vector2(.945f, .97f));
            scroll = frame.gameObject.AddComponent<ScrollRect>();
            var viewport = SpiritUi.Full("Viewport", frame);
            var hitSurface = viewport.gameObject.AddComponent<Image>();
            hitSurface.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = SpiritUi.Rect("Tokens", viewport, Vector2.zero, new Vector2(0, 1));
            content.pivot = new Vector2(0, .5f);
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = TokenSpacing;
            scroll.decelerationRate = .12f;
            InitializeRenderer();
            ResizeContent();
        }

        private void InitializeRenderer()
        {
            if (sharedScene != null) Renderer.Configure(trayPresentation, sharedScene);
            else Renderer.Configure(trayPresentation);
            Renderer.Initialize();
            if (sharedScene == null)
            {
                if (image == null) image = root.gameObject.AddComponent<RawImage>();
                image.texture = Renderer.Texture;
                image.raycastTarget = false;
                image.enabled = true;
                frame.anchorMin = new Vector2(.055f, .06f);
                frame.anchorMax = new Vector2(.945f, .97f);
            }
            else
            {
                if (image != null) image.enabled = false;
                UpdateBayFrame();
            }
        }

        private void UpdateBayFrame()
        {
            if (sharedScene == null || Renderer == null || frame == null) return;
            Rect bounds = Renderer.GetBayViewportRect();
            frame.anchorMin = bounds.min;
            frame.anchorMax = bounds.max;
            frame.offsetMin = Vector2.zero;
            frame.offsetMax = Vector2.zero;
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            foreach (Token token in tokens) token.Button.interactable = value;
        }

        public void Clear()
        {
            if (Renderer != null) Renderer.Clear();
            foreach (Token token in tokens)
            {
                token.Rect.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(token.Rect.gameObject);
                else DestroyImmediate(token.Rect.gameObject);
            }
            tokens.Clear();
            contributions.Clear();
            encounterId = null;
            scrollToLatest = false;
            if (scroll != null)
            {
                scroll.StopMovement();
                content.anchoredPosition = Vector2.zero;
                ResizeContent();
            }
        }

        public void Add(ModifierContribution contribution, ModifierDefinition definition, bool animate)
        {
            if (root == null) throw new InvalidOperationException("Build the modifier tray before adding activations.");
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (contribution.DefinitionId != definition.Id)
                throw new ArgumentException("The modifier definition must match the activation.", nameof(definition));

            Renderer.Add(contribution, definition, animate);
            var rect = SpiritUi.Rect("ModifierToken" + contributions.Count, content,
                new Vector2(0, .5f), new Vector2(0, .5f));
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(TokenSize, TokenSize);
            var graphic = rect.gameObject.AddComponent<Image>();
            graphic.color = Color.clear;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.interactable = interactable;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => TokenSelected?.Invoke(contribution, definition));

            var token = new Token
            {
                Rect = rect,
                Button = button
            };
            contributions.Add(contribution);
            tokens.Add(token);
            ResizeContent();
            if (animate) scrollToLatest = true;
        }

        public void Synchronize(EncounterSnapshot snapshot, MechanicalCatalog catalog)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (root == null) throw new InvalidOperationException("Build the modifier tray before synchronizing activations.");

            bool sameHand = encounterId == null || (encounterId == snapshot.EncounterId &&
                stageIndex == snapshot.StageIndex && handIndex == snapshot.HandIndex);
            bool prefixMatches = sameHand && contributions.Count <= snapshot.ModifierContributions.Count;
            if (prefixMatches)
                for (int i = 0; i < contributions.Count; ++i)
                    if (!Matches(contributions[i], snapshot.ModifierContributions[i])) { prefixMatches = false; break; }
            if (!prefixMatches) Clear();
            encounterId = snapshot.EncounterId;
            stageIndex = snapshot.StageIndex;
            handIndex = snapshot.HandIndex;
            for (int i = contributions.Count; i < snapshot.ModifierContributions.Count; ++i)
            {
                ModifierContribution contribution = snapshot.ModifierContributions[i];
                Add(contribution, catalog.GetModifier(contribution.DefinitionId), false);
            }
        }

        private static bool Matches(ModifierContribution first, ModifierContribution second)
            => first.InstanceId == second.InstanceId && first.DefinitionId == second.DefinitionId &&
                first.Phase == second.Phase && first.BaseScore == second.BaseScore &&
                first.Multiplier == second.Multiplier && first.Rerolls == second.Rerolls;

        public void Advance(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (Renderer != null) Renderer.Advance(deltaTime);
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        private void LateUpdate()
        {
            UpdateBayFrame();
            ResizeContent();
            if (scroll == null || Renderer == null) return;
            if (scrollToLatest)
            {
                scroll.StopMovement();
                scroll.horizontalNormalizedPosition = 1;
                scrollToLatest = false;
            }
            Renderer.SetScroll(scroll.horizontalNormalizedPosition);
            // Invisible pointer targets follow the projected dice while the frame stays fixed.
            for (int index = 0; index < tokens.Count; ++index)
            {
                Rect bounds = Renderer.GetDieViewportRect(index);
                tokens[index].Rect.gameObject.SetActive(bounds.width > 0 && bounds.height > 0);
                if (bounds.width <= 0 || bounds.height <= 0) continue;
                Vector2 center = root.rect.min + Vector2.Scale(bounds.center, root.rect.size);
                tokens[index].Rect.position = root.TransformPoint(center);
                tokens[index].Rect.sizeDelta = Vector2.Scale(bounds.size, root.rect.size);
            }
        }

        private void ResizeContent()
        {
            if (content == null || scroll == null) return;
            content.sizeDelta = new Vector2(scroll.viewport.rect.width +
                Mathf.Max(0, contributions.Count - (Renderer != null ? Renderer.Capacity : SpiritModifierHistoryRenderer.VisibleCapacity)) * TokenSpacing, 0);
        }
    }
}
