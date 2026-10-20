using System;
using Dice.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Ui = Dice.Editor.SpiritPrefabUi;
using Object = UnityEngine.Object;

namespace Dice.Editor
{
    /** Authors the sequence canvas, narrative sections, and reusable invitation rule rows. */
    public static class SpiritJourneyPrefabAuthoring
    {
        private const string Folder = "Assets/Dice/Content/Presentation/UI/";

        public static void CreateAssets()
        {
            SpiritCharmPrefabAuthoring.CreateAssets();
            var row = Create<SpiritInvitationRuleView>("SpiritInvitationRuleView", Rule);
            Create<EncounterJourneyView>("EncounterJourneyView", journey => Journey(journey, row));
        }

        private static T Create<T>(string name, Action<T> author) where T : Component
        {
            string path = Folder + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path))
                throw new InvalidOperationException("An asset already exists at " + path + ". Assign its required component explicitly.");
            var root = new GameObject(name, typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
                root.AddComponent<SpiritUiContext>().Configure(Ui.Theme);
                var component = root.AddComponent<T>(); author(component);
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<T>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? serialized.FindProperty("<" + field + ">k__BackingField");
            if (property == null) throw new InvalidOperationException(target.name + " is missing serialized field " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Stack(Transform transform, int padding = 0, float spacing = 10)
        {
            var layout = transform.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        }

        private static Text Label(string name, Transform parent, string text, int size, bool heading = false)
        {
            var label = heading ? Ui.Heading(name, parent, text, size, Vector2.zero, Vector2.one) :
                Ui.Label(name, parent, text, size, Vector2.zero, Vector2.one);
            label.resizeTextForBestFit = false; label.supportRichText = true;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return label;
        }

        private static RectTransform Header(string name, Transform parent)
        {
            var row = Ui.Full(name, parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            return row;
        }

        private static RectTransform IconRoot(Transform parent, float size)
        {
            var root = Ui.Full("Artwork", parent);
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = size;
            layout.minHeight = layout.preferredHeight = size;
            layout.flexibleWidth = layout.flexibleHeight = 0;
            return root;
        }

        private static Image Icon(Transform parent)
        {
            var icon = Ui.Full("Icon", parent).gameObject.AddComponent<Image>();
            icon.preserveAspect = true; icon.raycastTarget = false;
            return icon;
        }

        private static void Rule(SpiritInvitationRuleView row)
        {
            Stack(row.transform, 0, 10);
            var header = Header("Identity", row.transform);
            var artwork = IconRoot(header, 44);
            Set(row, "iconRoot", artwork); Set(row, "icon", Icon(artwork));
            Set(row, "title", Label("JourneyRuleName", header, "Encounter rule", 24, true));
            Set(row, "description", Label("JourneyRuleText", row.transform, "Description", 21));
        }

        private static void Journey(EncounterJourneyView journey, SpiritInvitationRuleView rule)
        {
            var sequence = Ui.Full("JourneySequence", journey.transform); Set(journey, "sequence", sequence);
            var blocker = sequence.gameObject.AddComponent<Image>(); blocker.color = Color.clear;
            var artwork = Ui.Full("JourneyArtwork", sequence); Set(journey, "artwork", artwork);
            var illustrations = artwork.gameObject.AddComponent<CanvasGroup>(); illustrations.blocksRaycasts = false;
            Set(journey, "illustrationGroup", illustrations);
            var background = Ui.Full("JourneyBackground", artwork).gameObject.AddComponent<RawImage>();
            background.raycastTarget = false; Set(journey, "background", background);
            var clip = Ui.Full("JourneyForegroundClip", artwork); clip.gameObject.AddComponent<RectMask2D>();
            var foreground = Ui.Full("JourneyForeground", clip).gameObject.AddComponent<RawImage>();
            foreground.raycastTarget = false; Set(journey, "foreground", foreground);
            foreground.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            var left = Ui.Rect("JourneyLeftPanel", artwork, Vector2.zero, new Vector2(.5f, 1)).gameObject.AddComponent<RawImage>();
            left.uvRect = new Rect(0, 0, .5f, 1); left.raycastTarget = false; left.gameObject.SetActive(false); Set(journey, "leftPanel", left);
            var right = Ui.Rect("JourneyRightPanel", artwork, new Vector2(.5f, 0), Vector2.one).gameObject.AddComponent<RawImage>();
            right.uvRect = new Rect(.5f, 0, .5f, 1); right.raycastTarget = false; right.gameObject.SetActive(false); Set(journey, "rightPanel", right);

            var panel = Ui.Panel("JourneyNarrative", sequence, new Vector2(.035f, .145f), new Vector2(.965f, .65f), Ui.Theme.panelColor, 0, true);
            Set(journey, "narrativeGroup", panel.gameObject.AddComponent<CanvasGroup>());
            var scroll = Ui.Scroll("NarrativeScroll", panel.transform, Vector2.zero, Vector2.one, out var content);
            Set(journey, "narrativeScroll", scroll);
            Stack(content, 24, 16);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Set(journey, "narrative", content);
            var title = Label("JourneyTitle", content, "Title", 29, true); title.alignment = TextAnchor.MiddleCenter;
            Set(journey, "title", title);
            var body = Label("JourneyBody", content, "Narrative", 23); body.alignment = TextAnchor.MiddleCenter; Set(journey, "body", body);

            var invitation = Ui.Full("InvitationRules", content); Stack(invitation, 0, 12); Set(journey, "invitationSection", invitation.gameObject);
            Divider("JourneyRuleDivider", invitation);
            Set(journey, "ruleHeading", Label("JourneyRuleHeading", invitation, "ENCOUNTER RULE", 14));
            var rows = Ui.Full("Rows", invitation); Stack(rows, 0, 22);
            Set(journey, "invitationRulesRoot", rows); Set(journey, "invitationRulePrefab", rule);
            Set(journey, "ruleScope", Label("JourneyRuleScope", invitation, "Active throughout this encounter.", 16));
            invitation.gameObject.SetActive(false);

            var charm = Ui.Full("JourneyCharmDetail", content); Stack(charm, 0, 10); Set(journey, "charmDetail", charm);
            Divider("JourneyCharmDivider", charm);
            var charmHeader = Header("Identity", charm);
            var charmArtwork = IconRoot(charmHeader, 50); Set(journey, "charmIconRoot", charmArtwork);
            Set(journey, "charmIcon", Icon(charmArtwork));
            var sigil = Ui.Full("Sigil", charmArtwork).gameObject.AddComponent<SpiritModifierSigilGraphic>(); sigil.raycastTarget = false;
            Set(journey, "charmSigil", sigil);
            var identity = Ui.Full("NameAndEffect", charmHeader); Stack(identity, 0, 4);
            Set(journey, "charmName", Label("JourneyCharmName", identity, "Charm", 24, true));
            Set(journey, "charmEffect", Label("JourneyCharmEffect", identity, "Effect", 21));
            Set(journey, "charmCondition", Label("JourneyCharmCondition", charm, "Description", 19));
            charm.gameObject.SetActive(false);

            Set(journey, "advanceButton", Ui.Button("JourneyAdvanceButton", sequence, "Continue", new Vector2(.035f, .065f), new Vector2(.965f, .145f), null, true));
            Set(journey, "acceptButton", Ui.Button("JourneyAcceptButton", sequence, "Accept", new Vector2(.035f, .065f), new Vector2(.965f, .145f), null, true));
            Set(journey, "returnButton", Ui.Button("JourneyReturnButton", sequence, "Back to clues", new Vector2(.035f, .003f), new Vector2(.505f, .064f)));
            Set(journey, "skipButton", Ui.Button("JourneySkipButton", sequence, "Skip to invitation", new Vector2(.505f, .003f), new Vector2(.965f, .064f)));
            Set(journey, "journeyStatus", Ui.Label("JourneyStatus", sequence, "", 16, new Vector2(.05f, .895f), new Vector2(.95f, .945f), TextAnchor.MiddleCenter, Ui.Theme.muted));
            var charmsPrefab = AssetDatabase.LoadAssetAtPath<SpiritEncounterCharmsScreen>(Folder + "SpiritEncounterCharmsScreen.prefab");
            var charms = ((GameObject)PrefabUtility.InstantiatePrefab(charmsPrefab.gameObject, sequence)).GetComponent<SpiritEncounterCharmsScreen>();
            Set(journey, "EncounterCharms", charms);
            journey.gameObject.SetActive(false);
        }

        private static void Divider(string name, Transform parent)
        {
            var color = Ui.Theme.muted; color.a = .25f;
            var divider = Ui.Panel(name, parent, Vector2.zero, Vector2.one, color);
            divider.raycastTarget = false;
            divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 1;
        }
    }
}
