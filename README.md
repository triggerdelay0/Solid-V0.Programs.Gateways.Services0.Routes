# V0.Programs.Gateways.Services0.Routes

Высокопроизводительный шлюз маршрутизации запросов к языковым моделям (LLM), мультимодальным сервисам и синтезу речи (TTS) на базе .NET 8 (ASP.NET Core Web API).

Сервис обеспечивает единую точку входа (API Gateway) для всех внешних интерфейсов и прокси, распределяя нагрузку между облачными провайдерами (Google Gemini, Alibaba DashScope, OpenRouter, DeepSeek, Zhipu Z.ai, Moonshot Kimi, Cohere, OpenCode) и локальными средами исполнения (LM Studio), а также реализует ротацию токенов и каскадный failover при исчерпании лимитов (429 Rate Limit).

---

## Основные возможности

1. Единый шлюз маршрутизации (Smart Failover):
   - Автоматический перебор моделей из V0Models.json при возникновении ошибок или исчерпании квот.
   - Независимый кулдаун для API-ключей при получении HTTP 429 / insufficient_quota (настраивается в V0Config.json).
   - Автоматическая ротация нескольких токенов одного провайдера из V0Tokens.json.

2. Мультимодальность (Vision & Attachments):
   - Прием одного или группы файлов в формате base64 (V0FileAttachment).
   - Автоматическое определение MIME-типов по сигнатурам и расширениям файлов.
   - Адаптация форматов под специфику каждого провайдера (OpenAI Vision standard, Gemini inlineData, LM Studio input_image).
   - Автоматический пропуск провайдеров, не поддерживающих анализ файлов, если в запросе переданы вложения.

3. Синтез речи (Fish Audio TTS):
   - Эндпоинт /API/V0/Routes/Audios/TTS для генерации аудио с поддержкой моделей s2.1-pro-free и s2.1-pro.
   - Поддержка форматов mp3, wav, opus, pcm.
   - Возможность отдачи аудио как в виде data:audio/...;base64,... в JSON, так и чистым бинарным потоком (параметр v0RawIs=true).

