using System;
using System.IO;
using Dice.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dice.Editor
{
    /** Creates the initial gameplay HUD and ending assets while preserving existing authored prefabs. */
    public static class SpiritHudPrefabAuthoring
    {
        private const string DirectoryPath = "Assets/Dice/Content/Presentation/UI";

        public static void CreateAssets()
        {
            Directory.CreateDirectory(DirectoryPath);
            AssetDatabase.Refresh();
            Create("SpiritGameplayHud", CreateHud);
            Create("SpiritEncounterEndingView", CreateEnding);
            AssetDatabase.SaveAssets();
        }

        private static void Create(string name, Func<GameObject> factory)
        {
            string path = DirectoryPath + "/" + name + ".prefab";
            if (File.Exists(path)) return;
            var root = factory();
            try { PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? serialized.FindProperty("<" + field + ">k__BackingField");
            if (property == null) throw new InvalidOperationException("Missing authored reference " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? serialized.FindProperty("<" + field + ">k__BackingField");
            if (property == null) throw new InvalidOperationException("Missing authored setting " + field);
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateHud()
        {
            var root = SpiritPrefabUi.Full("GameplayHud", null);
            root.gameObject.AddComponent<SpiritUiContext>().Configure(SpiritPrefabUi.Theme);
            var hud = root.gameObject.AddComponent<SpiritGameplayHud>();
            var builder = new HudBuilder();
            builder.BuildHeader(root, () => { });
            builder.BuildPlayingPanel();
            var trayFrame = SpiritPrefabUi.Rect("DiceTrayFrame", builder.PlayingPanel,
                new Vector2(0, .578f), new Vector2(1, .938f));
            var trayImage = SpiritPrefabUi.Full("DiceTrayImage", trayFrame).gameObject.AddComponent<RawImage>();
            trayImage.raycastTarget = false;
            builder.BuildPlayingHud(() => { }, () => { });
            builder.BuildEncounterRule(trayFrame);
            builder.BuildFooter(() => { }, () => { }, () => { });
            builder.BuildStageIntro();
            ApplyFrame(builder.CharmsButton.GetComponent<Image>(), SpiritPrefabUi.Theme.woodFrame);
            ApplyFrame(builder.HandRanksButton.GetComponent<Image>(), SpiritPrefabUi.Theme.woodFrame);
            builder.RerollButton.GetComponent<Image>().color = new Color32(170, 216, 181, 255);
            builder.PlayButton.GetComponent<Image>().color = new Color32(255, 196, 103, 255);
            Set(hud, "portrait", root);
            Set(hud, "DiceTrayFrame", trayFrame);
            Set(hud, "DiceTrayImage", trayImage);
            Set(hud, "EncounterTitle", builder.EncounterTitle);
            Set(hud, "StageLabel", builder.StageLabel);
            Set(hud, "MenuButton", builder.MenuButton);
            Set(hud, "TargetPanel", builder.TargetPanel);
            Set(hud, "TargetLabel", builder.TargetLabel);
            Set(hud, "TargetCaption", builder.TargetCaption);
            Set(hud, "HandsPanel", builder.HandsPanel);
            Set(hud, "HandsLabel", builder.HandsLabel);
            Set(hud, "RerollsPanel", builder.RerollsPanel);
            Set(hud, "RerollsLabel", builder.RerollsLabel);
            Set(hud, "ResourcesLabel", builder.ResourcesLabel);
            Set(hud, "PlayingPanel", builder.PlayingPanel);
            Set(hud, "SelectionLabel", builder.SelectionLabel);
            Set(hud, "ModifierEmblem", builder.ModifierEmblem);
            Set(hud, "ModifierActivationIcon", builder.ModifierActivationIcon);
            Set(hud, "CategoryLabel", builder.CategoryLabel);
            Set(hud, "ScoreDetail", builder.ScoreDetail);
            Set(hud, "BaseScoreLabel", builder.BaseScoreLabel);
            Set(hud, "MultiplierLabel", builder.MultiplierLabel);
            Set(hud, "HandScoreLabel", builder.HandScoreLabel);
            Set(hud, "RerollButton", builder.RerollButton);
            Set(hud, "PlayButton", builder.PlayButton);
            Set(hud, "EncounterRuleGroup", builder.EncounterRuleGroup);
            Set(hud, "EncounterRuleLabel", builder.EncounterRuleLabel);
            Set(hud, "EncounterRuleIcon", builder.EncounterRuleIcon);
            Set(hud, "RuleSpiritSweep", builder.RuleSpiritSweep);
            Set(hud, "StageIntroPanel", builder.StageIntroPanel);
            Set(hud, "StageIntroTitle", builder.StageIntroTitle);
            Set(hud, "StageIntroDetail", builder.StageIntroDetail);
            Set(hud, "StageIntroDetailFontSize", 19);
            Set(hud, "CharmsButton", builder.CharmsButton);
            Set(hud, "HandRanksButton", builder.HandRanksButton);
            Set(hud, "CharmCount", builder.CharmCount);
            Set(hud, "StatusLabel", builder.StatusLabel);
            Set(hud, "InvestigationButton", builder.InvestigationButton);
            Set(hud, "diceTrayFrame", trayFrame);
            Set(hud, "encounterRuleSigilRoot", builder.encounterRuleSigilRoot);
            Set(hud, "ruleSpiritIcon", builder.ruleSpiritIcon);
            Set(hud, "ruleSpiritSigilRoot", builder.ruleSpiritSigilRoot);
            Set(hud, "modifierActivationSigil", builder.modifierActivationSigil);
            Set(hud, "modifierActivationCategory", builder.modifierActivationCategory);
            Set(hud, "rerollEncounterRuleGroup", builder.EncounterRuleGroup);
            Set(hud, "rerollEncounterRuleLabel", builder.EncounterRuleLabel);
            Set(hud, "rerollEncounterRuleIcon", builder.EncounterRuleIcon);
            var playBadge = Object.Instantiate(builder.EncounterRuleGroup, builder.PlayButton.transform, false);
            playBadge.name = "PlayEncounterRuleFavor";
            Set(hud, "playEncounterRuleGroup", playBadge);
            Set(hud, "playEncounterRuleLabel", playBadge.GetComponentInChildren<Text>(true));
            var playIcon = playBadge.GetComponentInChildren<Image>(true);
            Set(hud, "playEncounterRuleIcon", playIcon);
            Set(hud, "playEncounterRuleSigilRoot", playIcon.rectTransform);
            SetHighlight(hud, "BaseScoreHighlight", builder.BaseScoreHighlight);
            SetHighlight(hud, "MultiplierHighlight", builder.MultiplierHighlight);
            SetHighlight(hud, "RerollsHighlight", builder.RerollsHighlight);
            SetHighlight(hud, "HandsHighlight", builder.HandsHighlight);
            foreach (var button in root.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            return root.gameObject;
        }

        private static void ApplyFrame(Image image, SpiritUiTheme.Frame frame)
        {
            image.sprite = SpiritPrefabUi.FrameSprite(frame);
            if (image.sprite == null) return;
            image.type = Image.Type.Sliced;
            image.material = frame.surfaceMaterial;
            image.pixelsPerUnitMultiplier = Mathf.Max(.1f, frame.pixelsPerUnitMultiplier);
            image.color = Color.white;
        }

        private static void SetHighlight(SpiritGameplayHud hud, string name, CounterReferences value)
        {
            var serialized = new SerializedObject(hud);
            var property = serialized.FindProperty("<" + name + ">k__BackingField");
            property.FindPropertyRelative("Label").objectReferenceValue = value.Label;
            property.FindPropertyRelative("Caption").objectReferenceValue = value.Caption;
            property.FindPropertyRelative("Icon").objectReferenceValue = value.Icon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class CounterReferences
        {
            public Text Label;
            public Text Caption;
            public Image Icon;
            public Vector2 OriginalInset;
        }

        private sealed class HudBuilder
        {
            public Text EncounterTitle;
            public Text StageLabel;
            public Button MenuButton;
            public RectTransform TargetPanel;
            public Text TargetLabel;
            public Text TargetCaption;
            public RectTransform HandsPanel;
            public Text HandsLabel;
            public RectTransform RerollsPanel;
            public Text RerollsLabel;
            public Text ResourcesLabel;
            public RectTransform PlayingPanel;
            public Text SelectionLabel;
            public SpiritUiOrnament ModifierEmblem;
            public Image ModifierActivationIcon;
            public Text CategoryLabel;
            public Text ScoreDetail;
            public Text BaseScoreLabel;
            public Text MultiplierLabel;
            public Text HandScoreLabel;
            public Button RerollButton;
            public Button PlayButton;
            public CanvasGroup EncounterRuleGroup;
            public Text EncounterRuleLabel;
            public Image EncounterRuleIcon;
            public CanvasGroup RuleSpiritSweep;
            public RectTransform StageIntroPanel;
            public Text StageIntroTitle;
            public Text StageIntroDetail;
            public Button CharmsButton;
            public Button HandRanksButton;
            public Text CharmCount;
            public Text StatusLabel;
            public Button InvestigationButton;
            private RectTransform portrait;
            private RectTransform diceTrayFrame;
            private RectTransform rerollActionLabel;
            private RectTransform playActionLabel;
            public RectTransform encounterRuleSigilRoot;
            public Image ruleSpiritIcon;
            public RectTransform ruleSpiritSigilRoot;
            public SpiritModifierSigilGraphic modifierActivationSigil;
            public SpiritModifierCategoryBadge modifierActivationCategory;
            public CounterReferences BaseScoreHighlight;
            public CounterReferences MultiplierHighlight;
            public CounterReferences RerollsHighlight;
            public CounterReferences HandsHighlight;
            private Transform transform => portrait;

            public void BuildHeader(RectTransform parent, Action toggleMenu)
            {
                if (parent == null) throw new ArgumentNullException(nameof(parent));
                if (toggleMenu == null) throw new ArgumentNullException(nameof(toggleMenu));

                portrait = parent;
                EncounterTitle = SpiritPrefabUi.Heading("EncounterTitle", portrait, "", 21,
                    new Vector2(.12f, .963f), new Vector2(.88f, .999f), TextAnchor.MiddleCenter);
                var headerRule = SpiritPrefabUi.Rect("HeaderRule", portrait, new Vector2(.16f, .925f), new Vector2(.84f, .995f));
                SpiritPrefabUi.Decorate(headerRule);
                StageLabel = SpiritPrefabUi.Heading("StageLabel", portrait, "", 16, new Vector2(.15f, .939f), new Vector2(.85f, .963f),
                    TextAnchor.MiddleCenter);
                MenuButton = SpiritPrefabUi.Button("MenuButton", portrait, "Menu", new Vector2(.80f, .932f), new Vector2(.99f, .978f),
                    () => toggleMenu(), compact: true);
                var menuText = MenuButton.GetComponentInChildren<Text>();
                menuText.fontSize = menuText.resizeTextMaxSize = 15;
                menuText.resizeTextMinSize = 12;
                var target = SpiritPrefabUi.Panel("TargetPanel", portrait, new Vector2(.035f, .544f), new Vector2(.965f, .63f),
                    SpiritPrefabUi.GetTheme(transform).panelColor, 0, true, SpiritPrefabUi.Theme.greenFrame);
                TargetPanel = target.rectTransform;
                TargetLabel = SpiritPrefabUi.Heading("TargetLabel", target.transform, "", 56, new Vector2(.2f, .29f), new Vector2(.8f, .99f),
                    TextAnchor.MiddleCenter);
                TargetCaption = SpiritPrefabUi.Heading("ToClearLabel", target.transform, "TO CLEAR", 17,
                    new Vector2(.23f, .1f), new Vector2(.77f, .35f), TextAnchor.MiddleCenter);
                TargetCaption.rectTransform.offsetMin = new Vector2(12, 0);
                TargetCaption.rectTransform.offsetMax = new Vector2(-12, 0);
                HandsPanel = SpiritPrefabUi.Panel("HandsPanel", portrait, new Vector2(.12f, .506f), new Vector2(.495f, .542f),
                    SpiritPrefabUi.GetTheme(transform).panelColor, 0, true).rectTransform;
                HandsLabel = SpiritPrefabUi.Heading("HandsRemainingLabel", HandsPanel, "", 20, Vector2.zero, Vector2.one,
                    TextAnchor.MiddleCenter);
                RerollsPanel = SpiritPrefabUi.Panel("RerollsPanel", portrait, new Vector2(.505f, .506f), new Vector2(.88f, .542f),
                    SpiritPrefabUi.GetTheme(transform).panelColor, 0, true).rectTransform;
                RerollsLabel = SpiritPrefabUi.Heading("RerollsRemainingLabel", RerollsPanel, "", 20, Vector2.zero, Vector2.one,
                    TextAnchor.MiddleCenter);
                ResourcesLabel = SpiritPrefabUi.Label("ResourcesLabel", portrait, "", 18, new Vector2(.04f, .503f), new Vector2(.96f, .54f),
                    TextAnchor.MiddleCenter, SpiritPrefabUi.GetTheme(transform).muted);
            }

            public RectTransform BuildPlayingPanel()
            {
                PlayingPanel = SpiritPrefabUi.Rect("PlayingPanel", portrait, new Vector2(0, .065f), new Vector2(1, .55f));
                return PlayingPanel;
            }

            public void BuildPlayingHud(Action reroll, Action play)
            {
                if (reroll == null) throw new ArgumentNullException(nameof(reroll));
                if (play == null) throw new ArgumentNullException(nameof(play));

                SelectionLabel = SpiritPrefabUi.Label("SelectionLabel", PlayingPanel, "Tap dice to select for reroll", 17,
                    new Vector2(.115f, .438f), new Vector2(.97f, .525f), TextAnchor.MiddleCenter, SpiritPrefabUi.GetTheme(transform).muted);
                SelectionLabel.resizeTextForBestFit = true;
                SelectionLabel.resizeTextMinSize = 14;
                SelectionLabel.resizeTextMaxSize = 17;
                SelectionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                SelectionLabel.rectTransform.offsetMin = new Vector2(0, 0);
                SelectionLabel.rectTransform.offsetMax = new Vector2(0, 0);
                ModifierEmblem = SpiritPrefabUi.Rect("ModifierEmblem", PlayingPanel, new Vector2(.045f, .456f), new Vector2(.105f, .511f))
                    .gameObject.AddComponent<SpiritUiOrnament>();
                ModifierEmblem.kind = "charm";
                ModifierEmblem.color = SpiritPrefabUi.GetTheme(transform).amber;
                ModifierEmblem.raycastTarget = false;
                ModifierActivationIcon = MakeIcon("ModifierActivationIcon", ModifierEmblem.transform,
                    new Vector2(.5f, .5f), 32);
                modifierActivationSigil = MakeSigil("ModifierActivationSigil", ModifierActivationIcon);
                modifierActivationCategory = SpiritCharmPrefabAuthoring.AddCategoryBadge("ModifierActivationCategory", ModifierActivationIcon);
                ModifierEmblem.gameObject.SetActive(false);
                var score = SpiritPrefabUi.Panel("ScorePreview", PlayingPanel, new Vector2(.045f, .19f), new Vector2(.955f, .435f),
                    SpiritPrefabUi.GetTheme(transform).panelColor, 0, true);
                CategoryLabel = SpiritPrefabUi.Heading("HandCategory", score.transform, "", 23,
                    new Vector2(.05f, .70f), new Vector2(.95f, .91f), TextAnchor.MiddleCenter);
                SpiritPrefabUi.Decorate(score.transform);
                var divider = score.transform.Find("GoldDivider").GetComponent<RectTransform>();
                divider.anchorMin = new Vector2(.07f, .65f);
                divider.anchorMax = new Vector2(.93f, .67f);
                var formula = SpiritPrefabUi.Rect("ScoreFormula", score.transform, new Vector2(.04f, .20f), new Vector2(.96f, .65f));
                BaseScoreLabel = BuildScoreCounter("BaseScoreLabel", "BASE", formula, 0, .32f);
                MultiplierLabel = BuildScoreCounter("MultiplierLabel", "MULT", formula, .34f, .66f);
                HandScoreLabel = BuildScoreCounter("HandScore", "TOTAL", formula, .68f, 1);
                BaseScoreHighlight = MakeCounterHighlight("BaseScoreCharmIcon", BaseScoreLabel, true);
                MultiplierHighlight = MakeCounterHighlight("MultiplierCharmIcon", MultiplierLabel, true);
                RerollsHighlight = MakeCounterHighlight("RerollsCharmIcon", RerollsLabel, false);
                HandsHighlight = MakeCounterHighlight("HandsCharmIcon", HandsLabel, false);
                ScoreDetail = SpiritPrefabUi.Label("ScoreBreakdown", score.transform, "ROLL BONUSES BANK HERE", 14,
                    new Vector2(.04f, .06f), new Vector2(.96f, .20f), TextAnchor.MiddleCenter, SpiritPrefabUi.GetTheme(transform).muted);
                ScoreDetail.rectTransform.offsetMin = ScoreDetail.rectTransform.offsetMax = Vector2.zero;
                ScoreDetail.resizeTextForBestFit = true;
                ScoreDetail.resizeTextMinSize = 11;
                ScoreDetail.resizeTextMaxSize = 14;
                BaseScoreLabel.text = MultiplierLabel.text = "0";
                HandScoreLabel.text = "0";
                RerollButton = SpiritPrefabUi.Button("RerollButton", PlayingPanel, "Reroll", new Vector2(.035f, 0), new Vector2(.49f, .188f),
                    () => reroll(), compact: true);
                PlayButton = SpiritPrefabUi.Button("PlayButton", PlayingPanel, "Play Hand", new Vector2(.51f, 0), new Vector2(.965f, .188f),
                    () => play(), true, true);
                foreach (var button in new[] { RerollButton, PlayButton })
                {
                    var text = button.GetComponentInChildren<Text>();
                    text.fontSize = text.resizeTextMaxSize = 25;
                    text.resizeTextMinSize = 18;
                }
            }

            public void BuildEncounterRule(RectTransform trayFrame)
            {
                diceTrayFrame = trayFrame;
                rerollActionLabel = RerollButton.GetComponentInChildren<Text>().rectTransform;
                playActionLabel = PlayButton.GetComponentInChildren<Text>().rectTransform;
                rerollActionLabel.anchorMin = new Vector2(.08f, .36f);
                playActionLabel.anchorMin = new Vector2(.08f, .36f);
                var favor = SpiritPrefabUi.Rect("EncounterRuleFavor", RerollButton.transform,
                    new Vector2(.08f, .06f), new Vector2(.94f, .35f));
                EncounterRuleGroup = favor.gameObject.AddComponent<CanvasGroup>();
                EncounterRuleGroup.interactable = false;
                EncounterRuleGroup.blocksRaycasts = false;
                var icon = SpiritPrefabUi.Rect("EncounterRuleIcon", favor, new Vector2(0, .5f), new Vector2(0, .5f));
                icon.sizeDelta = new Vector2(18, 18);
                icon.anchoredPosition = new Vector2(9, 0);
                EncounterRuleIcon = icon.gameObject.AddComponent<Image>();
                EncounterRuleIcon.preserveAspect = true;
                EncounterRuleIcon.color = SpiritPrefabUi.GetTheme(transform).amber;
                EncounterRuleIcon.raycastTarget = false;
                encounterRuleSigilRoot = icon;
                EncounterRuleLabel = SpiritPrefabUi.Label("EncounterRuleLabel", favor, "", 14,
                    Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, SpiritPrefabUi.GetTheme(transform).amber);
                EncounterRuleLabel.rectTransform.offsetMin = new Vector2(21, 0);
                EncounterRuleLabel.rectTransform.offsetMax = Vector2.zero;
                EncounterRuleLabel.resizeTextForBestFit = true;
                EncounterRuleLabel.resizeTextMinSize = 12;
                EncounterRuleLabel.resizeTextMaxSize = 14;
                favor.gameObject.SetActive(false);

                if (diceTrayFrame != null) BuildRuleSpiritSweep();
                RerollButton.GetComponent<SpiritUiButtonVisual>().CaptureContent();
            }

            public void BuildStageIntro()
            {
                StageIntroPanel = SpiritPrefabUi.Panel("StageIntroPanel", portrait, new Vector2(.06f, .37f), new Vector2(.94f, .57f),
                    SpiritPrefabUi.GetTheme(transform).panelColor, 0, true).rectTransform;
                StageIntroTitle = SpiritPrefabUi.Heading("StageIntroTitle", StageIntroPanel, "", 28,
                    new Vector2(.04f, .55f), new Vector2(.96f, .94f), TextAnchor.MiddleCenter, SpiritPrefabUi.GetTheme(transform).amber);
                StageIntroDetail = SpiritPrefabUi.Label("StageIntroDetail", StageIntroPanel, "", 19,
                    new Vector2(.04f, .08f), new Vector2(.96f, .55f), TextAnchor.MiddleCenter);
                StageIntroPanel.gameObject.SetActive(false);
            }

            public void BuildFooter(Action showCharms, Action showHandRanks, Action returnToInvestigation)
            {
                if (showCharms == null) throw new ArgumentNullException(nameof(showCharms));
                if (showHandRanks == null) throw new ArgumentNullException(nameof(showHandRanks));
                if (returnToInvestigation == null) throw new ArgumentNullException(nameof(returnToInvestigation));

                CharmsButton = SpiritPrefabUi.Button("CharmsButton", portrait, "CHARMS (0)", new Vector2(.035f, .007f), new Vector2(.49f, .065f),
                    () => showCharms());
                HandRanksButton = SpiritPrefabUi.Button("HandRanksButton", portrait, "HAND RANKS", new Vector2(.51f, .007f), new Vector2(.965f, .065f),
                    () => showHandRanks());
                foreach (var button in new[] { CharmsButton, HandRanksButton })
                {
                    var text = button.GetComponentInChildren<Text>();
                    text.fontSize = text.resizeTextMaxSize = 19;
                    text.resizeTextMinSize = 14;
                }
                CharmCount = SpiritPrefabUi.Label("CharmCount", portrait, "", 15, new Vector2(.35f, .005f), new Vector2(.52f, .065f),
                    TextAnchor.MiddleCenter, SpiritPrefabUi.GetTheme(transform).muted);
                CharmCount.gameObject.SetActive(false);
                StatusLabel = SpiritPrefabUi.Label("StatusMessage", portrait, "", 14, new Vector2(.12f, .892f), new Vector2(.88f, .927f),
                    TextAnchor.MiddleCenter, SpiritPrefabUi.GetTheme(transform).muted);
                InvestigationButton = SpiritPrefabUi.Button("ReturnToInvestigationButton", portrait, "Clues",
                    new Vector2(.01f, .932f), new Vector2(.20f, .978f), () => returnToInvestigation(), compact: true);
                var investigationText = InvestigationButton.GetComponentInChildren<Text>();
                investigationText.fontSize = investigationText.resizeTextMaxSize = 15;
                investigationText.resizeTextMinSize = 12;
                InvestigationButton.gameObject.SetActive(false);
            }

            private void BuildRuleSpiritSweep()
            {
                var sweep = SpiritPrefabUi.Rect("RuleSpiritSweep", diceTrayFrame, new Vector2(0, .5f), new Vector2(0, .5f));
                sweep.sizeDelta = new Vector2(72, 72);
                RuleSpiritSweep = sweep.gameObject.AddComponent<CanvasGroup>();
                RuleSpiritSweep.interactable = false;
                RuleSpiritSweep.blocksRaycasts = false;
                ruleSpiritIcon = SpiritPrefabUi.Full("RuleSpiritArtwork", sweep).gameObject.AddComponent<Image>();
                ruleSpiritIcon.preserveAspect = true;
                ruleSpiritIcon.color = new Color(.35f, .9f, .9f, .75f);
                ruleSpiritIcon.raycastTarget = false;
                ruleSpiritSigilRoot = sweep;
                sweep.gameObject.SetActive(false);
            }

            private static Text BuildScoreCounter(string name, string caption, Transform parent, float left, float right)
            {
                var cell = SpiritPrefabUi.Rect(name + "Cell", parent, new Vector2(left, 0), new Vector2(right, 1));
                var heading = SpiritPrefabUi.Heading(name + "Caption", cell, caption, 16,
                    new Vector2(0, .65f), Vector2.one, TextAnchor.MiddleCenter, SpiritPrefabUi.GetTheme(parent).muted);
                heading.rectTransform.offsetMin = heading.rectTransform.offsetMax = Vector2.zero;
                var value = SpiritPrefabUi.Heading(name, cell, "0", 36,
                    Vector2.zero, new Vector2(1, .68f), TextAnchor.MiddleCenter);
                value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero;
                value.resizeTextMinSize = 22;
                return value;
            }

            private static CounterReferences MakeCounterHighlight(string name, Text label, bool scoreCounter)
            {
                var caption = scoreCounter ? label.transform.parent.Find(label.name + "Caption").GetComponent<Text>() : label;
                var icon = MakeIcon(name, label.transform.parent,
                    scoreCounter ? new Vector2(.17f, .7f) : new Vector2(0, .5f), 22);
                if (!scoreCounter) icon.rectTransform.anchoredPosition = new Vector2(19, 0);
                return new CounterReferences
                {
                    Label = label,
                    Caption = caption,
                    Icon = icon,
                    OriginalInset = caption.rectTransform.offsetMin
                };
            }

            private static Image MakeIcon(string name, Transform parent, Vector2 anchor, float size)
            {
                var rect = SpiritPrefabUi.Rect(name, parent, anchor, anchor);
                rect.sizeDelta = new Vector2(size, size);
                var icon = rect.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.gameObject.SetActive(false);
                return icon;
            }

            private static SpiritModifierSigilGraphic MakeSigil(string name, Image icon)
            {
                var rect = SpiritPrefabUi.Rect(name, icon.transform.parent, icon.rectTransform.anchorMin, icon.rectTransform.anchorMax);
                rect.pivot = icon.rectTransform.pivot;
                rect.anchoredPosition = icon.rectTransform.anchoredPosition;
                rect.sizeDelta = icon.rectTransform.sizeDelta;
                var sigil = rect.gameObject.AddComponent<SpiritModifierSigilGraphic>();
                sigil.color = SpiritPrefabUi.Theme.amber;
                sigil.raycastTarget = false;
                sigil.gameObject.SetActive(false);
                return sigil;
            }
        }

        private static GameObject CreateEnding()
        {
            var theme = SpiritPrefabUi.Theme;
            var Root = SpiritPrefabUi.Full("EndingView", null);
            Root.gameObject.AddComponent<SpiritUiContext>().Configure(theme);
            var ending = Root.gameObject.AddComponent<SpiritEncounterEndingView>();
            var group = Root.gameObject.AddComponent<CanvasGroup>();
            group.ignoreParentGroups = true;
            var blocker = Root.gameObject.AddComponent<Image>();
            blocker.color = Color.clear;
            var panel = SpiritPrefabUi.Panel("EndingPanel", Root, new Vector2(.045f, .055f), new Vector2(.955f, .62f),
                theme.panelColor).rectTransform;
            var frame = SpiritPrefabUi.Panel("Frame", panel, Vector2.zero, Vector2.one, theme.panelColor, 0, true);
            frame.raycastTarget = false;
            var title = SpiritPrefabUi.Heading("EndingTitle", panel, "", 35, new Vector2(.025f, .845f), new Vector2(.975f, .98f),
                TextAnchor.MiddleCenter, theme.amber);
            title.resizeTextMinSize = 25;
            var scroll = SpiritPrefabUi.Scroll("EndingUnlockScroll", panel, new Vector2(.035f, .245f), new Vector2(.965f, .84f), out var content);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(3, 3, 6, 12);
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var artworkFrame = SpiritPrefabUi.Full("EndingArtworkFrame", content);
            artworkFrame.gameObject.AddComponent<LayoutElement>().preferredHeight = 120;
            var artwork = SpiritPrefabUi.Full("EndingArtwork", artworkFrame).gameObject.AddComponent<RawImage>();
            artwork.raycastTarget = false;
            var aspect = artwork.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            var detail = ContentLabel(content, "EndingDetail", 21, theme.cream);
            var clueTitle = ContentLabel(content, "EndingClueTitle", 24, theme.amber, true);
            var clueText = ContentLabel(content, "EndingClueText", 21, theme.cream);
            var unlockTitle = ContentLabel(content, "EndingUnlockTitle", 17, theme.muted, true);
            var unlocks = ContentLabel(content, "EndingUnlocks", 19, theme.amber);
            var returnButton = SpiritPrefabUi.Button("EndingNewRunButton", panel, "Return to the trail",
                new Vector2(.065f, .045f), new Vector2(.935f, .215f),
                null, true);
            var returnLabel = returnButton.GetComponentInChildren<Text>();
            returnLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            returnLabel.rectTransform.anchorMin = new Vector2(.18f, .08f);
            returnLabel.rectTransform.anchorMax = new Vector2(.82f, .92f);
            var retryButton = SpiritPrefabUi.Button("EndingRetryButton", panel, "Retry",
                new Vector2(.065f, .045f), new Vector2(.495f, .215f),
                null);
            var dialogueContinueButton = SpiritPrefabUi.Button("EndingDialogueContinueButton", panel, "Continue",
                new Vector2(.065f, .045f), new Vector2(.935f, .215f),
                null, true);
            var skipButton = SpiritPrefabUi.Button("EndingSkipButton", Root, "Skip farewell",
                new Vector2(.55f, .025f), new Vector2(.955f, .1f), null);
            var actions = SpiritPrefabUi.Rect("EndingActions", panel,
                new Vector2(.065f, .045f), new Vector2(.935f, .215f));
            var actionLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 10;
            actionLayout.childControlWidth = actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = actionLayout.childForceExpandHeight = true;
            foreach (var button in new[] { retryButton, returnButton, dialogueContinueButton })
            {
                button.transform.SetParent(actions, false);
                button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            }
            Set(ending, "Root", Root);
            Set(ending, "artworkAspect", aspect);
            Set(ending, "panel", panel);
            Set(ending, "content", content);
            Set(ending, "scroll", scroll);
            Set(ending, "title", title);
            Set(ending, "detail", detail);
            Set(ending, "clueTitle", clueTitle);
            Set(ending, "clueText", clueText);
            Set(ending, "unlockTitle", unlockTitle);
            Set(ending, "unlocks", unlocks);
            Set(ending, "artworkFrame", artworkFrame);
            Set(ending, "artwork", artwork);
            Set(ending, "returnButton", returnButton);
            Set(ending, "retryButton", retryButton);
            Set(ending, "dialogueContinueButton", dialogueContinueButton);
            Set(ending, "skipButton", skipButton);
            Root.gameObject.SetActive(false);
            return Root.gameObject;
        }

        private static Text ContentLabel(Transform content, string name, int size, Color color, bool heading = false)
        {
            var label = heading ? SpiritPrefabUi.Heading(name, content, "", size, Vector2.zero, Vector2.one,
                TextAnchor.UpperLeft, color) : SpiritPrefabUi.Label(name, content, "", size, Vector2.zero, Vector2.one,
                TextAnchor.UpperLeft, color);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = false;
            return label;
        }
    }
}
