using System.Text.Json;

namespace SimpleV2Ray
{
    internal class V2RayConfig
    {
        public class ApiConfig
        {
            public string? Tag { get; set; }
        }

        public ApiConfig? Api { get; set; }

        public class RoutingConfig
        {
            public class RuleConfig
            {
                public List<string>? InboundTag { get; set; }
                public string? OutboundTag { get; set; }
            }

            public List<RuleConfig>? Rules { get; set; }
        }

        public RoutingConfig? Routing { get; set; }

        public class InboundConfig
        {
            public string? Listen { get; set; }
            public int Port { get; set; }
            public string? Protocol { get; set; }
            public string? Tag { get; set; }
        }

        public List<InboundConfig>? Inbounds { get; set; }

        public class OutboundConfig
        {
            public string? Protocol { get; set; }
            public string? SendThrough { get; set; }
            public string? Tag { get; set; }
        }

        public List<OutboundConfig>? Outbounds { get; set; }

        public static V2RayConfig? Load(string path)
        {
            string v2RayConfigJson = File.ReadAllText(path);
            JsonSerializerOptions options = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            V2RayConfig? v2RayConfig = JsonSerializer.Deserialize<V2RayConfig>(v2RayConfigJson, options);
            return v2RayConfig;
        }
    }
}
