using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text;
using Dice.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Dice.App
{
    public static class EncounterSaveCodec
    {
        public const int MaximumSaveBytes = 16 * 1024 * 1024;
        private const int MaximumDepth = 64;
        private const string LegacyDisturbedMask = "DisturbedMask";
        private const string LegacyLatestDisturbedMask = "LatestDisturbedMask";
        private static readonly Encoding TextEncoding = new UTF8Encoding(false, true);
        private static readonly JsonSerializerSettings Settings = CreateSettings(MissingMemberHandling.Ignore);
        // A rule state belongs to its rule's save class, so a field that class does not define is rejected.
        private static readonly JsonSerializerSettings RuleStateSettings = CreateSettings(MissingMemberHandling.Error);

        public static byte[] Serialize(EncounterSaveData save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.SaveFormatVersion != EncounterSaveData.CurrentFormatVersion)
                throw new NotSupportedException("The save format version is unsupported.");
            string text;
            using (var output = new StringWriter(CultureInfo.InvariantCulture))
            using (var writer = new JsonTextWriter(output))
            {
                output.NewLine = "\n";
                JsonSerializer.Create(Settings).Serialize(writer, save);
                writer.Flush();
                text = output.ToString();
            }
            if (TextEncoding.GetByteCount(text) > MaximumSaveBytes)
                throw new ArgumentException("The save exceeds the size limit.", nameof(save));
            return TextEncoding.GetBytes(text);
        }

        /** The catalog resolves each rule state's type from the rule that owns it. */
        public static EncounterSaveData Deserialize(byte[] bytes, MechanicalCatalog catalog)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (bytes.Length > MaximumSaveBytes) throw new FormatException("The save exceeds the size limit.");
            try
            {
                int offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
                string text = TextEncoding.GetString(bytes, offset, bytes.Length - offset);
                if (text.IndexOf('\0') >= 0) throw new FormatException("The save contains an unescaped null character.");
                using (var input = new StringReader(text))
                using (var reader = new JsonTextReader(input) { MaxDepth = MaximumDepth, DateParseHandling = DateParseHandling.None })
                {
                    var document = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        LineInfoHandling = LineInfoHandling.Ignore
                    });
                    if (reader.Read()) throw new FormatException("The save contains trailing content.");
                    var version = document[nameof(EncounterSaveData.SaveFormatVersion)];
                    if (version == null || version.Type != JTokenType.Integer)
                        throw new FormatException("The save format version must be an integer.");
                    var serializer = JsonSerializer.Create(Settings);
                    int formatVersion = version.ToObject<int>(serializer);
                    if (formatVersion < 1 || formatVersion > EncounterSaveData.CurrentFormatVersion)
                        throw new NotSupportedException("The save format version is unsupported.");
                    if (formatVersion == 1)
                    {
                        // Legacy encounters spent one resource per reroll; retain the version for rules validation.
                        if (document[nameof(EncounterSaveData.FreeRerollsUsedInStage)] == null)
                            document.Add(nameof(EncounterSaveData.FreeRerollsUsedInStage), 0);
                        if (document[nameof(EncounterSaveData.TotalFreeRerollsUsed)] == null)
                            document.Add(nameof(EncounterSaveData.TotalFreeRerollsUsed), 0L);
                        if (document[nameof(EncounterSaveData.RerollsSpentInStage)] == null)
                            document.Add(nameof(EncounterSaveData.RerollsSpentInStage), 0);
                    }
                    if (formatVersion < 3)
                    {
                        if (document[nameof(EncounterSaveData.UnlockedModifierIds)] == null)
                            document.Add(nameof(EncounterSaveData.UnlockedModifierIds), new JArray());
                        if (document[nameof(EncounterSaveData.NewlyUnlockedModifierIds)] == null)
                            document.Add(nameof(EncounterSaveData.NewlyUnlockedModifierIds), new JArray());
                        if (document[nameof(EncounterSaveData.PendingModifierOfferUsesLegacyEligibility)] == null)
                            document.Add(nameof(EncounterSaveData.PendingModifierOfferUsesLegacyEligibility), false);
                        if (document[nameof(EncounterSaveData.PendingModifierOfferUnlockedIds)] == null)
                            document.Add(nameof(EncounterSaveData.PendingModifierOfferUnlockedIds), JValue.CreateNull());
                    }
                    if (formatVersion < 4 && document[nameof(EncounterSaveData.PendingModifierOfferUsesEncounterAdditions)] == null)
                        document.Add(nameof(EncounterSaveData.PendingModifierOfferUsesEncounterAdditions), false);
                    if (formatVersion < 5)
                    {
                        if (document[nameof(EncounterSaveData.DiscoveredFamilyIds)] == null)
                            document.Add(nameof(EncounterSaveData.DiscoveredFamilyIds), JValue.CreateNull());
                        if (document[nameof(EncounterSaveData.PendingUnlockOfferDiscoveredFamilyIds)] == null)
                            document.Add(nameof(EncounterSaveData.PendingUnlockOfferDiscoveredFamilyIds), JValue.CreateNull());
                    }
                    if (formatVersion < 6)
                    {
                        // Earlier formats had no table pieces, interference or rule stream; restore supplies the stream.
                        if (document[LegacyDisturbedMask] == null) document.Add(LegacyDisturbedMask, 0);
                        if (document[LegacyLatestDisturbedMask] == null) document.Add(LegacyLatestDisturbedMask, 0);
                        if (document[nameof(EncounterSaveData.RuleRandomState)] == null)
                            document.Add(nameof(EncounterSaveData.RuleRandomState), 0UL);
                        if (document[nameof(EncounterSaveData.Pieces)] == null)
                            document.Add(nameof(EncounterSaveData.Pieces), JValue.CreateNull());
                    }
                    if (formatVersion < 7) MigrateDisturbedMasks(document, catalog, serializer);
                    var ruleStates = ReadRuleStates(document, catalog, serializer);
                    ValidateFieldNames(document, typeof(EncounterSaveData), serializer.ContractResolver);
                    var save = document.ToObject<EncounterSaveData>(serializer);
                    if (save == null) throw new FormatException("The save payload is missing.");
                    for (int i = 0; i < ruleStates.Count; ++i)
                        if (save.RuleStates[i] != null) save.RuleStates[i].State = ruleStates[i];
                    return save;
                }
            }
            catch (JsonException exception) { throw new FormatException("The save JSON is invalid.", exception); }
            catch (DecoderFallbackException exception) { throw new FormatException("The save contains invalid UTF-8.", exception); }
            catch (OverflowException exception) { throw new FormatException("A save integer is outside its supported range.", exception); }
        }

        private static JsonSerializerSettings CreateSettings(MissingMemberHandling missingMemberHandling)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new SaveContractResolver(),
                Formatting = Formatting.Indented,
                Culture = CultureInfo.InvariantCulture,
                DateParseHandling = DateParseHandling.None,
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
                MissingMemberHandling = missingMemberHandling,
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                MaxDepth = MaximumDepth
            };
            settings.Converters.Add(new StringEnumConverter());
            settings.Converters.Add(new StrictPrimitiveConverter());
            return settings;
        }

        /** Format 6 kept the haunting's disturbed dice on the shared block; they move into its rule's state. */
        private static void MigrateDisturbedMasks(JObject document, MechanicalCatalog catalog, JsonSerializer serializer)
        {
            int disturbed = TakeLegacyMask(document, LegacyDisturbedMask, serializer);
            int latest = TakeLegacyMask(document, LegacyLatestDisturbedMask, serializer);
            if (document[nameof(EncounterSaveData.RuleStates)] == null)
                document.Add(nameof(EncounterSaveData.RuleStates), new JArray());
            if (disturbed == 0 && latest == 0) return;
            // Only the encounter's die-interference rule could have recorded disturbed dice.
            string ruleId = null;
            var encounter = FindEncounter(document, catalog);
            if (encounter != null)
                foreach (var rule in encounter.Rules)
                    if (rule.SaveDataType == typeof(DieInterferenceSaveData)) ruleId = rule.Id;
            if (ruleId == null) throw new FormatException("Disturbed dice require a die-interference rule.");
            if (!(document[nameof(EncounterSaveData.RuleStates)] is JArray entries))
                throw new FormatException("Rule states must be an array.");
            entries.Add(new JObject
            {
                { nameof(EncounterSaveRuleData.RuleId), ruleId },
                { nameof(EncounterSaveRuleData.State), JObject.FromObject(new DieInterferenceSaveData
                    { DisturbedMask = disturbed, LatestDisturbedMask = latest }, serializer) }
            });
        }

        private static int TakeLegacyMask(JObject document, string name, JsonSerializer serializer)
        {
            var token = document[name] ?? throw new JsonSerializationException("A required save field is missing: " + name);
            int value = token.ToObject<int>(serializer);
            document.Remove(name);
            return value;
        }

        /**
         * Validates each rule state against its owning rule's save type and deserializes it with that type.
         * States of rules the catalog does not define stay as parsed JSON, which restore rejects.
         */
        private static List<object> ReadRuleStates(JObject document, MechanicalCatalog catalog, JsonSerializer serializer)
        {
            var values = new List<object>();
            if (!(document[nameof(EncounterSaveData.RuleStates)] is JArray entries)) return values;
            var encounter = FindEncounter(document, catalog);
            foreach (var entry in entries)
            {
                var ruleId = (entry as JObject)?[nameof(EncounterSaveRuleData.RuleId)];
                var state = (entry as JObject)?[nameof(EncounterSaveRuleData.State)];
                Type type = null;
                if (encounter != null && ruleId != null && ruleId.Type == JTokenType.String)
                    foreach (var rule in encounter.Rules)
                        if (string.Equals(rule.Id, (string)ruleId, StringComparison.Ordinal)) type = rule.SaveDataType;
                if (type == null || state == null || state.Type == JTokenType.Null)
                {
                    values.Add(state == null || state.Type == JTokenType.Null ? null : state);
                    continue;
                }
                ValidateFieldNames(state, type, serializer.ContractResolver);
                values.Add(state.ToObject(type, JsonSerializer.Create(RuleStateSettings)));
            }
            return values;
        }

        private static EncounterDefinition FindEncounter(JObject document, MechanicalCatalog catalog)
        {
            var id = document[nameof(EncounterSaveData.EncounterId)];
            if (id == null || id.Type != JTokenType.String) return null;
            foreach (var encounter in catalog.Encounters)
                if (string.Equals(encounter.Id, (string)id, StringComparison.Ordinal)) return encounter;
            return null;
        }

        private static void ValidateFieldNames(JToken token, Type type, IContractResolver resolver)
        {
            if (token.Type == JTokenType.Null) return;
            if (type.IsArray && token is JArray array)
            {
                foreach (var item in array) ValidateFieldNames(item, type.GetElementType(), resolver);
                return;
            }
            if (!(token is JObject value) || !(resolver.ResolveContract(type) is JsonObjectContract contract)) return;
            var names = new HashSet<string>(StringComparer.Ordinal);
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in contract.Properties)
            {
                if (property.Ignored) continue;
                names.Add(property.PropertyName);
                aliases.Add(property.PropertyName);
                var field = value[property.PropertyName];
                if (field == null) throw new JsonSerializationException("A required save field is missing: " + property.PropertyName);
                ValidateFieldNames(field, property.PropertyType, resolver);
            }
            foreach (var property in value.Properties())
                if (aliases.Contains(property.Name) && !names.Contains(property.Name))
                    throw new JsonSerializationException("A save field uses an ambiguous casing alias: " + property.Name);
        }

        private sealed class SaveContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);
                property.Required = property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) == null
                    ? Required.Always : Required.AllowNull;
                return property;
            }
        }

        private sealed class StrictPrimitiveConverter : JsonConverter
        {
            public override bool CanWrite => false;

            public override bool CanConvert(Type objectType)
            {
                Type type = Nullable.GetUnderlyingType(objectType) ?? objectType;
                return type == typeof(int) || type == typeof(long) || type == typeof(ulong) || type == typeof(bool);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                Type nullable = Nullable.GetUnderlyingType(objectType);
                if (reader.TokenType == JsonToken.Null && nullable != null) return null;
                Type type = nullable ?? objectType;
                if (type == typeof(bool))
                {
                    if (reader.TokenType != JsonToken.Boolean)
                        throw new JsonSerializationException("A saved boolean requires a JSON boolean token.");
                    return reader.Value;
                }
                if (reader.TokenType != JsonToken.Integer)
                    throw new JsonSerializationException("A saved integer requires a JSON integer token.");
                if (reader.Value is BigInteger integer)
                {
                    if (type == typeof(int)) return (int)integer;
                    if (type == typeof(long)) return (long)integer;
                    return (ulong)integer;
                }
                if (type == typeof(int)) return Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                if (type == typeof(long)) return Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture);
                return Convert.ToUInt64(reader.Value, CultureInfo.InvariantCulture);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                => throw new NotSupportedException("Primitive writing uses the standard JSON serializer.");
        }
    }
}
