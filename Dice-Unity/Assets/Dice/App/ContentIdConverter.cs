using System;
using Dice.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dice.App
{
    /**
     * Writes a typed content id as its GUID's 32 hexadecimal digits, the text its asset's .meta file holds, and reads it
     * back, rejecting anything but a nonempty GUID. The run save and the profile write their typed ids through it.
     */
    internal sealed class ContentIdConverter<TId> : JsonConverter<TId> where TId : struct, IContentId
    {
        private readonly string kind;
        private readonly Func<Guid, TId> create;

        internal ContentIdConverter(string kind, Func<Guid, TId> create)
        {
            this.kind = kind;
            this.create = create;
        }

        internal JToken Write(TId id) => id.Value.ToString("N");

        internal TId Read(JToken token)
            => token != null && token.Type == JTokenType.String ? Read((string)token) : throw Invalid();

        public override void WriteJson(JsonWriter writer, TId value, JsonSerializer serializer) => writer.WriteValue(value.Value.ToString("N"));

        public override TId ReadJson(JsonReader reader, Type objectType, TId existingValue, bool hasExistingValue, JsonSerializer serializer)
            => reader.TokenType == JsonToken.String ? Read((string)reader.Value) : throw Invalid();

        private TId Read(string text)
            => Guid.TryParseExact(text, "N", out Guid value) && value != Guid.Empty ? create(value) : throw Invalid();

        private JsonSerializationException Invalid() => new JsonSerializationException("A saved " + kind + " id requires a GUID string.");
    }

    /** The converter of each typed content id the run save and the profile store. */
    internal static class ContentIds
    {
        internal static readonly ContentIdConverter<ModifierFamilyDefinition.Id> Family =
            new ContentIdConverter<ModifierFamilyDefinition.Id>("charm family", value => new ModifierFamilyDefinition.Id(value));
        internal static readonly ContentIdConverter<EncounterDefinition.Id> Encounter =
            new ContentIdConverter<EncounterDefinition.Id>("encounter", value => new EncounterDefinition.Id(value));
    }
}
