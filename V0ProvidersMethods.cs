// =====================================================================
// Methods — Providers Integrations with Multimodal, LM Studio, Fish Audio & URLs
// =====================================================================

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using S.Programs.Gateways.Services0.Routes.Datas;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace S.Programs.Gateways.Services0.Routes.Methods
{
    public static class V0ProvidersMethods
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        private static readonly Regex DataUriRegex = new Regex(
            @"^data:(?<mime>[\w\/\+\-\.]+);base64,(?<data>.+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public static (string MimeType, string CleanBase64, string DataUrl) V0ParseBase64(string rawData, string? explicitMime = null, string? fileName = null)
        {
            if (string.IsNullOrWhiteSpace(rawData))
            {
                return ("application/octet-stream", "", "");
            }

            var trimmed = rawData.Trim();
            var match = DataUriRegex.Match(trimmed);

            if (match.Success)
            {
                var mime = match.Groups["mime"].Value;
                var data = match.Groups["data"].Value.Trim();
                return (mime, data, trimmed);
            }

            var mimeType = explicitMime;
            if (string.IsNullOrWhiteSpace(mimeType) && !string.IsNullOrWhiteSpace(fileName))
            {
                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                mimeType = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".webp" => "image/webp",
                    ".gif" => "image/gif",
                    ".pdf" => "application/pdf",
                    _ => null
                };
            }

            mimeType ??= "image/png";
            var dataUrl = $"data:{mimeType};base64,{trimmed}";
            return (mimeType, trimmed, dataUrl);
        }

        public static async Task<V0ProviderCallResult> V0AsyncCallProvider(
            string provider,
            string modelName,
            string apiKey,
            List<V0RouteMessageItem> messages,
            double temperature,
            string? providerUrl = null,
            CancellationToken ct = default)
        {
            var providerKey = provider.Trim().ToLowerInvariant();

            return providerKey switch
            {
                "lmstudio" or "lm-studio" or "lms" => await V0CallLmStudio(providerUrl, modelName, apiKey, messages, temperature, ct),
                "google" or "gemini" => await V0CallGoogleGemini(providerUrl, modelName, apiKey, messages, temperature, ct),
                "cohere" => await V0CallCohere(providerUrl, modelName, apiKey, messages, temperature, ct),
                "openrouter" => await V0CallOpenRouter(providerUrl, modelName, apiKey, messages, temperature, ct),
                "alibaba" or "dashscope" or "qwen" => await V0CallAlibabaDashScope(providerUrl, modelName, apiKey, messages, temperature, ct),
                "deepseek" => await V0CallDeepSeek(providerUrl, modelName, apiKey, messages, temperature, ct),
                "kimi" or "moonshot" => await V0CallKimiMoonshot(providerUrl, modelName, apiKey, messages, temperature, ct),
                "zai" or "z.ai" or "zhipu" or "bigmodel" => await V0CallZai(providerUrl, modelName, apiKey, messages, temperature, ct),
                "opencode" or "opencode-zen" or "opencodeai" => await V0CallOpenCode(providerUrl, modelName, apiKey, messages, temperature, ct),
                "orcarouter" => await V0CallGenericOpenAiWithVision(V0AppendEndpoint(providerUrl, "https://api.orcarouter.ai/v1", "/chat/completions"), modelName, apiKey, messages, temperature, ct),
                "tokenrouter" => await V0CallGenericOpenAiWithVision(V0AppendEndpoint(providerUrl, "https://api.tokenrouter.com/v1", "/chat/completions"), modelName, apiKey, messages, temperature, ct),
                _ => !string.IsNullOrWhiteSpace(providerUrl)
                    ? await V0CallGenericOpenAiWithVision(V0AppendEndpoint(providerUrl, providerUrl, "/chat/completions"), modelName, apiKey, messages, temperature, ct)
                    : await V0CallOpenRouter(null, modelName, apiKey, messages, temperature, ct)
            };
        }

        public static string V0AppendEndpoint(string? customUrl, string defaultBaseUrl, string endpointPath)
        {
            var baseTarget = !string.IsNullOrWhiteSpace(customUrl) ? customUrl.Trim() : defaultBaseUrl.Trim();
            if (baseTarget.EndsWith(endpointPath, StringComparison.OrdinalIgnoreCase))
            {
                return baseTarget;
            }
            return $"{baseTarget.TrimEnd('/')}{endpointPath}";
        }

        // =============================================================
        // Fish Audio — Синтез речи (TTS)
        // =============================================================
        public static async Task<V0AudioProviderCallResult> V0CallFishAudioTts(
            string? providerUrl,
            string apiKey,
            string text,
            string? voiceId = null,
            string format = "mp3",
            double speed = 1.0,
            double volume = 0.0,
            string latency = "normal",
            bool normalize = true,
            string model = "s2.1-pro-free",
            CancellationToken ct = default)
        {
            var endpoint = V0AppendEndpoint(providerUrl, "https://api.fish.audio", "/v1/tts");

            var payload = new Dictionary<string, object?>
            {
                ["text"] = text,
                ["format"] = string.IsNullOrWhiteSpace(format) ? "mp3" : format.ToLowerInvariant(),
                ["latency"] = string.IsNullOrWhiteSpace(latency) ? "normal" : latency,
                ["normalize"] = normalize,
                ["prosody"] = new
                {
                    speed = speed,
                    volume = volume
                }
            };

            if (!string.IsNullOrWhiteSpace(voiceId))
            {
                payload["reference_id"] = voiceId.Trim();
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };

            var chosenModel = string.IsNullOrWhiteSpace(model) ? "s2.1-pro-free" : model.Trim();

            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("model", chosenModel);

            try
            {
                using var response = await _httpClient.SendAsync(request, ct);

                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode == 429)
                {
                    var err = await response.Content.ReadAsStringAsync(ct);
                    return new V0AudioProviderCallResult
                    {
                        V0IsSuccess = false,
                        V0IsRateLimited = true,
                        V0StatusCode = 429,
                        V0ErrorMessage = err
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(ct);
                    var isQuota = err.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
                                  err.Contains("rate_limit", StringComparison.OrdinalIgnoreCase) ||
                                  err.Contains("credits_depleted", StringComparison.OrdinalIgnoreCase);

                    return new V0AudioProviderCallResult
                    {
                        V0IsSuccess = false,
                        V0IsRateLimited = isQuota,
                        V0StatusCode = (int)response.StatusCode,
                        V0ErrorMessage = err
                    };
                }

                var audioBytes = await response.Content.ReadAsByteArrayAsync(ct);
                var fmt = format.ToLowerInvariant();
                var mimeType = fmt switch
                {
                    "mp3" => "audio/mpeg",
                    "wav" => "audio/wav",
                    "opus" => "audio/opus",
                    "pcm" => "audio/pcm",
                    _ => "audio/mpeg"
                };

                return new V0AudioProviderCallResult
                {
                    V0IsSuccess = true,
                    V0AudioBytes = audioBytes,
                    V0MimeType = mimeType,
                    V0StatusCode = 200
                };
            }
            catch (Exception ex)
            {
                return new V0AudioProviderCallResult
                {
                    V0IsSuccess = false,
                    V0StatusCode = 500,
                    V0ErrorMessage = ex.Message
                };
            }
        }

        // =============================================================
        // LM Studio API
        // =============================================================
        private static async Task<V0ProviderCallResult> V0CallLmStudio(
            string? providerUrl,
            string modelName,
            string apiKey,
            List<V0RouteMessageItem> messages,
            double temperature,
            CancellationToken ct)
        {
            var baseClean = !string.IsNullOrWhiteSpace(providerUrl) ? providerUrl.Trim().TrimEnd('/') : "http://localhost:1234/v1";
            var token = string.IsNullOrWhiteSpace(apiKey) ? "lm-studio" : apiKey;

            if (baseClean.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
            {
                return await V0CallLmStudioResponses(baseClean, modelName, token, messages, temperature, ct);
            }

            if (baseClean.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return await V0CallGenericOpenAiWithVision(baseClean, modelName, token, messages, temperature, ct);
            }

            var chatCompletionsUrl = baseClean.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? $"{baseClean}/chat/completions"
                : $"{baseClean}/v1/chat/completions";

            var responsesUrl = baseClean.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? $"{baseClean}/responses"
                : $"{baseClean}/v1/responses";

            var isResponsesPreferred = modelName.StartsWith("responses/", StringComparison.OrdinalIgnoreCase);
            var cleanModel = isResponsesPreferred ? modelName.Substring("responses/".Length) : modelName;

            if (isResponsesPreferred)
            {
                var respResult = await V0CallLmStudioResponses(responsesUrl, cleanModel, token, messages, temperature, ct);
                if (respResult.V0IsSuccess) return respResult;
                return await V0CallGenericOpenAiWithVision(chatCompletionsUrl, cleanModel, token, messages, temperature, ct);
            }

            var chatResult = await V0CallGenericOpenAiWithVision(chatCompletionsUrl, cleanModel, token, messages, temperature, ct);
            if (chatResult.V0IsSuccess) return chatResult;

            if (chatResult.V0StatusCode == 404 || chatResult.V0StatusCode == 405)
            {
                var respResult = await V0CallLmStudioResponses(responsesUrl, cleanModel, token, messages, temperature, ct);
                if (respResult.V0IsSuccess) return respResult;
            }

            return chatResult;
        }

        public static async Task<V0ProviderCallResult> V0CallLmStudioResponses(
            string endpointUrl,
            string modelName,
            string apiKey,
            List<V0RouteMessageItem> messages,
            double temperature,
            CancellationToken ct)
        {
            var inputItems = new List<object>();

            foreach (var m in messages)
            {
                var hasFiles = m.V0Files != null && m.V0Files.Count > 0;
                if (!hasFiles)
                {
                    inputItems.Add(new
                    {
                        role = m.V0Role,
                        content = m.V0Content
                    });
                }
                else
                {
                    var contentParts = new List<object>();
                    if (!string.IsNullOrWhiteSpace(m.V0Content))
                    {
                        contentParts.Add(new
                        {
                            type = "input_text",
                            text = m.V0Content
                        });
                    }

                    foreach (var file in m.V0Files!)
                    {
                        var (_, _, dataUrl) = V0ParseBase64(file.V0Base64, file.V0MimeType, file.V0FileName);
                        if (!string.IsNullOrEmpty(dataUrl))
                        {
                            contentParts.Add(new
                            {
                                type = "input_image",
                                image_url = dataUrl
                            });
                        }
                    }

                    inputItems.Add(new
                    {
                        role = m.V0Role,
                        content = contentParts
                    });
                }
            }

            var payload = new
            {
                model = modelName,
                input = inputItems,
                temperature = temperature,
                stream = false
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };

            var key = string.IsNullOrWhiteSpace(apiKey) ? "lm-studio" : apiKey;
            request.Headers.Add("Authorization", $"Bearer {key}");

            return await V0SendAndParseOpenAiResponses(request, ct);
        }

        private static async Task<V0ProviderCallResult> V0SendAndParseOpenAiResponses(
            HttpRequestMessage request,
            CancellationToken ct)
        {
            try
            {
                using var response = await _httpClient.SendAsync(request, ct);
                var responseJson = await response.Content.ReadAsStringAsync(ct);

                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode == 429)
                {
                    return new V0ProviderCallResult
                    {
                        V0IsSuccess = false,
                        V0IsRateLimited = true,
                        V0StatusCode = 429,
                        V0ErrorMessage = responseJson
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    return new V0ProviderCallResult
                    {
                        V0IsSuccess = false,
                        V0StatusCode = (int)response.StatusCode,
                        V0ErrorMessage = responseJson
                    };
                }

                var jObj = JObject.Parse(responseJson);

                var directText = jObj["output_text"]?.ToString();
                if (!string.IsNullOrWhiteSpace(directText))
                {
                    return new V0ProviderCallResult
                    {
                        V0IsSuccess = true,
                        V0Content = V0CleanThinkTags(directText),
                        V0FinishReason = jObj["status"]?.ToString() ?? "stop",
                        V0StatusCode = 200
                    };
                }

                if (jObj["output"] is JArray outputArray && outputArray.Count > 0)
                {
                    var textBuilder = new StringBuilder();
                    foreach (var item in outputArray)
                    {
                        var contentToken = item["content"];
                        if (contentToken is JArray contentParts)
                        {
                            foreach (var part in contentParts)
                            {
                                var t = part["text"]?.ToString();
                                if (!string.IsNullOrEmpty(t)) textBuilder.Append(t);
                            }
                        }
                        else if (contentToken != null && contentToken.Type == JTokenType.String)
                        {
                            textBuilder.Append(contentToken.ToString());
                        }
                        else if (item["text"] != null)
                        {
                            textBuilder.Append(item["text"]?.ToString());
                        }
                    }

                    if (textBuilder.Length > 0)
                    {
                        return new V0ProviderCallResult
                        {
                            V0IsSuccess = true,
                            V0Content = V0CleanThinkTags(textBuilder.ToString()),
                            V0FinishReason = jObj["status"]?.ToString() ?? "stop",
                            V0StatusCode = 200
                        };
                    }
                }

                if (jObj["choices"] is JArray choices && choices.Count > 0)
                {
                    var choice = choices[0];
                    var content = choice["message"]?["content"]?.ToString() ?? "";
                    var finishReason = choice["finish_reason"]?.ToString() ?? "stop";

                    return new V0ProviderCallResult
                    {
                        V0IsSuccess = true,
                        V0Content = V0CleanThinkTags(content),
                        V0FinishReason = finishReason,
                        V0StatusCode = 200
                    };
                }

                return new V0ProviderCallResult
                {
                    V0IsSuccess = false,
                    V0StatusCode = 200,
                    V0ErrorMessage = "Не удалось извлечь текст ответа из /v1/responses: " + responseJson
                };
            }
            catch (Exception ex)
            {
                return new V0ProviderCallResult
                {
                    V0IsSuccess = false,
                    V0StatusCode = 500,
                    V0ErrorMessage = ex.Message
                };
            }
        }

        // =============================================================
        // Google Gemini API
        // =============================================================
        private static async Task<V0ProviderCallResult> V0CallGoogleGemini(
            string? providerUrl,
            string modelName,
            string apiKey,
            List<V0RouteMessageItem> messages,
            double temperature,
            CancellationToken ct)
        {
            var cleanModel = modelName.StartsWith("models/") ? modelName[7..] : modelName;

            if (cleanModel.Equals("gemini-3-flash", StringComparison.OrdinalIgnoreCase))
                cleanModel = "gemini-3-flash-preview";
            else if (cleanModel.Equals("gemini-3.1-pro", StringComparison.OrdinalIgnoreCase))
                cleanModel = "gemini-3.1-pro-preview";
            else if (cleanModel.Equals("gemma-4-26b", StringComparison.OrdinalIgnoreCase))
                cleanModel = "gemma-4-26b-a4b-it";
            else if (cleanModel.Equals("gemma-4-31b", StringComparison.OrdinalIgnoreCase))
                cleanModel = "gemma-4-31b-it";

            var baseEndpoint = !string.IsNullOrWhiteSpace(providerUrl)
                ? providerUrl.TrimEnd('/')
                : "https://generativelanguage.googleapis.com/v1beta";

            var url = $"{baseEndpoint}/models/{cleanModel}:generateContent?key={apiKey}";

            string? systemInstructionText = null;
            var contents = new List<object>();

            foreach (var msg in messages)
            {
                var role = msg.V0Role.Trim().ToLowerInvariant();
                if (role is "system" or "developer")
                {
                    systemInstructionText = msg.V0Content;
                    continue;
                }

                var geminiRole = role == "assistant" ? "model" : "user";
                var parts = new List<object>();

                if (!string.IsNullOrEmpty(msg.V0Content))
                {
                    parts.Add(new { text = msg.V0Content });
                }

                if (msg.V0Files != null && msg.V0Files.Count > 0)
                {
                    foreach (var file in msg.V0Files)
                    {
                        var (mime, base64Data, _) = V0ParseBase64(file.V0Base64, file.V0MimeType, file.V0FileName);
                        if (!string.IsNullOrEmpty(base64Data))
                        {
                            parts.Add(new
                            {
                                inlineData = new
                                {
                                    mimeType = mime,
                                    data = base64Data
                                }
                            });
                        }
                    }
                }

                contents.Add(new
                {
                    role = geminiRole,
                    parts = parts
                });
            }

            object thinkingConfig = cleanModel.Contains("gemini-3.7", StringComparison.OrdinalIgnoreCase)
                ? new { thinkingLevel = "LOW" }
                : (cleanModel.Contains("gemma", StringComparison.OrdinalIgnoreCase) || cleanModel.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase))
                    ? new { thinkingLevel = "MINIMAL" }
                    : new { thinkingBudget = 0 };

            var payload = new Dictionary<string, object>
            {
                ["contents"] = contents,
                ["generationConfig"] = new
                {
                    temperature = temperature,
                    thinkingConfig = thinkingConfig
                }
            };

            if (!string.IsNullOrWhiteSpace(systemInstructionText))
            {
                payload["systemInstruction"] = new
                {
                    parts = new object[] { new { text = systemInstructionText } }
                };
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };

            return await V0SendAndParseGemini(request, ct);
        }

        private static async Task<V0ProviderCallResult> V0SendAndParseGemini(HttpRequestMessage request, CancellationToken ct)
        {
            try
            {
                using var response = await _httpClient.SendAsync(request, ct);
                var responseJson = await response.Content.ReadAsStringAsync(ct);

                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode == 429)
                {
                    return new V0ProviderCallResult
                    {
                        V0IsSuccess = false,
                        V0IsRateLimited = true,
                        V0StatusCode = 429,
                        V0ErrorMessage = responseJson
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    var isQuota = responseJson.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
                                  responseJson.Contains("QUOTA_EXCEEDED", StringComparison.OrdinalIgnoreCase);

                    return new V0ProviderCallResult
                    {
                        V0IsSuccess = false,
                        V0IsRateLimited = isQuota,
                        V0StatusCode = (int)response.StatusCode,
                        V0ErrorMessage = responseJson
                    };
                }

                var jObj = JObject.Parse(responseJson);
                var candidates = jObj["candidates"] as JArray;
                if (candidates != null && candidates.Count > 0)
                {
                    var candidate = candidates[0];
                    var finishReason = candidate["finishReason"]?.ToString() ?? "stop";
                    var parts = candidate["content"]?["parts"] as JArray;

                    var textBuilder = new StringBuilder();
                    if (parts != null)
                    {
                        foreach (var part in parts)
                        {
                            var isThought = part["thought"]?.Value<bool>() ?? false;
                            if (!isThought)
                            {
                                var text = part["text"]?.ToString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    textBuilder.Append(text);
                                }
                            }
                        }
                    }

                    return new V0ProviderCallResult
                    {
                        V0IsSuccess = true,
                        V0Content = textBuilder.ToString(),
                        V0FinishReason = finishReason.ToLowerInvariant(),
                        V0StatusCode = 200
                    };
                }

                return new V0ProviderCallResult
                {
                    V0IsSuccess = false,
                    V0StatusCode = 200,
                    V0ErrorMessage = "No candidates in Gemini response"
                };
            }
            catch (Exception ex)
            {
                return new V0ProviderCallResult
                {
                    V0IsSuccess = false,
                    V0StatusCode = 500,
                    V0ErrorMessage = ex.Message
                };
            }
        }

        public static List<object> V0BuildOpenAiMessages(List<V0RouteMessageItem> messages)
        {
            var openAiMessages = new List<object>();

            foreach (var m in messages)
            {
                var hasFiles = m.V0Files != null && m.V0Files.Count > 0;

                if (!hasFiles)
                {
                    openAiMessages.Add(new
                    {
                        role = m.V0Role,
                        content = m.V0Content
                    });
                }
                else
                {
                    var contentParts = new List<object>();

                    if (!string.IsNullOrWhiteSpace(m.V0Content))
                    {
                        contentParts.Add(new
                        {
                            type = "text",
                            text = m.V0Content
                        });
                    }

                    foreach (var file in m.V0Files!)
                    {
                        var (_, _, dataUrl) = V0ParseBase64(file.V0Base64, file.V0MimeType, file.V0FileName);
                        if (!string.IsNullOrEmpty(dataUrl))
                        {
                            contentParts.Add(new
                            {
                                type = "image_url",
                                image_url = new { url = dataUrl }
                            });
                        }
                    }

                    openAiMessages.Add(new
                    {
                        role = m.V0Role,
                        content = contentParts
                    });
                }
            }

            return openAiMessages;
        }

        private static async Task<V0ProviderCallResult> V0CallOpenRouter(
            string? providerUrl,
            string modelName,
            string apiKey,
            List<V0RouteMessageItem> messages,
            double temperature,
            CancellationToken ct)
        {
            var isReasoningMandatory = modelName.Contains("minimax", StringComparison.OrdinalIgnoreCase) ||
                                       modelName.Contains("stepfun", StringComparison.OrdinalIgnoreCase);

            var endpoint = V0AppendEndpoint(providerUrl, "https://openrouter.ai/api/v1", "/chat/completions");

            var result = await V0SendOpenRouterRequest(endpoint, modelName, apiKey, messages, temperature, disableReasoning: !isReasoningMandatory, ct);

            if (!result.V0IsSuccess && result.V0StatusCode == 400 &&
                result.V0ErrorMessage != null &&
                result.V0ErrorMessage.Contains("Reasoning is mandatory", StringComparison.OrdinalIgnoreCase))
            {
                result = await V0SendOpenRouterRequest(endpoint, modelName, apiKey, messages, temperature, disableReasoning: false, ct);
            }

            return result;
        }

        private static async Task<V0ProviderCallResult> V0SendOpenRouterRequest(
            string endpointUrl,
            string modelName,
            string apiKey,
            List<V0RouteMessageItem> messages,
            double temperature,
            bool disableReasoning,
            CancellationToken ct)
        {
            var openAiMessages = V0BuildOpenAiMessages(messages);

            object payload;
            if (disableReasoning)
            {
                payload = new
                {
                    model = modelName,
                    messages = openAiMessages,
                    temperature = temperature,
                    stream = false,
                    reasoning = new { effort = "none" }
                };
            }
            else
            {
                payload = new
                {
                    model = modelName,
                    messages = openAiMessages,
                    temperature = temperature,
                    stream = false,
                    reasoning = new { exclude = true }
                };
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("HTTP-Referer", "https://localhost");
            request.Headers.Add("X-Title", "V0.Programs.Gateways.Services0.Routes");

            return await V0SendAndParseOpenAiCompatible(request, ct);
        }

        private static async Task<V0ProviderCallResult> V0CallAlibabaDashScope(
            string? providerUrl,
            string modelName,
            string apiKey,
            List<V0RouteMessageItem> messages,
            double temperature,
            CancellationToken ct)
        {
            var endpoint = V0AppendEndpoint(providerUrl, "https://dashscope-intl.aliyuncs.com/compatible-mode/v1", "/chat/completions");
            var openAiMessages = V0BuildOpenAiMessages(messages);

            var payload = new
            {
                model = modelName,
                messages = openAiMessages,
                temperature = temperature,
                stream = false,
                enable_search = false
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            return await V0SendAndParseOpenAiCompatible(request, ct);
        }

        private static async Task<V0ProviderCallResult> V0CallDeepSeek(
            string? providerUrl,
            string modelName,
            string apiKey,
            List<V0RouteMessageItem> messages,
            double temperature,
            CancellationToken ct)
        {
            var endpoint = V0AppendEndpoint(providerUrl, "https://api.deepseek.com", "/chat/completions");

            var openAiMessages = messages.Select(m => new
            {
                role = m.V0Role,
                content = m.V0Content
            }).ToList();

            var payload = new
            {
                model = modelName,
                messages = openAiMessages,
                temperature = temperature,
                stream = false
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            return await V0SendAndParseOpenAiCompatible(request, ct);
        }

        private static async Task<V0ProviderCallResult> V0CallKimiMoonshot(
            string? providerUrl,
            string modelName,
            string apiKey,
            List<V0RouteMessageItem> messages,
            double temperature,
            CancellationToken ct)
        {
            var endpoint = V0AppendEndpoint(providerUrl, "https://api.moonshot.cn/v1", "/chat/completions");
            var openAiMessages = V0BuildOpenAiMessages(messages);

            var payload = new
            {
                model = modelName,
                messages = openAiMessages,
                temperature = temperature,
                stream = false
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            return await V0SendAndParseOpenAiCompatible(request, ct);
        }

        private static async Task<V0ProviderCallResult> V0CallZai(
            string? providerUrl,
            string modelName,
            string apiKey,
            List<V0RouteMessageItem> messages,
            double temperature,
            CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(providerUrl))
            {
                var customEndpoint = V0AppendEndpoint(providerUrl, providerUrl, "/chat/completions");
                return await V0CallGenericOpenAiWithVision(customEndpoint, modelName, apiKey, messages, temperature, ct);
            }

            var preferBigModel = modelName.Contains("flash", StringComparison.OrdinalIgnoreCase);

            var primaryUrl = preferBigModel
                ? "https://open.bigmodel.cn/api/paas/v4/chat/completions"
                : "https://api.z.ai/api/paas/v4/chat/completions";

            var secondaryUrl = preferBigModel
                ? "https://api.z.ai/api/paas/v4/chat/completions"
                : "https://open.bigmodel.cn/api/paas/v4/chat/completions";

            var result = await V0CallGenericOpenAiWithVision(primaryUrl, modelName, apiKey, messages, temperature, ct);

            if (!result.V0IsSuccess && (result.V0StatusCode == 400 || (result.V0ErrorMessage != null && result.V0ErrorMessage.Contains("1211"))))
            {
                result = await V0CallGenericOpenAiWithVision(secondaryUrl, modelName, apiKey, messages, temperature, ct);
            }

            return result;
        }

        private static async Task<V0ProviderCallResult> V0CallCohere(
            string? providerUrl,
            string modelName,
            string apiKey,
            List<V0RouteMessageItem> messages,
            double temperature,
            CancellationToken ct)
        {
            var endpoint = V0AppendEndpoint(providerUrl, "https://api.cohere.com/v2", "/chat");

            var cohereMessages = messages.Select(m =>
            {
                var role = m.V0Role.Trim().ToLowerInvariant();
                if (role is "developer") role = "system";

                return new
                {
                    role = role,
                    content = m.V0Content
                };
            }).ToList();

            var payload = new
            {
                model = modelName,
                messages = cohereMessages,
                temperature = temperature
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("Accept", "application/json");

            try
            {
                using var response = await _httpClient.SendAsync(request, ct);
                var responseJson = await response.Content.ReadAsStringAsync(ct);

                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode == 429)
                {
                    return new V0ProviderCallResult
                    {
                        V0IsSuccess = false,
                        V0IsRateLimited = true,
                        V0StatusCode = 429,
                        V0ErrorMessage = responseJson
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    return new V0ProviderCallResult
                    {
                        V0IsSuccess = false,
                        V0StatusCode = (int)response.StatusCode,
                        V0ErrorMessage = responseJson
                    };
                }

                var jObj = JObject.Parse(responseJson);
                var messageObj = jObj["message"];
                var finishReason = jObj["finish_reason"]?.ToString() ?? "complete";

                if (messageObj != null)
                {
                    var textBuilder = new StringBuilder();
                    var contentProp = messageObj["content"];

                    if (contentProp is JArray contentArray)
                    {
                        foreach (var item in contentArray)
                        {
                            var text = item["text"]?.ToString();
                            if (!string.IsNullOrEmpty(text)) textBuilder.Append(text);
                        }
                    }
                    else if (contentProp != null)
                    {
                        textBuilder.Append(contentProp.ToString());
                    }

                    return new V0ProviderCallResult
                    {
                        V0IsSuccess = true,
                        V0Content = textBuilder.ToString(),
                        V0FinishReason = finishReason.ToLowerInvariant(),
                        V0StatusCode = 200
                    };
                }

                return new V0ProviderCallResult
                {
                    V0IsSuccess = false,
                    V0StatusCode = 200,
                    V0ErrorMessage = "No message in Cohere response"
                };
            }
            catch (Exception ex)
            {
                return new V0ProviderCallResult
                {
                    V0IsSuccess = false,
                    V0StatusCode = 500,
                    V0ErrorMessage = ex.Message
                };
            }
        }

        private static async Task<V0ProviderCallResult> V0CallOpenCode(
            string? providerUrl,
            string modelName,
            string apiKey,
            List<V0RouteMessageItem> messages,
            double temperature,
            CancellationToken ct)
        {
            var cleanModel = modelName;
            if (cleanModel.Equals("muse-spark-1.2-free", StringComparison.OrdinalIgnoreCase))
                cleanModel = "muse-spark-1.2";

            var endpoint = V0AppendEndpoint(providerUrl, "https://opencode.ai/zen/v1", "/chat/completions");
            return await V0CallGenericOpenAiWithVision(endpoint, cleanModel, apiKey, messages, temperature, ct);
        }

        public static async Task<V0ProviderCallResult> V0CallGenericOpenAiWithVision(
            string endpointUrl,
            string modelName,
            string apiKey,
            List<V0RouteMessageItem> messages,
            double temperature,
            CancellationToken ct)
        {
            var openAiMessages = V0BuildOpenAiMessages(messages);

            var payload = new
            {
                model = modelName,
                messages = openAiMessages,
                temperature = temperature,
                stream = false
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
            {
                Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
            };

            var key = string.IsNullOrWhiteSpace(apiKey) ? "lm-studio" : apiKey;
            request.Headers.Add("Authorization", $"Bearer {key}");

            return await V0SendAndParseOpenAiCompatible(request, ct);
        }

        private static async Task<V0ProviderCallResult> V0SendAndParseOpenAiCompatible(
            HttpRequestMessage request,
            CancellationToken ct)
        {
            try
            {
                using var response = await _httpClient.SendAsync(request, ct);
                var responseJson = await response.Content.ReadAsStringAsync(ct);

                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode == 429)
                {
                    return new V0ProviderCallResult
                    {
                        V0IsSuccess = false,
                        V0IsRateLimited = true,
                        V0StatusCode = 429,
                        V0ErrorMessage = responseJson
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    var isQuota = responseJson.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
                                  responseJson.Contains("rate_limit_exceeded", StringComparison.OrdinalIgnoreCase) ||
                                  responseJson.Contains("quota_exceeded", StringComparison.OrdinalIgnoreCase) ||
                                  responseJson.Contains("credits_depleted", StringComparison.OrdinalIgnoreCase);

                    return new V0ProviderCallResult
                    {
                        V0IsSuccess = false,
                        V0IsRateLimited = isQuota,
                        V0StatusCode = (int)response.StatusCode,
                        V0ErrorMessage = responseJson
                    };
                }

                var jObj = JObject.Parse(responseJson);
                var choices = jObj["choices"] as JArray;

                if (choices != null && choices.Count > 0)
                {
                    var choice = choices[0];
                    var content = choice["message"]?["content"]?.ToString() ?? "";
                    var finishReason = choice["finish_reason"]?.ToString() ?? "stop";

                    return new V0ProviderCallResult
                    {
                        V0IsSuccess = true,
                        V0Content = V0CleanThinkTags(content),
                        V0FinishReason = finishReason,
                        V0StatusCode = 200
                    };
                }

                return new V0ProviderCallResult
                {
                    V0IsSuccess = false,
                    V0StatusCode = 200,
                    V0ErrorMessage = "No choices in OpenAI-compatible response: " + responseJson
                };
            }
            catch (Exception ex)
            {
                return new V0ProviderCallResult
                {
                    V0IsSuccess = false,
                    V0StatusCode = 500,
                    V0ErrorMessage = ex.Message
                };
            }
        }

        public static string V0CleanThinkTags(string content)
        {
            if (string.IsNullOrEmpty(content)) return "";

            if (content.Contains("<think>") && content.Contains("</think>"))
            {
                var endIdx = content.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
                if (endIdx >= 0)
                {
                    return content.Substring(endIdx + "</think>".Length).TrimStart('\r', '\n', ' ');
                }
            }

            return content;
        }

        public static string V0MaskToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return "***";
            if (token.Length <= 8) return token.Substring(0, Math.Min(3, token.Length)) + "...";
            return token.Substring(0, 4) + "..." + token.Substring(token.Length - 4);
        }
    }
}