using Dice.Presentation;
using UnityEngine;

namespace Dice.Tests
{
    /**
     * A fabricated Spirit House wording asset: every text field is its section and field name in angle brackets followed
     * by the template's placeholders joined by bars, counted wording marks its singular and plural forms, and separators are the field
     * name between bars. Tests compare composed text with these templates filled with their values.
     */
    internal static class SyntheticWording
    {
        /** A complete wording asset; the caller destroys it. */
        public static SpiritUiWording Create()
        {
            var w = ScriptableObject.CreateInstance<SpiritUiWording>();
            w.name = "SyntheticWording";
            Fill(w);
            return w;
        }

        private static void Fill(SpiritUiWording w)
        {
            var s = w.status;
            s.waitingForStartup = "<status.waitingForStartup>";
            s.loadingConfiguration = "<status.loadingConfiguration>";
            s.configurationFailed = "<status.configurationFailed>{detail}";
            s.applicationFailed = "<status.applicationFailed>{detail}";
            s.contentFailed = "<status.contentFailed>{detail}";
            s.encounterInitializationFailed = "<status.encounterInitializationFailed>{detail}";
            s.presentationSceneMissing = "<status.presentationSceneMissing>";
            s.loadingPresentation = "<status.loadingPresentation>";
            s.presentationOpenFailed = "<status.presentationOpenFailed>{detail}";
            s.presentationInitializationFailed = "<status.presentationInitializationFailed>{detail}";
            s.mainMenuReady = "<status.mainMenuReady>";
            s.mainMenuFailed = "<status.mainMenuFailed>{detail}";
            s.saveSlotsRecovered = "<status.saveSlotsRecovered>";
            s.unassignedSavesRecovered = "<status.unassignedSavesRecovered>";
            s.saveSlotsFailed = "<status.saveSlotsFailed>{detail}";
            s.saveSlotSelectionFailed = "<status.saveSlotSelectionFailed>{detail}";
            s.newGameFailed = "<status.newGameFailed>{detail}";
            s.previousCampaignNotRemoved = "<status.previousCampaignNotRemoved>{detail}";
            s.campaignRecovered = "<status.campaignRecovered>{slot}";
            s.campaignRecoveryFailed = "<status.campaignRecoveryFailed>{detail}";
            s.investigationReady = "<status.investigationReady>";
            s.investigationFailed = "<status.investigationFailed>{detail}";
            s.chapterNotFound = "<status.chapterNotFound>";
            s.chapterLocked = "<status.chapterLocked>";
            s.leadNotFound = "<status.leadNotFound>";
            s.locationLocked = "<status.locationLocked>";
            s.savedEncounterHasNoLead = "<status.savedEncounterHasNoLead>";
            s.activeEncounterHasNoLead = "<status.activeEncounterHasNoLead>";
            s.invitationFailed = "<status.invitationFailed>{detail}";
            s.rewardFailed = "<status.rewardFailed>{detail}";
            s.contentNotInitialized = "<status.contentNotInitialized>";
            s.startFailed = "<status.startFailed>{detail}";
            s.started = "<status.started>{seed}";
            s.saved = "<status.saved>";
            s.saveFailed = "<status.saveFailed>{detail}";
            s.catalogRequired = "<status.catalogRequired>";
            s.loaded = "<status.loaded>";
            s.loadFailed = "<status.loadFailed>{detail}";
            s.ready = "<status.ready>";
            s.actionAccepted = "<status.actionAccepted>{revision}";
            s.actionRejected = "<status.actionRejected>{rejection}";
            s.actionFailed = "<status.actionFailed>{detail}";
            s.noEncounter = "<status.noEncounter>";
            s.handPreviewUnavailable = "<status.handPreviewUnavailable>{detail}";
            s.modifierInspectionUnavailable = "<status.modifierInspectionUnavailable>{detail}";
            s.profileLoadFailed = "<status.profileLoadFailed>{detail}";
            s.encounterSavePending = "<status.encounterSavePending>{detail}";
            s.profileSavePending = "<status.profileSavePending>{detail}";
            s.storyFailed = "<status.storyFailed>{detail}";
            s.visitFailed = "<status.visitFailed>{detail}";
            s.separator = " |separator| ";

            var slots = w.saveSlots;
            slots.newGameTitle = "<saveSlots.newGameTitle>";
            slots.loadGameTitle = "<saveSlots.loadGameTitle>";
            slots.recoverTitle = "<saveSlots.recoverTitle>";
            slots.chooseSlot = "<saveSlots.chooseSlot>";
            slots.chooseDestination = "<saveSlots.chooseDestination>";
            slots.replaceWarning = "<saveSlots.replaceWarning>";
            slots.recoverNote = "<saveSlots.recoverNote>";
            slots.newGameButton = "<saveSlots.newGameButton>";
            slots.recoverButton = "<saveSlots.recoverButton>";
            slots.recoverSavesButton = "<saveSlots.recoverSavesButton>{count}";
            slots.slot = "<saveSlots.slot>{slot}";
            slots.activeSlot = "<saveSlots.activeSlot>{slot}";
            slots.emptySlot = "<saveSlots.emptySlot>";
            slots.unreadableSlot = "<saveSlots.unreadableSlot>";
            slots.progress = "<saveSlots.progress>{encounters}|{charms}";
            slots.encountersCompleted = Counted("<saveSlots.encountersCompleted.one>{count}", "<saveSlots.encountersCompleted.many>{count}");
            slots.charmsCollected = Counted("<saveSlots.charmsCollected.one>{count}", "<saveSlots.charmsCollected.many>{count}");
            slots.useSlotConfirmation = "<saveSlots.useSlotConfirmation>{slot}";
            slots.replaceSlotConfirmation = "<saveSlots.replaceSlotConfirmation>{slot}";

            var recovery = w.recovery;
            recovery.savedAt = "<recovery.savedAt>{time}";
            recovery.timeFormat = "yyyyMMdd";
            recovery.identifier = "<recovery.identifier>{slot}|{save}";
            recovery.unknownSlot = "<recovery.unknownSlot>";
            recovery.page = "<recovery.page>{page}|{pages}";

            var c = w.collection;
            c.summary = "<collection.summary>{collected}|{collectable}|{starters}";
            c.ownedFilter = "<collection.ownedFilter>";
            c.discoveredFilter = "<collection.discoveredFilter>";
            c.allFilter = "<collection.allFilter>";
            c.noOwnedFamilies = "<collection.noOwnedFamilies>";
            c.noDiscoveredFamilies = "<collection.noDiscoveredFamilies>";
            c.noFamilies = "<collection.noFamilies>";
            c.undiscoveredFamilies = "<collection.undiscoveredFamilies>";
            c.undiscoveredHint = "<collection.undiscoveredHint>";
            c.detailTitle = "<collection.detailTitle>{name}|{status}";
            c.available = "<collection.available>";
            c.encounterOnly = "<collection.encounterOnly>";
            c.unlocked = "<collection.unlocked>";
            c.locked = "<collection.locked>";
            c.unlockSource = Counted("<collection.unlockSource.one>{location}|{stages}", "<collection.unlockSource.many>{location}|{stages}");
            c.stageSeparator = " |stageSeparator| ";
            c.storyGift = "<collection.storyGift>{location}";
            c.completionGift = "<collection.completionGift>{location}";
            c.noRunRewards = "<collection.noRunRewards>";
            c.rewardSeparator = " |rewardSeparator| ";
            c.sourceSeparator = " |sourceSeparator| ";
            c.unlockChoices = "<collection.unlockChoices>{sources}";
            c.gifts = "<collection.gifts>{sources}";
            c.sourceLineSeparator = " |sourceLineSeparator| ";
            c.notObtainable = "<collection.notObtainable>";
            c.encounterOnlySource = "<collection.encounterOnlySource>{rewards}";
            c.starterSource = "<collection.starterSource>{rewards}";
            c.unlockableSource = "<collection.unlockableSource>{sources}|{rewards}";

            w.familyRow.undiscoveredFamily = "<familyRow.undiscoveredFamily>";
            w.familyRow.undiscoveredCondition = "<familyRow.undiscoveredCondition>";

            var r = w.rewards;
            r.handUpgradeTitle = "<rewards.handUpgradeTitle>";
            r.exhaustedTitle = "<rewards.exhaustedTitle>";
            r.unlockTitle = "<rewards.unlockTitle>";
            r.charmTitle = "<rewards.charmTitle>";
            r.upgradeButton = "<rewards.upgradeButton>";
            r.continueButton = "<rewards.continueButton>";
            r.unlockButton = "<rewards.unlockButton>";
            r.selectButton = "<rewards.selectButton>";
            r.exhaustedInstruction = "<rewards.exhaustedInstruction>";
            r.unlockInstruction = "<rewards.unlockInstruction>";
            r.refreshButton = "<rewards.refreshButton>{count}";

            var card = w.rewardCard;
            card.level = "<rewardCard.level>{level}";
            card.selectedLevel = "<rewardCard.selectedLevel>{level}";
            card.values = "<rewardCard.values>{base}|{multiplier}";
            card.selectedValues = "<rewardCard.selectedValues>{base}|{baseIncrement}|{multiplier}|{multiplierIncrement}";

            var charm = w.charmText;
            charm.description = "<charmText.description>{lead}|{condition}";
            charm.descriptionWithNote = "<charmText.descriptionWithNote>{lead}|{condition}|{note}";
            charm.highlightedTerms = new[] { "Two Pair", "Pair", "hands" };

            var hands = w.handNames;
            hands.highDie = "<handNames.highDie>";
            hands.pair = "<handNames.pair>";
            hands.twoPair = "<handNames.twoPair>";
            hands.threeOfAKind = "<handNames.threeOfAKind>";
            hands.fullHouse = "<handNames.fullHouse>";
            hands.fourOfAKind = "<handNames.fourOfAKind>";
            hands.straight = "<handNames.straight>";
            hands.fiveOfAKind = "<handNames.fiveOfAKind>";

            w.handRanks.level = "<handRanks.level>{level}";
            w.handRanks.baseBonus = "<handRanks.baseBonus>{base}";
            w.handRanks.multiplierBonus = "<handRanks.multiplierBonus>{multiplier}";

            var scoring = w.scoring;
            scoring.diceStep = "<scoring.diceStep>";
            scoring.diceCategory = "<scoring.diceCategory>";
            scoring.diceSelection = "<scoring.diceSelection>{contribution}";
            scoring.handStep = "<scoring.handStep>";
            scoring.handSelection = "<scoring.handSelection>{hand}|{level}|{contribution}";
            scoring.charmStep = "<scoring.charmStep>";
            scoring.bankedStep = "<scoring.bankedStep>";
            scoring.charmSelection = "<scoring.charmSelection>{charm}|{contribution}";
            scoring.bankedSelection = "<scoring.bankedSelection>{charm}|{contribution}";
            scoring.ruleStep = "<scoring.ruleStep>";
            scoring.ruleCategory = "<scoring.ruleCategory>";
            scoring.ruleSelection = "<scoring.ruleSelection>{rule}|{bonus}";
            scoring.totalStep = "<scoring.totalStep>{base}|{multiplier}";
            scoring.totalCategory = "<scoring.totalCategory>";
            scoring.totalSelection = "<scoring.totalSelection>";

            var hud = w.hud;
            hud.stage = "<hud.stage>{stage}|{stages}";
            hud.toClear = "<hud.toClear>";
            hud.stageCleared = "<hud.stageCleared>";
            hud.totalScore = "<hud.totalScore>";
            hud.hands = "<hud.hands>{count}";
            hud.rerolls = "<hud.rerolls>{count}";
            hud.resources = "<hud.resources>{hands}|{rerolls}";
            hud.handsLeft = Counted("<hud.handsLeft.one>{count}", "<hud.handsLeft.many>{count}");
            hud.rerollsLeft = Counted("<hud.rerollsLeft.one>{count}", "<hud.rerollsLeft.many>{count}");
            hud.lastHand = Counted("<hud.lastHand.one>{score}", "<hud.lastHand.many>{score}");
            hud.totalScoreLine = "<hud.totalScoreLine>{score}";
            hud.charmCount = "<hud.charmCount>{count}";
            hud.charmsButton = "<hud.charmsButton>{count}";
            hud.newHand = "<hud.newHand>";
            hud.rerollsRemaining = Counted("<hud.rerollsRemaining.one>{count}", "<hud.rerollsRemaining.many>{count}");
            hud.stageIntro = Counted("<hud.stageIntro.one>{threshold}|{hands}|{rerolls}", "<hud.stageIntro.many>{threshold}|{hands}|{rerolls}");
            hud.stageHands = Counted("<hud.stageHands.one>{count}", "<hud.stageHands.many>{count}");
            hud.stageRerolls = Counted("<hud.stageRerolls.one>{count}", "<hud.stageRerolls.many>{count}");

            var pause = w.pauseMenu;
            pause.soundOn = "<pauseMenu.soundOn>";
            pause.soundOff = "<pauseMenu.soundOff>";
            pause.profileSummary = "<pauseMenu.profileSummary>{charms}|{encounters}";
            pause.charmsUnlocked = Counted("<pauseMenu.charmsUnlocked.one>{count}", "<pauseMenu.charmsUnlocked.many>{count}");
            pause.encountersCompleted = Counted("<pauseMenu.encountersCompleted.one>{count}", "<pauseMenu.encountersCompleted.many>{count}");
            pause.profileAttention = "<pauseMenu.profileAttention>{problem}";
            pause.progressPending = "<pauseMenu.progressPending>";
            pause.profileUnreadable = "<pauseMenu.profileUnreadable>";

            var status = w.charmStatus;
            status.ready = "<charmStatus.ready>";
            status.waiting = "<charmStatus.waiting>";
            status.activatedThisStage = "<charmStatus.activatedThisStage>";
            status.activatedThisHand = "<charmStatus.activatedThisHand>";
            status.onInitialRoll = "<charmStatus.onInitialRoll>";
            status.afterReroll = "<charmStatus.afterReroll>";
            status.encounterEnded = "<charmStatus.encounterEnded>";
            status.betweenStages = "<charmStatus.betweenStages>";

            var availability = w.charmAvailability;
            availability.permanentlyUnlocked = "<charmAvailability.permanentlyUnlocked>";
            availability.requiresUnlock = "<charmAvailability.requiresUnlock>";
            availability.encounterExclusive = "<charmAvailability.encounterExclusive>";
            availability.availableFromStart = "<charmAvailability.availableFromStart>";
            availability.availability = "<charmAvailability.availability>{access}|{scope}";
            availability.allEncounters = "<charmAvailability.allEncounters>";
            availability.thisEncounter = "<charmAvailability.thisEncounter>";
            availability.scopeTier = "<charmAvailability.scopeTier>{scope}|{tier}";

            w.ownedCharms.noCharmsTitle = "<ownedCharms.noCharmsTitle>";
            w.ownedCharms.noCharmsBody = "<ownedCharms.noCharmsBody>";
            w.ownedCharms.detailBody = "<ownedCharms.detailBody>{condition}|{effect}|{timing}|{availability}";

            var ending = w.ending;
            ending.returnButton = "<ending.returnButton>";
            ending.abandonedTitle = "<ending.abandonedTitle>";
            ending.lostTitle = "<ending.lostTitle>";
            ending.wonTitle = "<ending.wonTitle>";
            ending.abandonedDetail = "<ending.abandonedDetail>";
            ending.scoreSummary = "<ending.scoreSummary>{score}";
            ending.farewellScoreSummary = "<ending.farewellScoreSummary>{farewell}|{score}";
            ending.noUnlocks = "<ending.noUnlocks>";
            ending.pendingUnlocks = "<ending.pendingUnlocks>{charms}";
            ending.permanentUnlocks = "<ending.permanentUnlocks>{charms}";
            ending.unlockedCharm = "<ending.unlockedCharm>{name}|{scope}";
            ending.unlockedCharmSeparator = " |unlockedCharmSeparator| ";

            w.journey.acceptButton = "<journey.acceptButton>";
            w.journey.continueButton = "<journey.continueButton>";
            w.journey.ruleHeading = Counted("<journey.ruleHeading.one>", "<journey.ruleHeading.many>");

            var investigation = w.investigation;
            investigation.chapterProgress = "<investigation.chapterProgress>{chapter}";
            investigation.chapters = "<investigation.chapters>";
            investigation.defaultTitle = "<investigation.defaultTitle>";
            investigation.readAgain = "<investigation.readAgain>";
            investigation.followLead = "<investigation.followLead>";
            investigation.visitedLead = "<investigation.visitedLead>";
            investigation.mainMenuButton = "<investigation.mainMenuButton>";
            investigation.loadButton = "<investigation.loadButton>";

            var chapters = w.chapters;
            chapters.locked = "<chapters.locked>";
            chapters.current = "<chapters.current>";
            chapters.currentCompleted = "<chapters.currentCompleted>";
            chapters.completed = "<chapters.completed>";
            chapters.available = "<chapters.available>";
            chapters.number = "<chapters.number>{chapter}";

            w.encounterCharms.narration = "<encounterCharms.narration>{narration}";
        }

        private static SpiritCountedWording Counted(string singular, string plural) =>
            new SpiritCountedWording { singular = singular, plural = plural };
    }
}
