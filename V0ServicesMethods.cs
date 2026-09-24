// =====================================================================
// Methods — Service Router (Model, Token & Audio TTS Failover Logic)
// =====================================================================

using S.Programs.Gateways.Services0.Routes.Datas;
using System.Collections.Concurrent;

namespace S.Programs.Gateways.Services0.Routes.Methods
{
    public sealed class V0ServiceRouter
    {
        private readonly V0Config _config;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _tokenRateLimitCooldowns = new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.Ordinal);

        public V0ServiceRouter(V0Config config)
        {
            _config = config;
        }

        public List<string> V0ModelsGet()
        {
            var path = _config.V0Router.V0PathFileModels;
            if (!File.Exists(path)) return new List<string>();

            try
            {
                var text = File.ReadAllText(path);
                return Datas.V0Static.V0JsonDeserialize<List<string>>(text) ?? new List<string>();
            }
            catch (Exception ex)
            {
                V0LogError($"Не удалось прочитать список моделей из {path}: {ex.Message}");
                return new List<string>();
            }
        }

        public Dictionary<string, List<string>> V0TokensGet()
        {
            var path = _config.V0Router.V0PathFileTokens;
            if (!File.Exists(path)) return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var text = File.ReadAllText(path);
                return Datas.V0Static.V0JsonDeserialize<Dictionary<string, List<string>>>(text)
                    ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                V0LogError($"Не удалось прочитать список токенов из {path}: {ex.Message}");
                return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        // =============================================================
        // Роутинг Аудио TTS (Fish Audio с поддержкой выбора модели и ротации токенов)
        // =============================================================
        public async Task<V0RouteAudioTtsResponse> V0AsyncRouteAudioTts(
            V0RouteAudioTtsRequest request,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.V0Text))
            {
                return new V0RouteAudioTtsResponse
                {
                    V0Code = 1,
                    V0Description = "Текст для синтеза речи (v0Text) не может быть пустым."
                };
            }

            var provider = string.IsNullOrWhiteSpace(request.V0ProviderSelect) 
                ? "fishaudio" 
                : request.V0ProviderSelect.Trim().ToLowerInvariant();

            var tokensDict = V0TokensGet();
            var providerUrl = _config.V0GetProviderUrl(provider);

            if (!tokensDict.TryGetValue(provider, out var tokens) || tokens == null || tokens.Count == 0)
            {
                return new V0RouteAudioTtsResponse
                {
                    V0Code = 2,
                    V0Description = $"Для аудио-провайдера '{provider}' не найдено API-токенов в файле V0Tokens.json."
                };
            }

            var format = string.IsNullOrWhiteSpace(request.V0Format) ? "mp3" : request.V0Format.Trim().ToLowerInvariant();
            var model = string.IsNullOrWhiteSpace(request.V0Model) ? "s2.1-pro-free" : request.V0Model.Trim();
            var voiceId = request.V0VoiceId?.Trim();
            var speed = request.V0Speed ?? 1.0;
            var volume = request.V0Volume ?? 0.0;
            var latency = string.IsNullOrWhiteSpace(request.V0Latency) ? "normal" : request.V0Latency.Trim();
            var normalize = request.V0NormalizeIs;
            var now = DateTimeOffset.UtcNow;

