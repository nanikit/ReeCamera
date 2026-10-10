using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ReeCamera {
    [JsonConverter(typeof(HandleVisibilityConverter))]
    public enum HandleVisibility {
        Hidden,
        HmdOnly
    }

    internal sealed class HandleVisibilityConverter : StringEnumConverter {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            // Preserve existing presets while retiring the desktop visibility option.
            if (reader.TokenType == JsonToken.String && (string)reader.Value == "HmdAndDesktop") return HandleVisibility.HmdOnly;
            return base.ReadJson(reader, objectType, existingValue, serializer);
        }
    }
}
