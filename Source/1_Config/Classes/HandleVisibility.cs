using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ReeCamera {
    [JsonConverter(typeof(StringEnumConverter))]
    public enum HandleVisibility {
        Hidden,
        HmdOnly,
        HmdAndDesktop
    }
}