            var previewText = request.V0Text.Length > 40 ? request.V0Text.Substring(0, 40) + "..." : request.V0Text;
            V0Log($"[Аудио-Роутер] Запрос TTS. Текст: '{previewText}', Модель: '{model}', Голос: '{voiceId ?? "default"}', Формат: '{format}'");

            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token)) continue;

                var cooldownKey = $"{provider}::{model}::tts::{token}";
                if (_tokenRateLimitCooldowns.TryGetValue(cooldownKey, out var cooledUntil) && cooledUntil > now)
                {
                    var remainingSeconds = (int)(cooledUntil - now).TotalSeconds;
                    V0Log($"[Кулдаун TTS] Токен {V0ProvidersMethods.V0MaskToken(token)} (модель {model}) в кулдауне еще {remainingSeconds}с.");
                    continue;
                }

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(10, _config.V0Router.V0TimeoutSeconds)));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                var result = await V0ProvidersMethods.V0CallFishAudioTts(
                    providerUrl,
                    token,
                    request.V0Text,
                    voiceId,
                    format,
                    speed,
                    volume,
                    latency,
                    normalize,
                    model,
                    linkedCts.Token);

                if (result.V0IsSuccess && result.V0AudioBytes != null && result.V0AudioBytes.Length > 0)
                {
                    V0Log($"[Успех TTS] Аудио синтезировано ({result.V0AudioBytes.Length} байт) через модель '{model}' и токен {V0ProvidersMethods.V0MaskToken(token)}");
                    var mime = result.V0MimeType ?? $"audio/{format}";
                    var base64Pure = Convert.ToBase64String(result.V0AudioBytes);
                    var dataUrl = $"data:{mime};base64,{base64Pure}";

                    return new V0RouteAudioTtsResponse
                    {
                        V0Code = 0,
                        V0Description = "Успех",
                        V0AudioBase64 = dataUrl,
                        V0RawBytes = result.V0AudioBytes,
                        V0MimeType = mime,
                        V0Format = format,
                        V0ModelUsed = model,
                        V0VoiceIdUsed = voiceId,
                        V0ProviderUsed = provider
                    };
                }

                if (result.V0IsRateLimited || result.V0StatusCode == 429)
                {
                    var cooldownDuration = TimeSpan.FromSeconds(_config.V0Router.V0CooldownSecondsOnRateLimit);
                    _tokenRateLimitCooldowns[cooldownKey] = now + cooldownDuration;
                    V0LogWarning($"[RateLimit TTS] Токен {V0ProvidersMethods.V0MaskToken(token)} для модели '{model}' исчерпал лимиты (429). Ротируем токен...");
                    continue;
                }

                V0LogWarning($"[Ошибка TTS] Провайдер '{provider}' (модель {model}): {result.V0ErrorMessage}. Пробуем следующий токен...");
            }

            return new V0RouteAudioTtsResponse
            {
                V0Code = 429,
                V0Description = $"Все доступные API-токены Fish Audio для модели '{model}' исчерпали лимиты (429) или временно недоступны."
            };
        }

        // =============================================================
        // Роутинг Чат-сообщений (LLM / Multimodal)
        // =============================================================
        public async Task<V0RouteMessageResponse> V0AsyncRouteMessageSend(
            V0RouteMessageRequest request,
            CancellationToken ct = default)
        {
            if (request.V0Messages == null || request.V0Messages.Count == 0)
            {
                return new V0RouteMessageResponse
                {
                    V0Code = 1,
                    V0Description = "Список сообщений v0Messages пуст.",
                    V0Content = null,
                    V0FinishReason = null
                };
            }

            if (!string.IsNullOrWhiteSpace(request.V0FileBase64))
            {
                var lastUserMsg = request.V0Messages.LastOrDefault(m => m.V0Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                               ?? request.V0Messages.Last();

                lastUserMsg.V0Files ??= new List<V0FileAttachment>();
                lastUserMsg.V0Files.Add(new V0FileAttachment
                {
                    V0Base64 = request.V0FileBase64
                });
            }

            var requestHasFiles = request.HasFiles();
            var configuredModels = V0ModelsGet();
            var tokensDict = V0TokensGet();

            var modelsToTry = new List<string>();
            var seenModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (request.V0ModelsSelect != null && request.V0ModelsSelect.Count > 0)
            {
                foreach (var model in request.V0ModelsSelect)
                {
                    if (string.IsNullOrWhiteSpace(model)) continue;
                    var trimmed = model.Trim();
                    if (seenModels.Add(trimmed)) modelsToTry.Add(trimmed);
                }
            }

            if (!request.V0ModelsSelectOnlyIs || modelsToTry.Count == 0)
            {
                foreach (var model in configuredModels)
                {
                    if (string.IsNullOrWhiteSpace(model)) continue;
                    var trimmed = model.Trim();
                    if (seenModels.Add(trimmed)) modelsToTry.Add(trimmed);
                }
            }

            if (modelsToTry.Count == 0)
            {
                return new V0RouteMessageResponse
                {
                    V0Code = 2,
                    V0Description = "В файле V0Models.json не настроено ни одной модели.",
                    V0Content = null,
                    V0FinishReason = null
                };
            }

            var temperature = request.V0Temperature ?? 0.7;
            var now = DateTimeOffset.UtcNow;

            foreach (var fullModelEntry in modelsToTry)
            {
                if (string.IsNullOrWhiteSpace(fullModelEntry)) continue;

                var slashIndex = fullModelEntry.IndexOf('/');
                string provider;
                string modelName;

                if (slashIndex >= 0)
                {
                    provider = fullModelEntry.Substring(0, slashIndex).Trim().ToLowerInvariant();
                    modelName = fullModelEntry.Substring(slashIndex + 1).Trim();
                }
                else
                {
                    provider = "openrouter";
                    modelName = fullModelEntry.Trim();
                }

                if (requestHasFiles && (provider == "deepseek" || provider == "cohere"))
                {
                    V0Log($"[Пропуск] Провайдер '{provider}' не поддерживает работу с файлами/изображениями. Пропускаем...");
                    continue;
                }

                var providerUrl = _config.V0GetProviderUrl(provider);

                if (!tokensDict.TryGetValue(provider, out var tokens) || tokens == null || tokens.Count == 0)
                {
                    if (provider is "lmstudio" or "lm-studio" or "lms" || (providerUrl != null && (providerUrl.Contains("localhost") || providerUrl.Contains("127.0.0.1"))))
                    {
                        tokens = new List<string> { "lm-studio" };
                    }
                    else
                    {
                        V0Log($"[Пропуск модели] Для провайдера '{provider}' нет токенов в V0Tokens.json");
                        continue;
                    }
                }

                V0Log($"[Роутер] Пробуем модель: '{fullModelEntry}' (URL: '{providerUrl ?? "default"}', Файлы: {(requestHasFiles ? "Да" : "Нет")})");

                foreach (var token in tokens)
                {
                    if (string.IsNullOrWhiteSpace(token)) continue;

                    var cooldownKey = $"{provider}::{modelName}::{token}";
                    if (_tokenRateLimitCooldowns.TryGetValue(cooldownKey, out var cooledUntil) && cooledUntil > now)
                    {
                        var remainingSeconds = (int)(cooledUntil - now).TotalSeconds;
                        V0Log($"[Кулдаун] Токен {V0ProvidersMethods.V0MaskToken(token)} в кулдауне еще {remainingSeconds}с.");
                        continue;
                    }

                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, _config.V0Router.V0TimeoutSeconds)));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                    var result = await V0ProvidersMethods.V0AsyncCallProvider(
                        provider,
                        modelName,
                        token,
                        request.V0Messages,
                        temperature,
                        providerUrl,
                        linkedCts.Token);

                    if (result.V0IsSuccess)
                    {
                        V0Log($"[Успех] Запрос выполнен моделью '{fullModelEntry}' через токен {V0ProvidersMethods.V0MaskToken(token)}");

                        return new V0RouteMessageResponse
                        {
                            V0Code = 0,
                            V0Description = "Успех",
                            V0Content = result.V0Content,
                            V0FinishReason = result.V0FinishReason,
                            V0ModelUsed = fullModelEntry,
                            V0ProviderUsed = provider
                        };
                    }

                    if (result.V0IsRateLimited || result.V0StatusCode == 429)
                    {
                        var cooldownDuration = TimeSpan.FromSeconds(_config.V0Router.V0CooldownSecondsOnRateLimit);
                        _tokenRateLimitCooldowns[cooldownKey] = now + cooldownDuration;

                        V0LogWarning($"[RateLimit] Токен {V0ProvidersMethods.V0MaskToken(token)} для модели '{modelName}' исчерпал лимиты (429).");
                        continue;
                    }

                    V0LogWarning($"[Ошибка] Модель '{fullModelEntry}': {result.V0ErrorMessage}. Пробуем следующий токен...");
                }
            }

            return new V0RouteMessageResponse
            {
                V0Code = 429,
                V0Description = "Все доступные модели и API-токены исчерпали лимиты или временно недоступны.",
                V0Content = null,
                V0FinishReason = null
            };
        }

        private void V0Log(string message)
        {
            if (_config.V0IsVerbose)
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {message}");
        }

        private void V0LogWarning(string message)
        {
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [WARN] {message}");
        }

        private void V0LogError(string message)
        {
            Console.Error.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [ERROR] {message}");
        }
    }
}