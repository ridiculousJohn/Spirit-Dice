using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dice.App
{
    public static class PlayerProfileCodec
    {
        public const int MaximumProfileBytes = 1024 * 1024;
        private static readonly Encoding TextEncoding = new UTF8Encoding(false, true);

        public static byte[] Serialize(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var document = new JObject
            {
                ["ProfileFormatVersion"] = PlayerProfile.CurrentFormatVersion,
                ["DiscoveredFamilyIds"] = new JArray(profile.DiscoveredFamilyIds),
                ["UnlockedModifierIds"] = new JArray(profile.UnlockedModifierIds),
                ["CompletedEncounterIds"] = new JArray(profile.CompletedEncounterIds),
                ["CompletedStoryIds"] = new JArray(profile.CompletedStoryIds),
                ["VisitedEncounterIds"] = new JArray(profile.VisitedEncounterIds)
            };
            byte[] bytes = TextEncoding.GetBytes(document.ToString(Formatting.Indented));
            if (bytes.Length > MaximumProfileBytes) throw new FormatException("The profile exceeds the size limit.");
            return bytes;
        }

        public static PlayerProfile Deserialize(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length > MaximumProfileBytes) throw new FormatException("The profile exceeds the size limit.");
            try
            {
                int offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
                using (var input = new StringReader(TextEncoding.GetString(bytes, offset, bytes.Length - offset)))
                using (var reader = new JsonTextReader(input) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
                {
                    var document = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    });
                    if (reader.Read()) throw new FormatException("The profile contains trailing content.");
                    JToken version = document["ProfileFormatVersion"];
                    if (version == null || version.Type != JTokenType.Integer)
                        throw new FormatException("The profile format version must be an integer.");
                    long formatVersion = version.Value<long>();
                    if (formatVersion != 1 && formatVersion != 3 && formatVersion != PlayerProfile.CurrentFormatVersion)
                        throw new NotSupportedException("The profile format version is unsupported.");
                    foreach (var property in document.Properties())
                        if (property.Name != "ProfileFormatVersion" && property.Name != "UnlockedModifierIds" &&
                            property.Name != "CompletedEncounterIds" && property.Name != "VisitedEncounterIds" &&
                            !(formatVersion >= 3 && property.Name == "DiscoveredFamilyIds") &&
                            !(formatVersion == PlayerProfile.CurrentFormatVersion && property.Name == "CompletedStoryIds"))
                            throw new FormatException("The profile contains an unsupported field: " + property.Name);
                    var profile = new PlayerProfile();
                    if (formatVersion >= 3)
                        ReadIds(document["DiscoveredFamilyIds"], profile.DiscoverFamily);
                    if (formatVersion == PlayerProfile.CurrentFormatVersion)
                        ReadIds(document["CompletedStoryIds"], profile.CompleteStory);
                    ReadIds(document["UnlockedModifierIds"], profile.UnlockModifier);
                    ReadIds(document["CompletedEncounterIds"], profile.CompleteEncounter);
                    // Version one profiles written before journey tracking have no visit field.
                    if (document["VisitedEncounterIds"] != null)
                        ReadIds(document["VisitedEncounterIds"], profile.VisitEncounter);
                    return profile;
                }
            }
            catch (JsonException exception) { throw new FormatException("The profile JSON is invalid.", exception); }
            catch (DecoderFallbackException exception) { throw new FormatException("The profile contains invalid UTF-8.", exception); }
            catch (OverflowException exception) { throw new FormatException("A profile integer is outside its supported range.", exception); }
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
