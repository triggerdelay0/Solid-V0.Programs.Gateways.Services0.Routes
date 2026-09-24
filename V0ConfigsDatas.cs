// =====================================================================
// Datas — Configs (V0Config.json)
// =====================================================================

using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace S.Programs.Gateways.Services0.Routes.Datas
{
    public class V0ConfigServer
    {
        [JsonProperty("v0Host")]
        public string V0Host { get; set; } = "0.0.0.0";

        [JsonProperty("v0Port")]
        public int V0Port { get; set; } = 8035;
    }

    public class V0ConfigRouter
    {
        [JsonProperty("v0PathFileModels")]
        public string V0PathFileModels { get; set; } = "V0Models.json";

        [JsonProperty("v0PathFileTokens")]
        public string V0PathFileTokens { get; set; } = "V0Tokens.json";

        [JsonProperty("v0TimeoutSeconds")]
        public int V0TimeoutSeconds { get; set; } = 60;

        [JsonProperty("v0CooldownSecondsOnRateLimit")]
        public int V0CooldownSecondsOnRateLimit { get; set; } = 60;

        /// <summary>
        /// Словарь URL и эндпоинтов провайдеров (например, "lmstudio": "http://localhost:1234/v1")
        /// </summary>
        [JsonProperty("v0ProviderUrls")]
        public Dictionary<string, string> V0ProviderUrls { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public class V0Config
    {
        [JsonProperty("v0Server")]
        public V0ConfigServer V0Server { get; set; } = new V0ConfigServer();

        [JsonProperty("v0Router")]
        public V0ConfigRouter V0Router { get; set; } = new V0ConfigRouter();

        [JsonProperty("v0ProviderUrls")]
        public Dictionary<string, string>? V0ProviderUrls { get; set; }

        [JsonProperty("v0IsVerbose")]
        public bool V0IsVerbose { get; set; } = true;

        /// <summary>
        /// Возвращает настроенный URL провайдера (сначала из v0Router.v0ProviderUrls, затем из корневого v0ProviderUrls).
        /// </summary>
        public string? V0GetProviderUrl(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider)) return null;

            if (V0Router?.V0ProviderUrls != null && V0Router.V0ProviderUrls.TryGetValue(provider, out var routerUrl) && !string.IsNullOrWhiteSpace(routerUrl))
            {
                return routerUrl.Trim();
            }

            if (V0ProviderUrls != null && V0ProviderUrls.TryGetValue(provider, out var rootUrl) && !string.IsNullOrWhiteSpace(rootUrl))
            {
                return rootUrl.Trim();
            }

            return null;
        }
    }

    static public partial class V0Static
    {
        public const string v0SystemsPathsFileJsonConfig = "V0Config.json";

        static public V0Config v0Config = new V0Config();

        private static readonly Regex JsonCommentsRegex = new Regex(
            @"(\""(?:\\.|[^\""\\])*\"")|//.*?$|/\*.*?\*/",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

        private static readonly Regex TrailingCommasRegex = new Regex(
            @",(?=\s*[\}\]])",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Удаляет комментарии (// и /* */) и завершающие запятые перед закрывающими скобками,
        /// сохраняя строки и URL без изменений.
        /// </summary>
        static public string V0JsonClean(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return "";
            }

            var stripped = JsonCommentsRegex.Replace(rawJson, match =>
                match.Groups[1].Success ? match.Groups[1].Value : "");

            return TrailingCommasRegex.Replace(stripped, "");
        }

        /// <summary>
        /// Десериализует JSON с поддержкой комментариев и висячих запятых.
        /// </summary>
        static public T? V0JsonDeserialize<T>(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return default;
            }

            var cleaned = V0JsonClean(text);
            return JsonConvert.DeserializeObject<T>(cleaned);
        }

        static public V0Config V0ConfigLoadAndGet(string? systemsPathsFileJson = null)
        {
            systemsPathsFileJson ??= v0SystemsPathsFileJsonConfig;

            if (!File.Exists(systemsPathsFileJson))
            {
                v0Config = new V0Config();
                V0ConfigSave(systemsPathsFileJson);
                return v0Config;
            }

            var text = File.ReadAllText(systemsPathsFileJson);
            v0Config = V0JsonDeserialize<V0Config>(text) ?? new V0Config();
            return v0Config;
        }

        static public void V0ConfigSave(string? systemsPathsFileJson = null)
        {
            systemsPathsFileJson ??= v0SystemsPathsFileJsonConfig;

            var directory = Path.GetDirectoryName(Path.GetFullPath(systemsPathsFileJson));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(systemsPathsFileJson, JsonConvert.SerializeObject(v0Config, Formatting.Indented));
        }
    }
}