4. Локальные модели (LM Studio):
   - Прямая работа с локальным сервером LM Studio (http://localhost:1234/v1).
   - Поддержка классического /chat/completions и интерфейса /responses.
   - Автоматическая очистка служебных рассуждений (<think>...</think>).

5. JSON с комментариями (JSONC):
   - Все файлы конфигурации (V0Config.json, V0Models.json, V0Tokens.json) поддерживают однострочные (//) и многострочные (/* */) комментарии, а также висячие запятые.

---

## Системные требования

- .NET 8 SDK (версия 8.0.100 или новее)
- Доступ к внешним API провайдеров или запущенный локальный сервер (LM Studio)

---

## Структура проекта

```text
V0.Programs.Gateways.Services0.Routes/
├── V0.Programs.Gateways.Services0.Routes.csproj   # Файл проекта .NET 8 (Newtonsoft.Json)
├── V0Program.cs                                    # Точка входа, регистрация DI и эндпоинтов Minimal API
├── V0Config.json                                   # Настройки хоста, порта, таймаутов и базовых URL провайдеров
├── V0ConfigsDatas.cs                               # Парсер JSON с удалением комментариев и загрузка конфигурации
├── V0Models.json                                   # Приоритетный упорядоченный список моделей для роутинга
├── V0Tokens.json                                   # Пул API-токенов по каждому провайдеру
├── V0Datas.cs                                      # Модели запросов и ответов (DTO) для LLM и TTS
├── V0ProvidersMethods.cs                           # Интеграции с API (Google, OpenRouter, Fish Audio, LM Studio и др.)
└── V0ServicesMethods.cs                            # Бизнес-логика маршрутизатора, учет кулдаунов и ротация токенов
```

---

## Конфигурация

### 1. V0Config.json (Параметры сервера и сетевых провайдеров)

```json
{
  "v0Server": {
    "v0Host": "0.0.0.0",
    "v0Port": 8042
  },
  "v0Router": {
    "v0PathFileModels": "V0Models.json",
    "v0PathFileTokens": "V0Tokens.json",
    "v0TimeoutSeconds": 120,
    "v0CooldownSecondsOnRateLimit": 60,
    "v0ProviderUrls": {
      "fishaudio": "https://api.fish.audio",
      "lmstudio": "http://localhost:1234/v1",
      "openrouter": "https://openrouter.ai/api/v1",
      "google": "https://generativelanguage.googleapis.com/v1beta",
      "alibaba": "https://dashscope-intl.aliyuncs.com/compatible-mode/v1",
      "deepseek": "https://api.deepseek.com",
      "kimi": "https://api.moonshot.cn/v1",
      "zai": "https://open.bigmodel.cn/api/paas/v4",
      "cohere": "https://api.cohere.com/v2",
      "opencode": "https://opencode.ai/zen/v1",
      "orcarouter": "https://api.orcarouter.ai/v1",
      "tokenrouter": "https://api.tokenrouter.com/v1"
    }
  },
  "v0IsVerbose": true
}
```

- v0Server.v0Port: порт, на котором слушает сервис (по умолчанию 8042).
- v0Router.v0CooldownSecondsOnRateLimit: время в секундах, на которое блокируется токен при получении ошибки 429.
- v0Router.v0TimeoutSeconds: предельный таймаут ожидания ответа от каждого отдельного провайдера.

### 2. V0Tokens.json (Управление ключами)

Добавьте API-ключи в соответствующие массивы. Можно указывать несколько ключей для одного провайдера для автоматической ротации:

```json
{
  "google": [
    "AIzaSyYourGoogleApiKey1",
    "AIzaSyYourGoogleApiKey2"
  ],
  "openrouter": [
    "sk-or-v1-your-openrouter-key"
  ],
  "fishaudio": [
    "your-fish-audio-token"
  ]
}
```

### 3. V0Models.json (Приоритет моделей)

Формат записей: `<провайдер>/<название_модели>`. Роутер перебирает модели строго сверху вниз:

```json
[
  "google/gemini-2.5-flash",
  "openrouter/google/gemma-4-26b-a4b:free",
  "alibaba/qwen3.5-flash",
  "lmstudio/default"
]
```

---

## Сборка и запуск

### Запуск в режиме разработки

```bash
# Перейдите в каталог проекта
cd V0.Programs.Gateways.Services0.Routes

# Восстановление зависимостей
dotnet restore

# Запуск приложения
dotnet run
```

После старта в консоли отобразится информация:
```text
=====================================================================
 V0.Programs.Gateways.Services0.Routes (AI Router + Fish Audio TTS)
 Адрес шлюза     : http://0.0.0.0:8042
 Роут диалогов   : /API/V0/Route/Message/Send
 Роут аудио TTS  : /API/V0/Routes/Audios/TTS
 Файл моделей    : .../V0Models.json
 Файл токенов    : .../V0Tokens.json
=====================================================================
```

### Сборка Release-версии

```bash
dotnet publish -c Release -o ./publish
```

Запуск скомпилированного сервиса:
```bash
dotnet ./publish/V0.Programs.Gateways.Services0.Routes.dll
```

---

## Спецификация API

### 1. Отправка сообщений и диалогов (LLM / Multimodal)

- URL: `/API/V0/Route/Message/Send` (алиас: `/API/V0/Message/Send`)
- Метод: `POST` или `GET`
- Content-Type: `application/json`

#### Пример запроса (JSON):
```json
{
  "v0Messages": [
    {
      "v0Role": "system",
      "v0Content": "Ты — полезный ассистент."
    },
    {
      "v0Role": "user",
      "v0Content": "Опиши, что изображено на картинке и ответь на вопрос.",
      "v0Files": [
        {
          "v0FileName": "screenshot.png",
          "v0MimeType": "image/png",
          "v0Base64": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA..."
        }
      ]
    }
  ],
  "v0Temperature": 0.7,
  "v0ModelsSelect": ["google/gemini-2.5-flash"],
  "v0ModelsSelectOnlyIs": false
}
```

#### Параметры запроса:
| Поле | Тип | Описание |
| :--- | :--- | :--- |
| `v0Messages` | `Array` | История сообщений диалога с ролями (`user`, `assistant`, `system`). |
| `v0Messages[].v0Files` | `Array` | Список вложенных файлов (`v0FileName`, `v0MimeType`, `v0Base64`). |
| `v0FileBase64` | `String` | Упрощенное поле для прикрепления одного файла к последнему сообщению. |
| `v0Temperature` | `Double` | Креативность ответа (от 0.0 до 1.5, по умолчанию 0.7). |
| `v0ModelsSelect` | `Array` | Приоритетная модель или список моделей для вызова в первую очередь. |
| `v0ModelsSelectOnlyIs` | `Boolean` | Если `true`, роутер пробует только модели из `v0ModelsSelect` без общего fallback. |

#### Пример ответа:
```json
{
  "v0Code": 0,
  "v0Description": "Успех",
  "v0Content": "На изображении представлен интерфейс консоли...",
  "v0FinishReason": "stop",
  "v0ModelUsed": "google/gemini-2.5-flash",
  "v0ProviderUsed": "google"
}
```

---

### 2. Синтез речи (Fish Audio TTS)

- URL: `/API/V0/Routes/Audios/TTS` (алиасы: `/API/V0/Route/Audio/TTS`, `/API/V0/Audio/TTS`)
- Метод: `POST` или `GET`
- Content-Type: `application/json`

#### Пример запроса:
```json
{
  "v0Text": "Привет! Ответ сгенерирован шлюзом маршрутизации.",
  "v0Model": "s2.1-pro-free",
  "v0Format": "mp3",
  "v0Speed": 1.0,
  "v0Volume": 0.0,
  "v0NormalizeIs": true
}
```

#### Пример ответа (JSON):
```json
{
  "v0Code": 0,
  "v0Description": "Успех",
  "v0AudioBase64": "data:audio/mp3;base64,//uQZAAAAAAAAAAAAAAAAAAAAAA...",
  "v0Format": "mp3",
  "v0ModelUsed": "s2.1-pro-free",
  "v0VoiceIdUsed": null,
  "v0ProviderUsed": "fishaudio"
}
```

Примечание: передача параметра `?v0RawIs=true` в URL возвращает чистый бинарный поток аудио (с заголовком `Content-Type: audio/mpeg`), подходящий для прямого воспроизведения в теге `<audio src="...">`.

---

### 3. Получение списка доступных моделей

- URL: `/API/V0/Route/Models/Get`
- Метод: `GET`

#### Пример ответа:
```json
{
  "v0Code": 0,
  "v0Description": "Успех",
  "v0Value": [
    "google/gemini-2.5-flash",
    "openrouter/google/gemma-4-26b-a4b:free",
    "alibaba/qwen3.5-flash"
  ]
}
```

---

### 4. Получение статуса токенов (маскированных)

- URL: `/API/V0/Route/Tokens/Get`
- Метод: `GET`

Возвращает список зарегистрированных токенов в маскированном виде для диагностики подключений:
```json
{
  "v0Code": 0,
  "v0Description": "Успех",
  "v0Value": {
    "google": ["AIza...Token1", "AIza...Token2"],
    "openrouter": ["sk-o...oken1"]
  }
}
```

---

## Поведение при ошибках и лимитах (Failover)

1. HTTP 429 / Превышение квоты: токен помечается временной меткой блокировки на `v0CooldownSecondsOnRateLimit` секунд. Роутер пробует следующий токен того же провайдера.
2. Исчерпание токенов провайдера: роутер переходит к следующей модели из списка `V0Models.json`.
3. Общий отказ: если ни один провайдер не смог ответить или все токены находятся в кулдауне, возвращается ответ с кодом `429`:
   ```json
   {
     "v0Code": 429,
     "v0Description": "Все доступные модели и API-токены исчерпали лимиты или временно недоступны.",
     "v0Content": null
   }
   ```
