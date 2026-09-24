// =====================================================================
// Entry Point — V0.Programs.Gateways.Services0.Routes
// =====================================================================

using Microsoft.AspNetCore.Mvc;
using S.Programs.Gateways.Services0.Routes.Datas;
using S.Programs.Gateways.Services0.Routes.Methods;

namespace S.Programs.Gateways.Services0.Routes
{
    static public class V0Program
    {
        static public async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var config = Datas.V0Static.V0ConfigLoadAndGet();
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton<V0ServiceRouter>();

            var app = builder.Build();

            // =============================================================
            // 1. Обработчик сообщений чата (LLM)
            // =============================================================
            async Task<IResult> HandleMessageSendAsync(
                [FromBody] V0RouteMessageRequest? bodyRequest,
                [FromQuery] string? v0Role,
                [FromQuery] string? v0Content,
                [FromQuery] string? v0FileBase64,
                [FromQuery] double? v0Temperature,
                [FromQuery] bool? v0StreamIs,
                [FromQuery] string? v0ModelsSelect,
                [FromQuery] bool? v0ModelsSelectOnlyIs,
                [FromServices] V0ServiceRouter router,
                CancellationToken ct)
            {
                var request = bodyRequest ?? new V0RouteMessageRequest();

                if (request.V0Messages.Count == 0 && !string.IsNullOrWhiteSpace(v0Content))
                {
                    request.V0Messages.Add(new V0RouteMessageItem
                    {
                        V0Role = v0Role ?? "user",
                        V0Content = v0Content
                    });
                }

                if (!string.IsNullOrWhiteSpace(v0FileBase64))
                {
                    request.V0FileBase64 = v0FileBase64;
                }

                if (v0Temperature.HasValue)
                {
                    request.V0Temperature = v0Temperature.Value;
                }

                if (v0StreamIs.HasValue)
                {
                    request.V0StreamIs = v0StreamIs.Value;
                }

                if (!string.IsNullOrWhiteSpace(v0ModelsSelect))
                {
                    request.V0ModelsSelect ??= new List<string>();
                    var parsedModels = v0ModelsSelect
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var m in parsedModels)
                    {
                        if (!request.V0ModelsSelect.Contains(m, StringComparer.OrdinalIgnoreCase))
                        {
                            request.V0ModelsSelect.Add(m);
                        }
                    }
                }

                if (v0ModelsSelectOnlyIs.HasValue)
                {
                    request.V0ModelsSelectOnlyIs = v0ModelsSelectOnlyIs.Value;
                }

                var response = await router.V0AsyncRouteMessageSend(request, ct);
                return Results.Json(response);
            }

            app.MapMethods("/API/V0/Route/Message/Send", new[] { "POST", "GET" }, HandleMessageSendAsync);
            app.MapMethods("/API/V0/Message/Send", new[] { "POST", "GET" }, HandleMessageSendAsync);

            // =============================================================
            // 2. Обработчик синтеза речи (Fish Audio TTS)
            // =============================================================
            async Task<IResult> HandleAudioTtsSendAsync(
                [FromBody] V0RouteAudioTtsRequest? bodyRequest,
                [FromQuery] string? v0Text,
                [FromQuery] string? v0Model,
                [FromQuery] string? v0VoiceId,
                [FromQuery] string? v0Format,
                [FromQuery] double? v0Speed,
                [FromQuery] double? v0Volume,
                [FromQuery] string? v0Latency,
                [FromQuery] bool? v0NormalizeIs,
                [FromQuery] string? v0ProviderSelect,
                [FromQuery] bool? v0RawIs,
                [FromServices] V0ServiceRouter router,
                CancellationToken ct)
            {
                var request = bodyRequest ?? new V0RouteAudioTtsRequest();

                if (!string.IsNullOrWhiteSpace(v0Text)) request.V0Text = v0Text;
                if (!string.IsNullOrWhiteSpace(v0Model)) request.V0Model = v0Model;
                if (!string.IsNullOrWhiteSpace(v0VoiceId)) request.V0VoiceId = v0VoiceId;
                if (!string.IsNullOrWhiteSpace(v0Format)) request.V0Format = v0Format;
                if (v0Speed.HasValue) request.V0Speed = v0Speed.Value;
                if (v0Volume.HasValue) request.V0Volume = v0Volume.Value;
                if (!string.IsNullOrWhiteSpace(v0Latency)) request.V0Latency = v0Latency;
                if (v0NormalizeIs.HasValue) request.V0NormalizeIs = v0NormalizeIs.Value;
                if (!string.IsNullOrWhiteSpace(v0ProviderSelect)) request.V0ProviderSelect = v0ProviderSelect;

                var response = await router.V0AsyncRouteAudioTts(request, ct);

                // Если запрошен чистый бинарный поток аудио (например, для вставки в HTML-тег <audio src="...">)
                if (v0RawIs == true && response.V0Code == 0 && response.V0RawBytes != null)
                {
                    return Results.File(
                        fileContents: response.V0RawBytes,
                        contentType: response.V0MimeType ?? "audio/mpeg",
                        fileDownloadName: $"voice.{request.V0Format ?? "mp3"}"
                    );
                }

                return Results.Json(response);
            }

            app.MapMethods("/API/V0/Routes/Audios/TTS", new[] { "POST", "GET" }, HandleAudioTtsSendAsync);
            app.MapMethods("/API/V0/Route/Audio/TTS", new[] { "POST", "GET" }, HandleAudioTtsSendAsync);
            app.MapMethods("/API/V0/Audio/TTS", new[] { "POST", "GET" }, HandleAudioTtsSendAsync);

            // =============================================================
            // 3. Информационные эндпоинты
            // =============================================================
            app.MapGet("/API/V0/Route/Models/Get", ([FromServices] V0ServiceRouter router) =>
            {
                return Results.Json(new
                {
                    v0Code = 0,
                    v0Description = "Успех",
                    v0Value = router.V0ModelsGet()
                });
            });

            app.MapGet("/API/V0/Route/Tokens/Get", ([FromServices] V0ServiceRouter router) =>
            {
                var tokens = router.V0TokensGet();
                var masked = tokens.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.Select(V0ProvidersMethods.V0MaskToken).ToList()
                );

                return Results.Json(new
                {
                    v0Code = 0,
                    v0Description = "Успех",
                    v0Value = masked
                });
            });

            Console.WriteLine("=====================================================================");
            Console.WriteLine(" V0.Programs.Gateways.Services0.Routes (AI Router + Fish Audio TTS)");
            Console.WriteLine($" Адрес шлюза     : http://{config.V0Server.V0Host}:{config.V0Server.V0Port}");
            Console.WriteLine($" Роут диалогов   : /API/V0/Route/Message/Send");
            Console.WriteLine($" Роут аудио TTS  : /API/V0/Routes/Audios/TTS");
            Console.WriteLine($" Файл моделей    : {Path.GetFullPath(config.V0Router.V0PathFileModels)}");
            Console.WriteLine($" Файл токенов    : {Path.GetFullPath(config.V0Router.V0PathFileTokens)}");
            Console.WriteLine("=====================================================================");

            await app.RunAsync($"http://{config.V0Server.V0Host}:{config.V0Server.V0Port}");
        }
    }
}