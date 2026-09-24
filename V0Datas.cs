// =====================================================================
// Datas — DTO Request / Response Models (Чат + Аудио TTS)
// =====================================================================

using Newtonsoft.Json;

namespace S.Programs.Gateways.Services0.Routes.Datas
{
    /// <summary>
    /// Файл-вложение (изображение, документ или схема) в Base64.
    /// </summary>
    public class V0FileAttachment
    {
        [JsonProperty("v0FileName")]
        public string? V0FileName { get; set; }

        [JsonProperty("v0MimeType")]
        public string? V0MimeType { get; set; }

        [JsonProperty("v0Base64")]
        public string V0Base64 { get; set; } = "";
    }

    /// <summary>
    /// Сообщение в диалоге с поддержкой прикрепления одного или пака файлов.
    /// </summary>
    public class V0RouteMessageItem
    {
        [JsonProperty("v0Role")]
        public string V0Role { get; set; } = "user";

        [JsonProperty("v0Content")]
        public string V0Content { get; set; } = "";

        [JsonProperty("v0Files")]
        public List<V0FileAttachment>? V0Files { get; set; }
    }

    public class V0RouteMessageRequest
    {
        [JsonProperty("v0Messages")]
        public List<V0RouteMessageItem> V0Messages { get; set; } = new List<V0RouteMessageItem>();

        [JsonProperty("v0FileBase64")]
        public string? V0FileBase64 { get; set; }

        [JsonProperty("v0Temperature")]
        public double? V0Temperature { get; set; } = 0.7;

        [JsonProperty("v0StreamIs")]
        public bool V0StreamIs { get; set; } = false;

        [JsonProperty("v0ModelsSelect")]
        public List<string>? V0ModelsSelect { get; set; } = null;

        [JsonProperty("v0ModelsSelectOnlyIs")]
        public bool V0ModelsSelectOnlyIs { get; set; } = false;

        public bool HasFiles()
        {
            if (!string.IsNullOrWhiteSpace(V0FileBase64)) return true;
            return V0Messages.Any(m => m.V0Files != null && m.V0Files.Count > 0);
        }
    }

    public class V0RouteMessageResponse
    {
        [JsonProperty("v0Code")]
        public int V0Code { get; set; }

        [JsonProperty("v0Description")]
        public string V0Description { get; set; } = "Успех";

        [JsonProperty("v0Content")]
        public string? V0Content { get; set; }

        [JsonProperty("v0FinishReason")]
        public string? V0FinishReason { get; set; }

        [JsonProperty("v0ModelUsed")]
        public string? V0ModelUsed { get; set; }

        [JsonProperty("v0ProviderUsed")]
        public string? V0ProviderUsed { get; set; }
    }

    public class V0ProviderCallResult
    {
        public bool V0IsSuccess { get; set; }
        public bool V0IsRateLimited { get; set; }
        public string? V0Content { get; set; }
        public string? V0FinishReason { get; set; }
        public string? V0ErrorMessage { get; set; }
        public int V0StatusCode { get; set; }
    }

    // =====================================================================
    // DTO для Аудио TTS (Text-to-Speech)
    // =====================================================================

    public class V0RouteAudioTtsRequest
    {
        /// <summary>
        /// Текст для озвучки
        /// </summary>
        [JsonProperty("v0Text")]
        public string V0Text { get; set; } = "";

        /// <summary>
        /// Модель Fish Audio ("s2.1-pro-free" для бесплатного режима, "s2.1-pro" для платного)
        /// </summary>
        [JsonProperty("v0Model")]
        public string? V0Model { get; set; } = "s2.1-pro-free";

        /// <summary>
        /// ID голоса (reference_id из каталога Fish Audio)
        /// </summary>
        [JsonProperty("v0VoiceId")]
        public string? V0VoiceId { get; set; }

        /// <summary>
        /// Формат вывода: "mp3", "wav", "opus", "pcm"
        /// </summary>
        [JsonProperty("v0Format")]
        public string V0Format { get; set; } = "mp3";

        /// <summary>
        /// Скорость речи (1.0 = нормальная)
        /// </summary>
        [JsonProperty("v0Speed")]
        public double? V0Speed { get; set; } = 1.0;

        /// <summary>
        /// Громкость речи (0.0 = нормальная)
        /// </summary>
        [JsonProperty("v0Volume")]
        public double? V0Volume { get; set; } = 0.0;

        /// <summary>
        /// Задержка / баланс: "normal" или "balanced"
        /// </summary>
        [JsonProperty("v0Latency")]
        public string? V0Latency { get; set; } = "normal";

        /// <summary>
        /// Включить нормализацию текста перед чтением
        /// </summary>
        [JsonProperty("v0NormalizeIs")]
        public bool V0NormalizeIs { get; set; } = true;

        /// <summary>
        /// Провайдер синтеза (по умолчанию "fishaudio")
        /// </summary>
        [JsonProperty("v0ProviderSelect")]
        public string? V0ProviderSelect { get; set; } = "fishaudio";
    }

    public class V0RouteAudioTtsResponse
    {
        [JsonProperty("v0Code")]
        public int V0Code { get; set; }

        [JsonProperty("v0Description")]
        public string V0Description { get; set; } = "Успех";

        /// <summary>
        /// Ссылка формата data:audio/mp3;base64,...
        /// </summary>
        [JsonProperty("v0AudioBase64")]
        public string? V0AudioBase64 { get; set; }

        [JsonProperty("v0Format")]
        public string? V0Format { get; set; }

        [JsonProperty("v0ModelUsed")]
        public string? V0ModelUsed { get; set; }

        [JsonProperty("v0VoiceIdUsed")]
        public string? V0VoiceIdUsed { get; set; }

        [JsonProperty("v0ProviderUsed")]
        public string? V0ProviderUsed { get; set; }

        // Поля, исключенные из сериализации JSON, для передачи бинарных байтов в контроллер
        [JsonIgnore]
        public byte[]? V0RawBytes { get; set; }

        [JsonIgnore]
        public string? V0MimeType { get; set; }
    }

    public class V0AudioProviderCallResult
    {
        public bool V0IsSuccess { get; set; }
        public bool V0IsRateLimited { get; set; }
        public byte[]? V0AudioBytes { get; set; }
        public string? V0MimeType { get; set; }
        public string? V0ErrorMessage { get; set; }
        public int V0StatusCode { get; set; }
    }
}