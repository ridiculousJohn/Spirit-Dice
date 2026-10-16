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
        private static readonly Encoding TextEncoding = new UTF8Encoding(false, true);
        private static readonly JsonSerializerSettings Settings = CreateSettings();

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

        public static EncounterSaveData Deserialize(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
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
                    ValidateFieldNames(document, typeof(EncounterSaveData), serializer.ContractResolver);
                    var save = document.ToObject<EncounterSaveData>(serializer);
                    if (save == null) throw new FormatException("The save payload is missing.");
                    return save;
                }
            }
            catch (JsonException exception) { throw new FormatException("The save JSON is invalid.", exception); }
            catch (DecoderFallbackException exception) { throw new FormatException("The save contains invalid UTF-8.", exception); }
            catch (OverflowException exception) { throw new FormatException("A save integer is outside its supported range.", exception); }
        }

        private static JsonSerializerSettings CreateSettings()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new SaveContractResolver(),
                Formatting = Formatting.Indented,
                Culture = CultureInfo.InvariantCulture,
                DateParseHandling = DateParseHandling.None,
                NullValueHandling = NullValueHandling.Include,
                DefaultValueHandling = DefaultValueHandling.Include,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                MaxDepth = MaximumDepth
            };
            settings.Converters.Add(new StringEnumConverter());
            settings.Converters.Add(new StrictPrimitiveConverter());
            return settings;
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
