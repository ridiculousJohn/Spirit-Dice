using System;
using System.Collections.Generic;
using System.Globalization;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * A phrase whose wording a count decides: the singular template when the count is 1, otherwise the plural
     * template. The caller fills the chosen template's placeholders.
     */
    [Serializable]
    public sealed class SpiritCountedWording
    {
        public string singular;
        public string plural;

        public string For(long count) => count == 1 ? singular : plural;
    }

    /** Fills a wording template's named placeholders, written {name}, the way charm condition templates are filled. */
    public static class SpiritWordingTemplate
    {
        public static string Fill(string template, string name, string value) => template.Replace("{" + name + "}", value);

        /** Fills a placeholder with a whole number written without digit grouping. */
        public static string Fill(string template, string name, long value) =>
            Fill(template, name, value.ToString(CultureInfo.InvariantCulture));

        /** Fills a placeholder with a score, written with the digit grouping of score counters. */
        public static string FillScore(string template, string name, long value) =>
            Fill(template, name, DiagnosticText.Number(value));
    }

    /** Checks one section of a wording asset; a missing field fails with its asset, section and field names. */
    public sealed class SpiritWordingCheck
    {
        private readonly string asset;
        private readonly string section;

        internal SpiritWordingCheck(string asset, string section)
        {
            this.asset = asset;
            this.section = section;
        }

        /** Wording shown on its own; it must contain visible text. */
        public void Text(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value)) throw Missing(field);
        }

        /** Wording that joins other wording; it may be only spaces or line breaks, but not empty. */
        public void Separator(string value, string field)
        {
            if (string.IsNullOrEmpty(value)) throw Missing(field);
        }

        public void Counted(SpiritCountedWording value, string field)
        {
            if (value == null) throw Missing(field);
            Text(value.singular, field + ".singular");
            Text(value.plural, field + ".plural");
        }

        public void Terms(string[] values, string field)
        {
            if (values == null || values.Length == 0) throw Missing(field);
            for (int i = 0; i < values.Length; ++i) Text(values[i], field + "[" + i + "]");
        }

        private InvalidOperationException Missing(string field) =>
            new InvalidOperationException("The UI wording " + asset + " has no " + section + "." + field + ".");
    }

    /** One screen's group of wording. */
    [Serializable]
    public abstract class SpiritWordingSection
    {
        /** Fails naming the first missing field the section's screen uses. */
        public abstract void Validate(SpiritWordingCheck check);
    }

    /**
     * The Spirit House UI's player-facing wording, grouped by screen. Text that composes values or chooses between
     * wordings lives here as named-placeholder templates; text that never changes is authored on the prefab's Text
     * component instead. Every field is required: configuring a screen validates the asset and fails naming the
     * missing field.
     */
    [CreateAssetMenu(menuName = "Dice/Spirit UI Wording", fileName = "SpiritHouseWording")]
    public sealed class SpiritUiWording : ScriptableObject
    {
        /** Application, campaign, journey and run statuses; {detail} is developer text describing a failure. */
        [Serializable]
        public sealed class StatusText : SpiritWordingSection
        {
            public string waitingForStartup;
            public string loadingConfiguration;
            public string configurationFailed;
            public string applicationFailed;
            public string contentFailed;
            public string encounterInitializationFailed;
            public string presentationSceneMissing;
            public string loadingPresentation;
            public string presentationOpenFailed;
            public string presentationInitializationFailed;
            public string mainMenuReady;
            public string mainMenuFailed;
            public string saveSlotsRecovered;
            public string unassignedSavesRecovered;
            public string saveSlotsFailed;
            public string saveSlotSelectionFailed;
            public string newGameFailed;
            public string previousCampaignNotRemoved;
            /** {slot}: the slot the campaign was recovered to. */
            public string campaignRecovered;
            public string campaignRecoveryFailed;
            public string investigationReady;
            public string investigationFailed;
            public string chapterNotFound;
            public string chapterLocked;
            public string leadNotFound;
            public string locationLocked;
            public string savedEncounterHasNoLead;
            public string activeEncounterHasNoLead;
            public string invitationFailed;
            /** Shared by the run status and the profile state that report a failed story reward. */
            public string rewardFailed;
            public string contentNotInitialized;
            public string startFailed;
            /** {seed}: the run's seed. */
            public string started;
            public string saved;
            public string saveFailed;
            public string catalogRequired;
            public string loaded;
            public string loadFailed;
            public string ready;
            /** {revision}: the run's revision. */
            public string actionAccepted;
            /** {rejection}: why the engine rejected the action. */
            public string actionRejected;
            public string actionFailed;
            public string noEncounter;
            public string handPreviewUnavailable;
            public string modifierInspectionUnavailable;
            public string profileLoadFailed;
            public string encounterSavePending;
            public string profileSavePending;
            public string storyFailed;
            public string visitFailed;
            /** Joins a run state's wording, its warnings and its profile problem. */
            public string separator;

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(waitingForStartup, nameof(waitingForStartup));
                check.Text(loadingConfiguration, nameof(loadingConfiguration));
                check.Text(configurationFailed, nameof(configurationFailed));
                check.Text(applicationFailed, nameof(applicationFailed));
                check.Text(contentFailed, nameof(contentFailed));
                check.Text(encounterInitializationFailed, nameof(encounterInitializationFailed));
                check.Text(presentationSceneMissing, nameof(presentationSceneMissing));
                check.Text(loadingPresentation, nameof(loadingPresentation));
                check.Text(presentationOpenFailed, nameof(presentationOpenFailed));
                check.Text(presentationInitializationFailed, nameof(presentationInitializationFailed));
                check.Text(mainMenuReady, nameof(mainMenuReady));
                check.Text(mainMenuFailed, nameof(mainMenuFailed));
                check.Text(saveSlotsRecovered, nameof(saveSlotsRecovered));
                check.Text(unassignedSavesRecovered, nameof(unassignedSavesRecovered));
                check.Text(saveSlotsFailed, nameof(saveSlotsFailed));
                check.Text(saveSlotSelectionFailed, nameof(saveSlotSelectionFailed));
                check.Text(newGameFailed, nameof(newGameFailed));
                check.Text(previousCampaignNotRemoved, nameof(previousCampaignNotRemoved));
                check.Text(campaignRecovered, nameof(campaignRecovered));
                check.Text(campaignRecoveryFailed, nameof(campaignRecoveryFailed));
                check.Text(investigationReady, nameof(investigationReady));
                check.Text(investigationFailed, nameof(investigationFailed));
                check.Text(chapterNotFound, nameof(chapterNotFound));
                check.Text(chapterLocked, nameof(chapterLocked));
                check.Text(leadNotFound, nameof(leadNotFound));
                check.Text(locationLocked, nameof(locationLocked));
                check.Text(savedEncounterHasNoLead, nameof(savedEncounterHasNoLead));
                check.Text(activeEncounterHasNoLead, nameof(activeEncounterHasNoLead));
                check.Text(invitationFailed, nameof(invitationFailed));
                check.Text(rewardFailed, nameof(rewardFailed));
                check.Text(contentNotInitialized, nameof(contentNotInitialized));
                check.Text(startFailed, nameof(startFailed));
                check.Text(started, nameof(started));
                check.Text(saved, nameof(saved));
                check.Text(saveFailed, nameof(saveFailed));
                check.Text(catalogRequired, nameof(catalogRequired));
                check.Text(loaded, nameof(loaded));
                check.Text(loadFailed, nameof(loadFailed));
                check.Text(ready, nameof(ready));
                check.Text(actionAccepted, nameof(actionAccepted));
                check.Text(actionRejected, nameof(actionRejected));
                check.Text(actionFailed, nameof(actionFailed));
                check.Text(noEncounter, nameof(noEncounter));
                check.Text(handPreviewUnavailable, nameof(handPreviewUnavailable));
                check.Text(modifierInspectionUnavailable, nameof(modifierInspectionUnavailable));
                check.Text(profileLoadFailed, nameof(profileLoadFailed));
                check.Text(encounterSavePending, nameof(encounterSavePending));
                check.Text(profileSavePending, nameof(profileSavePending));
                check.Text(storyFailed, nameof(storyFailed));
                check.Text(visitFailed, nameof(visitFailed));
                check.Separator(separator, nameof(separator));
            }
        }

        /** The main menu's save slot picker and its replacement confirmation. */
        [Serializable]
        public sealed class SaveSlots : SpiritWordingSection
        {
            public string newGameTitle;
            public string loadGameTitle;
            public string recoverTitle;
            public string chooseSlot;
            public string chooseDestination;
            public string replaceWarning;
            public string recoverNote;
            public string newGameButton;
            public string recoverButton;
            /** {count}: the number of campaigns awaiting a slot. */
            public string recoverSavesButton;
            /** {slot}: the slot number. */
            public string slot;
            /** {slot}: the slot number of the active slot. */
            public string activeSlot;
            public string emptySlot;
            public string unreadableSlot;
            /** {encounters} and {charms}: the counted encounters and charms phrases. */
            public string progress;
            /** {count}: completed encounters. */
            public SpiritCountedWording encountersCompleted = new SpiritCountedWording();
            /** {count}: collected charms. */
            public SpiritCountedWording charmsCollected = new SpiritCountedWording();
            /** {slot}: the occupied slot a recovered campaign would replace. */
            public string useSlotConfirmation;
            /** {slot}: the occupied slot a new game would replace. */
            public string replaceSlotConfirmation;

            public string RecoverSavesButton(int count) => SpiritWordingTemplate.Fill(recoverSavesButton, "count", count);
            public string Slot(int slotNumber, bool active) => SpiritWordingTemplate.Fill(active ? activeSlot : slot, "slot", slotNumber);

            public string Progress(int completedEncounters, int collectedCharms)
            {
                string text = SpiritWordingTemplate.Fill(progress, "encounters",
                    SpiritWordingTemplate.Fill(encountersCompleted.For(completedEncounters), "count", completedEncounters));
                return SpiritWordingTemplate.Fill(text, "charms",
                    SpiritWordingTemplate.Fill(charmsCollected.For(collectedCharms), "count", collectedCharms));
            }

            public string UseSlotConfirmation(int slotNumber) => SpiritWordingTemplate.Fill(useSlotConfirmation, "slot", slotNumber);
            public string ReplaceSlotConfirmation(int slotNumber) => SpiritWordingTemplate.Fill(replaceSlotConfirmation, "slot", slotNumber);

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(newGameTitle, nameof(newGameTitle));
                check.Text(loadGameTitle, nameof(loadGameTitle));
                check.Text(recoverTitle, nameof(recoverTitle));
                check.Text(chooseSlot, nameof(chooseSlot));
                check.Text(chooseDestination, nameof(chooseDestination));
                check.Text(replaceWarning, nameof(replaceWarning));
                check.Text(recoverNote, nameof(recoverNote));
                check.Text(newGameButton, nameof(newGameButton));
                check.Text(recoverButton, nameof(recoverButton));
                check.Text(recoverSavesButton, nameof(recoverSavesButton));
                check.Text(slot, nameof(slot));
                check.Text(activeSlot, nameof(activeSlot));
                check.Text(emptySlot, nameof(emptySlot));
                check.Text(unreadableSlot, nameof(unreadableSlot));
                check.Text(progress, nameof(progress));
                check.Counted(encountersCompleted, nameof(encountersCompleted));
                check.Counted(charmsCollected, nameof(charmsCollected));
                check.Text(useSlotConfirmation, nameof(useSlotConfirmation));
                check.Text(replaceSlotConfirmation, nameof(replaceSlotConfirmation));
            }
        }

        /** The picker of preserved campaigns awaiting a save slot; a candidate's progress uses the save slots' wording. */
        [Serializable]
        public sealed class Recovery : SpiritWordingSection
        {
            /** {time}: when the campaign was last saved, written in the time format. */
            public string savedAt;
            /** A .NET date and time format string. */
            public string timeFormat;
            /** {slot}: the suggested slot, or the unknown slot wording; {save}: the start of the campaign's id. */
            public string identifier;
            public string unknownSlot;
            /** {page} and {pages}: the shown page and the page count. */
            public string page;

            public string SavedAt(DateTime time) => SpiritWordingTemplate.Fill(savedAt, "time", time.ToString(timeFormat));

            public string Identifier(int suggestedSlot, string save)
            {
                string slot = suggestedSlot > 0 ? suggestedSlot.ToString(CultureInfo.InvariantCulture) : unknownSlot;
                return SpiritWordingTemplate.Fill(SpiritWordingTemplate.Fill(identifier, "slot", slot), "save", save);
            }

            public string Page(int pageNumber, int pageCount) =>
                SpiritWordingTemplate.Fill(SpiritWordingTemplate.Fill(page, "page", pageNumber), "pages", pageCount);

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(savedAt, nameof(savedAt));
                check.Text(timeFormat, nameof(timeFormat));
                check.Text(identifier, nameof(identifier));
                check.Text(unknownSlot, nameof(unknownSlot));
                check.Text(page, nameof(page));
            }
        }

        /** The collection: its summary, filters, selected charm and where each charm can be obtained. */
        [Serializable]
        public sealed class Collection : SpiritWordingSection
        {
            /** {collected}, {collectable} and {starters}: unlocked, unlockable and starting charms. */
            public string summary;
            public string ownedFilter;
            public string discoveredFilter;
            public string allFilter;
            public string noOwnedFamilies;
            public string noDiscoveredFamilies;
            public string noFamilies;
            public string undiscoveredFamilies;
            public string undiscoveredHint;
            /** {name} and {status}: the selected charm's name and status. */
            public string detailTitle;
            public string available;
            public string encounterOnly;
            public string unlocked;
            public string locked;
            /** {location} and {stages}: an encounter's location and the stages after which it offers the charm. */
            public SpiritCountedWording unlockSource = new SpiritCountedWording();
            public string stageSeparator;
            /** {location}: an encounter's location. */
            public string storyGift;
            /** {location}: an encounter's location. */
            public string completionGift;
            public string noRunRewards;
            public string rewardSeparator;
            public string sourceSeparator;
            /** {sources}: the unlock sources. */
            public string unlockChoices;
            /** {sources}: the gift sources. */
            public string gifts;
            public string sourceLineSeparator;
            public string notObtainable;
            /** {rewards}: the encounters whose run charm pool offers the charm. */
            public string encounterOnlySource;
            /** {rewards}: the encounters whose run charm pool offers the charm. */
            public string starterSource;
            /** {sources}: the unlock and gift sources; {rewards}: the encounters whose run charm pool offers the charm. */
            public string unlockableSource;

            public string Summary(int collected, int collectable, int starters)
            {
                string text = SpiritWordingTemplate.Fill(summary, "collected", collected);
                text = SpiritWordingTemplate.Fill(text, "collectable", collectable);
                return SpiritWordingTemplate.Fill(text, "starters", starters);
            }

            public string FilterName(SpiritCollectionFilter filter)
            {
                switch (filter)
                {
                    case SpiritCollectionFilter.Owned: return ownedFilter;
                    case SpiritCollectionFilter.Discovered: return discoveredFilter;
                    case SpiritCollectionFilter.All: return allFilter;
                    default: throw new ArgumentOutOfRangeException(nameof(filter));
                }
            }

            /** The detail title when the filter shows no family to select. */
            public string NoFamilies(SpiritCollectionFilter filter)
            {
                switch (filter)
                {
                    case SpiritCollectionFilter.Owned: return noOwnedFamilies;
                    case SpiritCollectionFilter.Discovered: return noDiscoveredFamilies;
                    case SpiritCollectionFilter.All: return noFamilies;
                    default: throw new ArgumentOutOfRangeException(nameof(filter));
                }
            }

            public string DetailTitle(string name, string status) =>
                SpiritWordingTemplate.Fill(SpiritWordingTemplate.Fill(detailTitle, "name", name), "status", status);

            public string UnlockSource(string location, IReadOnlyList<int> stages)
            {
                var numbers = new List<string>();
                foreach (int stage in stages) numbers.Add(stage.ToString(CultureInfo.InvariantCulture));
                string text = SpiritWordingTemplate.Fill(unlockSource.For(stages.Count), "location", location);
                return SpiritWordingTemplate.Fill(text, "stages", string.Join(stageSeparator, numbers));
            }

            public string StoryGift(string location) => SpiritWordingTemplate.Fill(storyGift, "location", location);
            public string CompletionGift(string location) => SpiritWordingTemplate.Fill(completionGift, "location", location);
            public string UnlockChoices(IEnumerable<string> sources) =>
                SpiritWordingTemplate.Fill(unlockChoices, "sources", string.Join(sourceSeparator, sources));
            public string Gifts(IEnumerable<string> sources) =>
                SpiritWordingTemplate.Fill(gifts, "sources", string.Join(sourceSeparator, sources));
            public string Rewards(IReadOnlyCollection<string> encounters) =>
                encounters.Count > 0 ? string.Join(rewardSeparator, encounters) : noRunRewards;
            public string EncounterOnlySource(string rewards) => SpiritWordingTemplate.Fill(encounterOnlySource, "rewards", rewards);
            public string StarterSource(string rewards) => SpiritWordingTemplate.Fill(starterSource, "rewards", rewards);

            public string UnlockableSource(string sources, string rewards) =>
                SpiritWordingTemplate.Fill(SpiritWordingTemplate.Fill(unlockableSource, "sources", sources), "rewards", rewards);

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(summary, nameof(summary));
                check.Text(ownedFilter, nameof(ownedFilter));
                check.Text(discoveredFilter, nameof(discoveredFilter));
                check.Text(allFilter, nameof(allFilter));
                check.Text(noOwnedFamilies, nameof(noOwnedFamilies));
                check.Text(noDiscoveredFamilies, nameof(noDiscoveredFamilies));
                check.Text(noFamilies, nameof(noFamilies));
                check.Text(undiscoveredFamilies, nameof(undiscoveredFamilies));
                check.Text(undiscoveredHint, nameof(undiscoveredHint));
                check.Text(detailTitle, nameof(detailTitle));
                check.Text(available, nameof(available));
                check.Text(encounterOnly, nameof(encounterOnly));
                check.Text(unlocked, nameof(unlocked));
                check.Text(locked, nameof(locked));
                check.Counted(unlockSource, nameof(unlockSource));
                check.Separator(stageSeparator, nameof(stageSeparator));
                check.Text(storyGift, nameof(storyGift));
                check.Text(completionGift, nameof(completionGift));
                check.Text(noRunRewards, nameof(noRunRewards));
                check.Separator(rewardSeparator, nameof(rewardSeparator));
                check.Separator(sourceSeparator, nameof(sourceSeparator));
                check.Text(unlockChoices, nameof(unlockChoices));
                check.Text(gifts, nameof(gifts));
                check.Separator(sourceLineSeparator, nameof(sourceLineSeparator));
                check.Text(notObtainable, nameof(notObtainable));
                check.Text(encounterOnlySource, nameof(encounterOnlySource));
                check.Text(starterSource, nameof(starterSource));
                check.Text(unlockableSource, nameof(unlockableSource));
            }
        }

        /** A charm family row whose family is not yet discovered. */
        [Serializable]
        public sealed class FamilyRow : SpiritWordingSection
        {
            public string undiscoveredFamily;
            public string undiscoveredCondition;

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(undiscoveredFamily, nameof(undiscoveredFamily));
                check.Text(undiscoveredCondition, nameof(undiscoveredCondition));
            }
        }

        /** The rewards panel for charm, hand upgrade and unlock offers. */
        [Serializable]
        public sealed class Rewards : SpiritWordingSection
        {
            public string handUpgradeTitle;
            public string exhaustedTitle;
            public string unlockTitle;
            public string charmTitle;
            public string upgradeButton;
            public string continueButton;
            public string unlockButton;
            public string selectButton;
            public string exhaustedInstruction;
            public string unlockInstruction;
            /** {count}: the refreshes available. */
            public string refreshButton;

            public string RefreshButton(int count) => SpiritWordingTemplate.Fill(refreshButton, "count", count);

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(handUpgradeTitle, nameof(handUpgradeTitle));
                check.Text(exhaustedTitle, nameof(exhaustedTitle));
                check.Text(unlockTitle, nameof(unlockTitle));
                check.Text(charmTitle, nameof(charmTitle));
                check.Text(upgradeButton, nameof(upgradeButton));
                check.Text(continueButton, nameof(continueButton));
                check.Text(unlockButton, nameof(unlockButton));
                check.Text(selectButton, nameof(selectButton));
                check.Text(exhaustedInstruction, nameof(exhaustedInstruction));
                check.Text(unlockInstruction, nameof(unlockInstruction));
                check.Text(refreshButton, nameof(refreshButton));
            }
        }

        /** A hand upgrade reward card, before and while it is selected. */
        [Serializable]
        public sealed class RewardCard : SpiritWordingSection
        {
            /** {level}: the hand's current level. */
            public string level;
            /** {level}: the hand's current level, which the selected upgrade raises. */
            public string selectedLevel;
            /** {base} and {multiplier}: the hand's current values. */
            public string values;
            /** {base}, {multiplier}, {baseIncrement} and {multiplierIncrement}: current values and the upgrade's increments. */
            public string selectedValues;

            public string Level(int handLevel, bool selected) =>
                SpiritWordingTemplate.Fill(selected ? selectedLevel : level, "level", handLevel);

            public string Values(long baseScore, long multiplier, long baseIncrement, long multiplierIncrement, bool selected)
            {
                string text = SpiritWordingTemplate.FillScore(selected ? selectedValues : values, "base", baseScore);
                text = SpiritWordingTemplate.FillScore(text, "multiplier", multiplier);
                text = SpiritWordingTemplate.FillScore(text, "baseIncrement", baseIncrement);
                return SpiritWordingTemplate.FillScore(text, "multiplierIncrement", multiplierIncrement);
            }

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(level, nameof(level));
                check.Text(selectedLevel, nameof(selectedLevel));
                check.Text(values, nameof(values));
                check.Text(selectedValues, nameof(selectedValues));
            }
        }

        /** A charm's description on cards, rows, popups and journey beats. */
        [Serializable]
        public sealed class CharmText : SpiritWordingSection
        {
            /** {lead} and {condition}: the phase's description lead and the charm's condition. */
            public string description;
            /** {lead}, {condition} and {note}: as the description, with the activation limit's note. */
            public string descriptionWithNote;
            /**
             * Terms emphasized in a charm's condition, matched as whole words regardless of case; numbers are always
             * emphasized. A term listed before another is preferred where both match at the same place.
             */
            public string[] highlightedTerms = Array.Empty<string>();

            public string Description(string lead, string condition, string note)
            {
                string text = SpiritWordingTemplate.Fill(note.Length > 0 ? descriptionWithNote : description, "lead", lead);
                text = SpiritWordingTemplate.Fill(text, "condition", condition);
                return SpiritWordingTemplate.Fill(text, "note", note);
            }

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(description, nameof(description));
                check.Text(descriptionWithNote, nameof(descriptionWithNote));
                check.Terms(highlightedTerms, nameof(highlightedTerms));
            }
        }

        /** The name of every hand category. */
        [Serializable]
        public sealed class HandNames : SpiritWordingSection
        {
            public string highDie;
            public string pair;
            public string twoPair;
            public string threeOfAKind;
            public string fullHouse;
            public string fourOfAKind;
            public string straight;
            public string fiveOfAKind;

            public string Name(HandCategory category)
            {
                switch (category)
                {
                    case HandCategory.HighDie: return highDie;
                    case HandCategory.Pair: return pair;
                    case HandCategory.TwoPair: return twoPair;
                    case HandCategory.ThreeOfAKind: return threeOfAKind;
                    case HandCategory.FullHouse: return fullHouse;
                    case HandCategory.FourOfAKind: return fourOfAKind;
                    case HandCategory.Straight: return straight;
                    case HandCategory.FiveOfAKind: return fiveOfAKind;
                    default: throw new ArgumentOutOfRangeException(nameof(category));
                }
            }

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(highDie, nameof(highDie));
                check.Text(pair, nameof(pair));
                check.Text(twoPair, nameof(twoPair));
                check.Text(threeOfAKind, nameof(threeOfAKind));
                check.Text(fullHouse, nameof(fullHouse));
                check.Text(fourOfAKind, nameof(fourOfAKind));
                check.Text(straight, nameof(straight));
                check.Text(fiveOfAKind, nameof(fiveOfAKind));
            }
        }

        /** The hand ranks panel; each row's category is named by the hand names. */
        [Serializable]
        public sealed class HandRanks : SpiritWordingSection
        {
            /** {level}: the category's level. */
            public string level;
            /** {base}: the category's base score at its level. */
            public string baseBonus;
            /** {multiplier}: the category's multiplier at its level. */
            public string multiplierBonus;

            public string Level(int handLevel) => SpiritWordingTemplate.Fill(level, "level", handLevel);
            public string BaseBonus(long baseScore) => SpiritWordingTemplate.FillScore(baseBonus, "base", baseScore);
            public string MultiplierBonus(long multiplier) => SpiritWordingTemplate.FillScore(multiplierBonus, "multiplier", multiplier);

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(level, nameof(level));
                check.Text(baseBonus, nameof(baseBonus));
                check.Text(multiplierBonus, nameof(multiplierBonus));
            }
        }

        /** The labels of score playback's steps; {contribution} is the charm skin's contribution wording. */
        [Serializable]
        public sealed class Scoring : SpiritWordingSection
        {
            public string diceStep;
            public string diceCategory;
            /** {contribution}: the dice sum. */
            public string diceSelection;
            public string handStep;
            /** {hand}, {level} and {contribution}: the hand's upper-cased name, its level and its bonus. */
            public string handSelection;
            public string charmStep;
            public string bankedStep;
            /** {charm} and {contribution}: the charm's name and its contribution on play. */
            public string charmSelection;
            /** {charm} and {contribution}: the charm's name and its banked contribution. */
            public string bankedSelection;
            public string ruleStep;
            public string ruleCategory;
            /** {rule} and {bonus}: the rule's name and its multiplier bonus. */
            public string ruleSelection;
            /** {base} and {multiplier}: the hand's final base score and multiplier. */
            public string totalStep;
            public string totalCategory;
            public string totalSelection;

            public string DiceSelection(string contribution) => SpiritWordingTemplate.Fill(diceSelection, "contribution", contribution);

            public string HandSelection(string hand, int level, string contribution)
            {
                string text = SpiritWordingTemplate.Fill(handSelection, "hand", hand);
                text = SpiritWordingTemplate.Fill(text, "level", level);
                return SpiritWordingTemplate.Fill(text, "contribution", contribution);
            }

            public string CharmSelection(string charm, string contribution, bool banked) =>
                SpiritWordingTemplate.Fill(SpiritWordingTemplate.Fill(banked ? bankedSelection : charmSelection, "charm", charm),
                    "contribution", contribution);

            public string RuleSelection(string rule, long bonus) =>
                SpiritWordingTemplate.FillScore(SpiritWordingTemplate.Fill(ruleSelection, "rule", rule), "bonus", bonus);

            public string TotalStep(long baseScore, long multiplier) =>
                SpiritWordingTemplate.FillScore(SpiritWordingTemplate.FillScore(totalStep, "base", baseScore), "multiplier", multiplier);

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(diceStep, nameof(diceStep));
                check.Text(diceCategory, nameof(diceCategory));
                check.Text(diceSelection, nameof(diceSelection));
                check.Text(handStep, nameof(handStep));
                check.Text(handSelection, nameof(handSelection));
                check.Text(charmStep, nameof(charmStep));
                check.Text(bankedStep, nameof(bankedStep));
                check.Text(charmSelection, nameof(charmSelection));
                check.Text(bankedSelection, nameof(bankedSelection));
                check.Text(ruleStep, nameof(ruleStep));
                check.Text(ruleCategory, nameof(ruleCategory));
                check.Text(ruleSelection, nameof(ruleSelection));
                check.Text(totalStep, nameof(totalStep));
                check.Text(totalCategory, nameof(totalCategory));
                check.Text(totalSelection, nameof(totalSelection));
            }
        }

        /** The gameplay HUD: header, target, resources, footer and the stage intro. */
        [Serializable]
        public sealed class Hud : SpiritWordingSection
        {
            /** {stage} and {stages}: the stage's number and the stage count. */
            public string stage;
            public string toClear;
            public string stageCleared;
            public string totalScore;
            /** {count}: hands remaining. */
            public string hands;
            /** {count}: rerolls remaining. */
            public string rerolls;
            /** {hands} and {rerolls}: the counted hands and rerolls left. */
            public string resources;
            /** {count}: hands left. */
            public SpiritCountedWording handsLeft = new SpiritCountedWording();
            /** {count}: rerolls left. */
            public SpiritCountedWording rerollsLeft = new SpiritCountedWording();
            /** {score}: the last hand's score. */
            public SpiritCountedWording lastHand = new SpiritCountedWording();
            /** {score}: the encounter's total score. */
            public string totalScoreLine;
            /** {count}: charms kept this run. */
            public string charmCount;
            /** {count}: charms kept this run. */
            public string charmsButton;
            public string newHand;
            /** {count}: rerolls remaining. */
            public SpiritCountedWording rerollsRemaining = new SpiritCountedWording();
            /** {threshold}, {hands} and {rerolls}: the stage's threshold and its counted hands and rerolls. */
            public SpiritCountedWording stageIntro = new SpiritCountedWording();
            /** {count}: the stage's hands. */
            public SpiritCountedWording stageHands = new SpiritCountedWording();
            /** {count}: the stage's rerolls. */
            public SpiritCountedWording stageRerolls = new SpiritCountedWording();

            public string Stage(int stageNumber, int stageCount) =>
                SpiritWordingTemplate.Fill(SpiritWordingTemplate.Fill(stage, "stage", stageNumber), "stages", stageCount);

            public string Hands(int count) => SpiritWordingTemplate.Fill(hands, "count", count);
            public string Rerolls(int count) => SpiritWordingTemplate.Fill(rerolls, "count", count);

            public string Resources(int handCount, int rerollCount)
            {
                string text = SpiritWordingTemplate.Fill(resources, "hands", Counted(handsLeft, handCount));
                return SpiritWordingTemplate.Fill(text, "rerolls", Counted(rerollsLeft, rerollCount));
            }

            public string LastHand(long score) => SpiritWordingTemplate.FillScore(lastHand.For(score), "score", score);
            public string TotalScoreLine(long score) => SpiritWordingTemplate.FillScore(totalScoreLine, "score", score);
            public string CharmCount(int count) => SpiritWordingTemplate.Fill(charmCount, "count", count);
            public string CharmsButton(int count) => SpiritWordingTemplate.Fill(charmsButton, "count", count);
            public string RerollsRemaining(int count) => Counted(rerollsRemaining, count);

            public string StageIntro(long threshold, int handCount, int rerollCount)
            {
                string text = SpiritWordingTemplate.FillScore(stageIntro.For(threshold), "threshold", threshold);
                text = SpiritWordingTemplate.Fill(text, "hands", Counted(stageHands, handCount));
                return SpiritWordingTemplate.Fill(text, "rerolls", Counted(stageRerolls, rerollCount));
            }

            private static string Counted(SpiritCountedWording wording, int count) =>
                SpiritWordingTemplate.Fill(wording.For(count), "count", count);

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(stage, nameof(stage));
                check.Text(toClear, nameof(toClear));
                check.Text(stageCleared, nameof(stageCleared));
                check.Text(totalScore, nameof(totalScore));
                check.Text(hands, nameof(hands));
                check.Text(rerolls, nameof(rerolls));
                check.Text(resources, nameof(resources));
                check.Counted(handsLeft, nameof(handsLeft));
                check.Counted(rerollsLeft, nameof(rerollsLeft));
                check.Counted(lastHand, nameof(lastHand));
                check.Text(totalScoreLine, nameof(totalScoreLine));
                check.Text(charmCount, nameof(charmCount));
                check.Text(charmsButton, nameof(charmsButton));
                check.Text(newHand, nameof(newHand));
                check.Counted(rerollsRemaining, nameof(rerollsRemaining));
                check.Counted(stageIntro, nameof(stageIntro));
                check.Counted(stageHands, nameof(stageHands));
                check.Counted(stageRerolls, nameof(stageRerolls));
            }
        }

        /** The encounter's pause menu: its sound toggle and the player profile summary. */
        [Serializable]
        public sealed class PauseMenu : SpiritWordingSection
        {
            public string soundOn;
            public string soundOff;
            /** {charms} and {encounters}: the counted unlocked charms and completed encounters. */
            public string profileSummary;
            /** {count}: charms permanently unlocked. */
            public SpiritCountedWording charmsUnlocked = new SpiritCountedWording();
            /** {count}: encounters completed. */
            public SpiritCountedWording encountersCompleted = new SpiritCountedWording();
            /** {problem}: what needs attention. */
            public string profileAttention;
            public string progressPending;
            public string profileUnreadable;

            public string Sound(bool muted) => muted ? soundOff : soundOn;

            public string ProfileSummary(int unlockedCharms, int completedEncounters)
            {
                string text = SpiritWordingTemplate.Fill(profileSummary, "charms",
                    SpiritWordingTemplate.Fill(charmsUnlocked.For(unlockedCharms), "count", unlockedCharms));
                return SpiritWordingTemplate.Fill(text, "encounters",
                    SpiritWordingTemplate.Fill(encountersCompleted.For(completedEncounters), "count", completedEncounters));
            }

            public string ProfileAttention(bool progressIsPending) =>
                SpiritWordingTemplate.Fill(profileAttention, "problem", progressIsPending ? progressPending : profileUnreadable);

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(soundOn, nameof(soundOn));
                check.Text(soundOff, nameof(soundOff));
                check.Text(profileSummary, nameof(profileSummary));
                check.Counted(charmsUnlocked, nameof(charmsUnlocked));
                check.Counted(encountersCompleted, nameof(encountersCompleted));
                check.Text(profileAttention, nameof(profileAttention));
                check.Text(progressPending, nameof(progressPending));
                check.Text(profileUnreadable, nameof(profileUnreadable));
            }
        }

        /** An owned charm's status detail; a kept charm shows none. */
        [Serializable]
        public sealed class CharmStatus : SpiritWordingSection
        {
            public string ready;
            public string waiting;
            public string activatedThisStage;
            public string activatedThisHand;
            public string onInitialRoll;
            public string afterReroll;
            public string encounterEnded;
            public string betweenStages;

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(ready, nameof(ready));
                check.Text(waiting, nameof(waiting));
                check.Text(activatedThisStage, nameof(activatedThisStage));
                check.Text(activatedThisHand, nameof(activatedThisHand));
                check.Text(onInitialRoll, nameof(onInitialRoll));
                check.Text(afterReroll, nameof(afterReroll));
                check.Text(encounterEnded, nameof(encounterEnded));
                check.Text(betweenStages, nameof(betweenStages));
            }
        }

        /** How a charm is obtained and where it can appear, on owned charm details and unlock summaries. */
        [Serializable]
        public sealed class CharmAvailability : SpiritWordingSection
        {
            public string permanentlyUnlocked;
            public string requiresUnlock;
            public string encounterExclusive;
            public string availableFromStart;
            /** {access} and {scope}: how the charm is obtained and its scope line. */
            public string availability;
            public string allEncounters;
            public string spiritEncounters;
            public string thisEncounter;
            /** {scope} and {tier}: where the charm can appear and its power tier. */
            public string scopeTier;

            public string Availability(string access, string scope) =>
                SpiritWordingTemplate.Fill(SpiritWordingTemplate.Fill(availability, "access", access), "scope", scope);

            public string ScopeTier(string scope, int tier) =>
                SpiritWordingTemplate.Fill(SpiritWordingTemplate.Fill(scopeTier, "scope", scope), "tier", tier);

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(permanentlyUnlocked, nameof(permanentlyUnlocked));
                check.Text(requiresUnlock, nameof(requiresUnlock));
                check.Text(encounterExclusive, nameof(encounterExclusive));
                check.Text(availableFromStart, nameof(availableFromStart));
                check.Text(availability, nameof(availability));
                check.Text(allEncounters, nameof(allEncounters));
                check.Text(spiritEncounters, nameof(spiritEncounters));
                check.Text(thisEncounter, nameof(thisEncounter));
                check.Text(scopeTier, nameof(scopeTier));
            }
        }

        /** The owned charms panel's detail. */
        [Serializable]
        public sealed class OwnedCharms : SpiritWordingSection
        {
            public string noCharmsTitle;
            public string noCharmsBody;
            /** {condition}, {effect}, {timing} and {availability}: the selected charm's lines. */
            public string detailBody;

            public string DetailBody(string condition, string effect, string timing, string availability)
            {
                string text = SpiritWordingTemplate.Fill(detailBody, "condition", condition);
                text = SpiritWordingTemplate.Fill(text, "effect", effect);
                text = SpiritWordingTemplate.Fill(text, "timing", timing);
                return SpiritWordingTemplate.Fill(text, "availability", availability);
            }

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(noCharmsTitle, nameof(noCharmsTitle));
                check.Text(noCharmsBody, nameof(noCharmsBody));
                check.Text(detailBody, nameof(detailBody));
            }
        }

        /** The encounter ending's summary and the run's unlock summary. */
        [Serializable]
        public sealed class Ending : SpiritWordingSection
        {
            public string returnButton;
            public string abandonedTitle;
            public string lostTitle;
            public string wonTitle;
            public string abandonedDetail;
            /** {score}: the encounter's total score. */
            public string scoreSummary;
            /** {farewell} and {score}: the host's farewell and the encounter's total score. */
            public string farewellScoreSummary;
            public string noUnlocks;
            /** {charms}: the unlocked charms, while their save is pending. */
            public string pendingUnlocks;
            /** {charms}: the unlocked charms. */
            public string permanentUnlocks;
            /** {name} and {scope}: an unlocked charm's name and scope line. */
            public string unlockedCharm;
            public string unlockedCharmSeparator;

            public string Summary(string farewell, long score) => string.IsNullOrWhiteSpace(farewell)
                ? SpiritWordingTemplate.FillScore(scoreSummary, "score", score)
                : SpiritWordingTemplate.FillScore(SpiritWordingTemplate.Fill(farewellScoreSummary, "farewell", farewell), "score", score);

            public string UnlockedCharm(string name, string scope) =>
                SpiritWordingTemplate.Fill(SpiritWordingTemplate.Fill(unlockedCharm, "name", name), "scope", scope);

            public string Unlocks(IEnumerable<string> charms, bool pending) =>
                SpiritWordingTemplate.Fill(pending ? pendingUnlocks : permanentUnlocks, "charms", string.Join(unlockedCharmSeparator, charms));

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(returnButton, nameof(returnButton));
                check.Text(abandonedTitle, nameof(abandonedTitle));
                check.Text(lostTitle, nameof(lostTitle));
                check.Text(wonTitle, nameof(wonTitle));
                check.Text(abandonedDetail, nameof(abandonedDetail));
                check.Text(scoreSummary, nameof(scoreSummary));
                check.Text(farewellScoreSummary, nameof(farewellScoreSummary));
                check.Text(noUnlocks, nameof(noUnlocks));
                check.Text(pendingUnlocks, nameof(pendingUnlocks));
                check.Text(permanentUnlocks, nameof(permanentUnlocks));
                check.Text(unlockedCharm, nameof(unlockedCharm));
                check.Separator(unlockedCharmSeparator, nameof(unlockedCharmSeparator));
            }
        }

        /** A journey beat's buttons and its invitation's rule heading; a beat's own continue label takes precedence. */
        [Serializable]
        public sealed class Journey : SpiritWordingSection
        {
            public string acceptButton;
            public string continueButton;
            /** The heading over an invitation's rules, counted by the rules shown. */
            public SpiritCountedWording ruleHeading = new SpiritCountedWording();

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(acceptButton, nameof(acceptButton));
                check.Text(continueButton, nameof(continueButton));
                check.Counted(ruleHeading, nameof(ruleHeading));
            }
        }

        /** The investigation board, its leads and its buttons. */
        [Serializable]
        public sealed class Investigation : SpiritWordingSection
        {
            /** {chapter}: the selected chapter's number. */
            public string chapterProgress;
            public string chapters;
            public string defaultTitle;
            public string readAgain;
            public string followLead;
            public string visitedLead;
            public string mainMenuButton;
            public string loadButton;

            public string ChapterProgress(int chapter) => SpiritWordingTemplate.Fill(chapterProgress, "chapter", chapter);

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(chapterProgress, nameof(chapterProgress));
                check.Text(chapters, nameof(chapters));
                check.Text(defaultTitle, nameof(defaultTitle));
                check.Text(readAgain, nameof(readAgain));
                check.Text(followLead, nameof(followLead));
                check.Text(visitedLead, nameof(visitedLead));
                check.Text(mainMenuButton, nameof(mainMenuButton));
                check.Text(loadButton, nameof(loadButton));
            }
        }

        /** The chapter selector's cards. */
        [Serializable]
        public sealed class Chapters : SpiritWordingSection
        {
            public string locked;
            public string current;
            public string currentCompleted;
            public string completed;
            public string available;
            /** {chapter}: the chapter's number. */
            public string number;

            public string Number(int chapter) => SpiritWordingTemplate.Fill(number, "chapter", chapter);

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(locked, nameof(locked));
                check.Text(current, nameof(current));
                check.Text(currentCompleted, nameof(currentCompleted));
                check.Text(completed, nameof(completed));
                check.Text(available, nameof(available));
                check.Text(number, nameof(number));
            }
        }

        /** The encounter charms screen's narration. */
        [Serializable]
        public sealed class EncounterCharms : SpiritWordingSection
        {
            /** {narration}: the journey's narration. */
            public string narration;

            public string Narration(string text) => SpiritWordingTemplate.Fill(narration, "narration", text);

            public override void Validate(SpiritWordingCheck check)
            {
                check.Text(narration, nameof(narration));
            }
        }

        public StatusText status = new StatusText();
        public SaveSlots saveSlots = new SaveSlots();
        public Recovery recovery = new Recovery();
        public Collection collection = new Collection();
        public FamilyRow familyRow = new FamilyRow();
        public Rewards rewards = new Rewards();
        public RewardCard rewardCard = new RewardCard();
        public CharmText charmText = new CharmText();
        public HandNames handNames = new HandNames();
        public HandRanks handRanks = new HandRanks();
        public Scoring scoring = new Scoring();
        public Hud hud = new Hud();
        public PauseMenu pauseMenu = new PauseMenu();
        public CharmStatus charmStatus = new CharmStatus();
        public CharmAvailability charmAvailability = new CharmAvailability();
        public OwnedCharms ownedCharms = new OwnedCharms();
        public Ending ending = new Ending();
        public Journey journey = new Journey();
        public Investigation investigation = new Investigation();
        public Chapters chapters = new Chapters();
        public EncounterCharms encounterCharms = new EncounterCharms();

        /** Fails naming the first missing field; screens validate the wording when they are configured. */
        public void Validate()
        {
            Validate(status, nameof(status));
            Validate(saveSlots, nameof(saveSlots));
            Validate(recovery, nameof(recovery));
            Validate(collection, nameof(collection));
            Validate(familyRow, nameof(familyRow));
            Validate(rewards, nameof(rewards));
            Validate(rewardCard, nameof(rewardCard));
            Validate(charmText, nameof(charmText));
            Validate(handNames, nameof(handNames));
            Validate(handRanks, nameof(handRanks));
            Validate(scoring, nameof(scoring));
            Validate(hud, nameof(hud));
            Validate(pauseMenu, nameof(pauseMenu));
            Validate(charmStatus, nameof(charmStatus));
            Validate(charmAvailability, nameof(charmAvailability));
            Validate(ownedCharms, nameof(ownedCharms));
            Validate(ending, nameof(ending));
            Validate(journey, nameof(journey));
            Validate(investigation, nameof(investigation));
            Validate(chapters, nameof(chapters));
            Validate(encounterCharms, nameof(encounterCharms));
        }

        private void Validate(SpiritWordingSection section, string sectionName)
        {
            if (section == null) throw new InvalidOperationException("The UI wording " + name + " has no " + sectionName + " section.");
            section.Validate(new SpiritWordingCheck(name, sectionName));
        }
    }
}
