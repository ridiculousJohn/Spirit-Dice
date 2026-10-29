using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Dice.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dice.App
{
    public static class PlayerProfileCodec
    {
        public const int MaximumProfileBytes = 1024 * 1024;
        private const int MaximumDepth = 8;
        private const string FormatVersionField = "ProfileFormatVersion";
        private const string DiscoveredFamilyIdsField = "DiscoveredFamilyIds";
        private const string UnlockedModifierIdsField = "UnlockedModifierIds";
        private const string CompletedEncounterIdsField = "CompletedEncounterIds";
        private const string CompletedStoryIdsField = "CompletedStoryIds";
        private const string VisitedEncounterIdsField = "VisitedEncounterIds";
        // To change the format, raise PlayerProfile.CurrentFormatVersion and append a step that upgrades the previous format.
        private static readonly SaveFormatUpgrades FormatUpgrades = new SaveFormatUpgrades(
            FormatVersionField, PlayerProfile.CurrentFormatVersion);

        public static byte[] Serialize(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var document = new JObject
            {
                [FormatVersionField] = PlayerProfile.CurrentFormatVersion,
                [DiscoveredFamilyIdsField] = IdArray(profile.DiscoveredFamilyIds, ContentIds.Family),
                [UnlockedModifierIdsField] = new JArray(profile.UnlockedModifierIds),
                [CompletedEncounterIdsField] = IdArray(profile.CompletedEncounterIds, ContentIds.Encounter),
                [CompletedStoryIdsField] = new JArray(profile.CompletedStoryIds),
                [VisitedEncounterIdsField] = IdArray(profile.VisitedEncounterIds, ContentIds.Encounter)
            };
            byte[] bytes = SaveFileText.Encoding.GetBytes(document.ToString(Formatting.Indented));
            if (bytes.Length > MaximumProfileBytes) throw new FormatException("The profile exceeds the size limit.");
            return bytes;
        }

        public static PlayerProfile Deserialize(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length > MaximumProfileBytes) throw new FormatException("The profile exceeds the size limit.");
            try
            {
                using (var input = new StringReader(SaveFileText.Decode(bytes)))
                using (var reader = new JsonTextReader(input) { MaxDepth = MaximumDepth, DateParseHandling = DateParseHandling.None })
                {
                    var document = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    });
                    if (reader.Read()) throw new FormatException("The profile contains trailing content.");
                    var check = FormatUpgrades.Upgrade(document);
                    if (check == SaveFormatCheck.VersionNotInteger)
                        throw new FormatException("The profile format version must be an integer.");
                    if (check == SaveFormatCheck.VersionUnsupported)
                        throw new NotSupportedException("The profile format version is unsupported.");
                    foreach (var property in document.Properties())
                        if (property.Name != FormatVersionField && property.Name != DiscoveredFamilyIdsField &&
                            property.Name != UnlockedModifierIdsField && property.Name != CompletedEncounterIdsField &&
                            property.Name != CompletedStoryIdsField && property.Name != VisitedEncounterIdsField)
                            throw new FormatException("The profile contains an unsupported field: " + property.Name);
                    var profile = new PlayerProfile();
                    ReadIds(document[DiscoveredFamilyIdsField], ContentIds.Family, profile.DiscoverFamily);
                    ReadIds(document[CompletedStoryIdsField], profile.CompleteStory);
                    ReadIds(document[UnlockedModifierIdsField], profile.UnlockModifier);
                    ReadIds(document[CompletedEncounterIdsField], ContentIds.Encounter, profile.CompleteEncounter);
                    ReadIds(document[VisitedEncounterIdsField], ContentIds.Encounter, profile.VisitEncounter);
                    return profile;
                }
            }
            catch (JsonException exception) { throw new FormatException("The profile JSON is invalid.", exception); }
            catch (DecoderFallbackException exception) { throw new FormatException("The profile contains invalid UTF-8.", exception); }
            catch (OverflowException exception) { throw new FormatException("A profile integer is outside its supported range.", exception); }
        }

        private static JArray IdArray<TId>(IEnumerable<TId> ids, ContentIdConverter<TId> converter) where TId : struct, IContentId
        {
            var array = new JArray();
            foreach (TId id in ids) array.Add(converter.Write(id));
            return array;
        }

        private static void ReadIds<TId>(JToken token, ContentIdConverter<TId> converter, Func<TId, bool> add)
            where TId : struct, IContentId
        {
            if (!(token is JArray array)) throw new FormatException("Profile IDs must be arrays.");
            foreach (JToken value in array)
                if (!add(converter.Read(value))) throw new FormatException("Profile IDs must be unique.");
        }

        private static void ReadIds(JToken token, Func<string, bool> add)
        {
            if (!(token is JArray array)) throw new FormatException("Profile IDs must be arrays.");
            foreach (JToken value in array)
            {
                if (value.Type != JTokenType.String) throw new FormatException("Profile IDs must be strings.");
                if (!add(value.Value<string>())) throw new FormatException("Profile IDs must be unique.");
            }
        }
    }
}
