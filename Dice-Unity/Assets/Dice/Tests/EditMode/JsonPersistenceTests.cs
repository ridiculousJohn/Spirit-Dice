using System;
using System.Linq;
using System.Text;
using Dice.App;
using Dice.Engine;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class JsonPersistenceTests
    {
        [Test]
        public void JsonRoundTripPreservesFreeRerollUsageAndFollowingRandomSequence()
        {
            var stage = new StageDefinition(1000000, 3, 3);
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { stage },
                rules: new[] { new FirstRerollFreeRuleDefinition(TestRules.IdOf("first_reroll_free")) });
            var catalog = new MechanicalCatalog("test.json.free_reroll", TestContent.Hands(), new[] { encounter });
            var original = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 60);
            Assert.That(original.Execute(new RerollDiceAction(1), 0).Accepted, Is.True);
            Assert.That(original.Snapshot.RerollsRemaining, Is.EqualTo(stage.StartingRerolls), "The first reroll is free.");
            var result = EncounterSession.RestoreEncounter(catalog, Decode(SaveJson(original), catalog));
            Assert.That(result.Success, Is.True, result.Detail);
            Assert.That(result.Session.Snapshot.EncounterRules.Single().IsAvailable, Is.False);
            original.Execute(new RerollDiceAction(3), original.Snapshot.Revision);
            result.Session.Execute(new RerollDiceAction(3), result.Session.Snapshot.Revision);
            Assert.That(result.Session.Snapshot.RerollsRemaining, Is.EqualTo(stage.StartingRerolls - 1));
            EncounterPersistenceTests.AssertGraphEqual(original.CaptureSave(), result.Session.CaptureSave());
        }

        [TestCase("RerollsSpentInStage")]
        public void CurrentFormatRequiresExplicitRerollAccounting(string field)
        {
            var json = SaveJson(TestContent.Session());
            json.Remove(field);
            Assert.Throws<FormatException>(() => Decode(json));
        }

        [Test]
        public void SavesAreIndentedUtf8ObjectsWithReadableFieldsAndEnumNames()
        {
            var session = RewardTestContent.Session();
            Assert.That(session.Execute(PlayHandAction.Instance, 0).Accepted, Is.True);
            byte[] bytes = EncounterSaveCodec.Serialize(session.CaptureSave());
            string text = new UTF8Encoding(false, true).GetString(bytes);
            Assert.That(text[0], Is.EqualTo('{'));
            Assert.That(text[text.Length - 1], Is.EqualTo('}'));
            Assert.That(text, Does.Contain("\n  \"SaveFormatVersion\":"));
            var json = JObject.Parse(text);
            Assert.That((int)json["SaveFormatVersion"], Is.EqualTo(EncounterSaveData.CurrentFormatVersion));
            Assert.That((string)json["Phase"], Is.EqualTo("ChoosingModifier"));
            Assert.That((string)json["Outcome"], Is.EqualTo("None"));
            Assert.That((string)json["PendingReward"]["Kind"], Is.EqualTo("Modifier"));
            Assert.That(json["PendingUpgradeCategories"].Children().All(value => value.Type == JTokenType.String), Is.True);
            Assert.That(json["DiceBeforeLastReroll"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(json["CurrentDice"].Type, Is.EqualTo(JTokenType.Array));
            Assert.That(json["$type"], Is.Null);
            AssertRestoresIdentically(session, json);
        }

        [TestCase("SaveFormatVersion")]
        [TestCase("DiceRandomState")]
        [TestCase("RewardRandomState")]
        [TestCase("CurrentDice")]
        [TestCase("PendingReward")]
        public void MissingRequiredFieldsAreRejectedInsteadOfReceivingDefaults(string field)
        {
            var json = SaveJson(EncounterSession.CreateEncounter(TestContent.Catalog(), TestContent.EncounterId, 42));
            Assert.That(json.Remove(field), Is.True);
            Assert.Throws<FormatException>(() => Decode(json));
        }

        [Test]
        public void MissingRequiredNestedOptionFieldsAreRejected()
        {
            var session = RewardTestContent.Session();
            session.Execute(PlayHandAction.Instance, 0);
            var json = SaveJson(session);
            var option = (JObject)json["PendingReward"]["Options"][0];
            Assert.That(option.Remove("OptionId"), Is.True);
            Assert.Throws<FormatException>(() => Decode(json));
        }

        [Test]
        public void DuplicateRootPropertiesAreRejectedEvenWhenTheirValuesAgree()
        {
            var session = EncounterSession.CreateEncounter(TestContent.Catalog(), TestContent.EncounterId, 42);
            string text = Encoding.UTF8.GetString(EncounterSaveCodec.Serialize(session.CaptureSave()));
            text = text.Insert(1, "\"SaveFormatVersion\": 1,");
            Assert.Throws<FormatException>(() => EncounterSaveCodec.Deserialize(Encoding.UTF8.GetBytes(text), TestContent.Catalog()));
        }

        [Test]
        public void DuplicateNestedPropertiesAreRejected()
        {
            var session = RewardTestContent.Session();
            session.Execute(PlayHandAction.Instance, 0);
            string text = Encoding.UTF8.GetString(EncounterSaveCodec.Serialize(session.CaptureSave()));
            Assert.That(text, Does.Contain("\"OptionId\":"));
            text = text.Replace("\"OptionId\":", "\"OptionId\": 1, \"OptionId\":");
            Assert.Throws<FormatException>(() => EncounterSaveCodec.Deserialize(Encoding.UTF8.GetBytes(text), TestContent.Catalog()));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DifferentlyCasedKnownPropertiesCannotOverrideCanonicalFields(bool nested)
        {
            var session = RewardTestContent.Session();
            session.Execute(PlayHandAction.Instance, 0);
            var json = SaveJson(session);
            if (nested)
                ((JObject)json["PendingReward"]["Options"][0])["optionid"] = long.MaxValue;
            else
                json["dicerandomstate"] = 0;
            Assert.Throws<FormatException>(() => Decode(json));
        }

        [Test]
        public void UnknownFieldsAndReorderedPropertiesPreserveSavedState()
        {
            var session = RewardTestContent.Session();
            session.Execute(PlayHandAction.Instance, 0);
            var original = SaveJson(session);
            var reordered = new JObject(original.Properties().Reverse()
                .Select(property => new JProperty(property.Name, property.Value.DeepClone())));
            reordered["FuturePresentationData"] = new JObject { ["label"] = "unrecognized", ["count"] = 7 };
            ((JObject)reordered["PendingReward"]["Options"][0])["FutureOptionData"] = true;
            AssertRestoresIdentically(session, reordered);
        }

        [Test]
        public void TypeMetadataIsInertAtRootAndInsideOptions()
        {
            var session = RewardTestContent.Session();
            session.Execute(PlayHandAction.Instance, 0);
            var json = SaveJson(session);
            json.AddFirst(new JProperty("$type", "System.IO.FileInfo, mscorlib"));
            ((JObject)json["PendingReward"]["Options"][0]).AddFirst(
                new JProperty("$type", "System.Version, mscorlib"));
            AssertRestoresIdentically(session, json);
        }

        [Test]
        public void SignedAndUnsignedExtremesRemainExactJsonIntegers()
        {
            var save = EncounterSession.CreateEncounter(TestContent.Catalog(), TestContent.EncounterId, 42).CaptureSave();
            save.DiceRandomState = ulong.MaxValue;
            save.RewardRandomState = ulong.MaxValue;
            save.TotalScore = long.MinValue;
            save.Revision = long.MaxValue;
            byte[] bytes = EncounterSaveCodec.Serialize(save);
            var json = JObject.Parse(Encoding.UTF8.GetString(bytes));
            Assert.That(json["DiceRandomState"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That(json["DiceRandomState"].ToString(), Is.EqualTo("18446744073709551615"));
            Assert.That(json["TotalScore"].ToString(), Is.EqualTo("-9223372036854775808"));
            var decoded = EncounterSaveCodec.Deserialize(bytes, TestContent.Catalog());
            EncounterPersistenceTests.AssertGraphEqual(save, decoded);
        }

        [Test]
        public void MalformedUtf8CannotBeReplacedWithAReplacementCharacter()
        {
            var json = SaveJson(EncounterSession.CreateEncounter(TestContent.Catalog(), TestContent.EncounterId, 42));
            json["CatalogVersion"] = "UTF8_SENTINEL";
            string text = json.ToString();
            int offset = Encoding.UTF8.GetByteCount(text.Substring(0, text.IndexOf("UTF8_SENTINEL", StringComparison.Ordinal)));
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            bytes[offset] = 0xc3;
            bytes[offset + 1] = 0x28;
            Assert.Throws<FormatException>(() => EncounterSaveCodec.Deserialize(bytes, TestContent.Catalog()));
        }

        [TestCase("null")]
        [TestCase("[]")]
        [TestCase("1")]
        [TestCase("\"save\"")]
        public void OnlyAnObjectCanRepresentASave(string text)
        {
            Assert.Throws<FormatException>(() => EncounterSaveCodec.Deserialize(Encoding.UTF8.GetBytes(text), TestContent.Catalog()));
        }

        private static JObject SaveJson(EncounterSession session)
        {
            return JObject.Parse(Encoding.UTF8.GetString(EncounterSaveCodec.Serialize(session.CaptureSave())));
        }

        private static EncounterSaveData Decode(JObject json, MechanicalCatalog catalog = null)
        {
            return EncounterSaveCodec.Deserialize(Encoding.UTF8.GetBytes(json.ToString()), catalog ?? TestContent.Catalog());
        }

        private static void AssertRestoresIdentically(EncounterSession session, JObject json)
        {
            var decoded = Decode(json);
            EncounterPersistenceTests.AssertGraphEqual(session.CaptureSave(), decoded);
            var restored = EncounterSession.RestoreEncounter(RewardTestContent.Catalog(), decoded);
            Assert.That(restored.Success, Is.True, restored.Detail);
            EncounterSessionTests.AssertSnapshotsEqual(session.Snapshot, restored.Session.Snapshot);
            Assert.That(restored.Session.InitialResolution.Events, Is.Empty);
        }
    }
}
