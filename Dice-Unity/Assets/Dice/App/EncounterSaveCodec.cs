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
        private static readonly JsonSerializerSettings Settings = CreateSettings(MissingMemberHandling.Ignore);
        // A rule state belongs to its rule's save class, so a field that class does not define is rejected.
        private static readonly JsonSerializerSettings RuleStateSettings = CreateSettings(MissingMemberHandling.Error);
        // To change the format, raise EncounterSaveData.CurrentFormatVersion and append a step that upgrades the previous format.
        private static readonly SaveFormatUpgrades FormatUpgrades = new SaveFormatUpgrades(
            nameof(EncounterSaveData.SaveFormatVersion), EncounterSaveData.CurrentFormatVersion);

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
            if (SaveFileText.Encoding.GetByteCount(text) > MaximumSaveBytes)
                throw new ArgumentException("The save exceeds the size limit.", nameof(save));
            return SaveFileText.Encoding.GetBytes(text);
        }

        /** The catalog resolves each rule state's type from the rule that owns it. */
        public static EncounterSaveData Deserialize(byte[] bytes, MechanicalCatalog catalog)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (bytes.Length > MaximumSaveBytes) throw new FormatException("The save exceeds the size limit.");
            try
            {
                string text = SaveFileText.Decode(bytes);
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
                    var check = FormatUpgrades.Upgrade(document);
                    if (check == SaveFormatCheck.VersionNotInteger)
                        throw new FormatException("The save format version must be an integer.");
                    if (check == SaveFormatCheck.VersionUnsupported)
                        throw new NotSupportedException("The save format version is unsupported.");
                    var serializer = JsonSerializer.Create(Settings);
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
            settings.Converters.Add(new RewardKindConverter());
            settings.Converters.Add(ContentIds.Modifier);
            settings.Converters.Add(ContentIds.Family);
            settings.Converters.Add(ContentIds.Encounter);
            settings.Converters.Add(ContentIds.Piece);
            settings.Converters.Add(ContentIds.Rule);
            return settings;
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
                if (encounter != null && ruleId != null)
                {
                    EncounterRuleDefinition.Id id = ContentIds.Rule.Read(ruleId);
                    foreach (var rule in encounter.Rules)
                        if (rule.Identity == id) type = rule.SaveDataType;
                }
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
            var token = document[nameof(EncounterSaveData.EncounterId)];
            if (token == null) return null;
            EncounterDefinition.Id id = ContentIds.Encounter.Read(token);
            foreach (var encounter in catalog.Encounters)
                if (encounter.Identity == id) return encounter;
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

        /** Writes a reward kind as its id and reads it back as the kind declaring that id, rejecting any other value. */
        private sealed class RewardKindConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => typeof(RewardKind).IsAssignableFrom(objectType);

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                => writer.WriteValue(((RewardKind)value).Id);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType != JsonToken.String)
                    throw new JsonSerializationException("A saved reward kind requires a string.");
                var id = (string)reader.Value;
                return RewardKind.TryFind(id, out var kind) ? kind
                    : throw new JsonSerializationException("A saved reward kind is unknown: " + id);
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
