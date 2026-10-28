# Ask AEROVA (travel assistant) setup

Ask AEROVA is the chat on every page of the site. The browser never talks to the AI provider: it calls the AEROVA API, the API calls
Google Gemini with a key only the server knows.

## The endpoint

`POST /api/chat`: open to visitors who are not signed in (the chat is on every page), limited per IP address.

Request:

```json
{
  "language": "az",
  "messages": [
    { "role": "user", "content": "Hello" },
    { "role": "assistant", "content": "Welcome to AEROVA! How can I help?" },
    { "role": "user", "content": "How much baggage can I add?" }
  ]
}
```

`messages` is the recent conversation, oldest first, ending with the question to answer. Only the roles `user` and `assistant` are
accepted (there is no `system` role: the browser can never send rules). `message` (a single string) may be sent instead of `messages`.

Response `200`:

```json
{ "reply": "..." }
```

| Status | Meaning |
| --- | --- |
| 400 | Empty or too long message, too many messages, wrong role, or the conversation does not end with a question |
| 429 | Too many questions from this IP address (`Retry-After: 60`) |
| 503 | The assistant is not set up (no key) or the provider failed or is busy; `code` says why (see below) |
| 504 | The provider did not answer in time |

## The key (secret)

The provider key is a **Google AI Studio** key (create it at <https://aistudio.google.com/apikey>; it starts with `AIza`). Its
configuration name is `TravelAssistant:ApiKey`. It is never put in `appsettings.json`, the frontend, Git, the log or a response.
Without it the API runs and the chat answers `503` ("The assistant is not available right now").

Local development, in PowerShell, from the API project folder
(`C:\Users\HP\Desktop\Airline Reservation System Backend\AirlineReservationSystem`). Type the key in the command line yourself; it is
stored in your user profile, outside the project and Git:

```powershell
dotnet user-secrets set "TravelAssistant:ApiKey" "AIza..." --project AirlineReservationSystem
```

Production: set the environment variable (or the secret store of the host)

```bash
TravelAssistant__ApiKey=<your-gemini-api-key>
```

Keys of other providers (they start with `sk-`) are never sent to Google: the API refuses them with `assistant_key_malformed`.
User-secrets are only read when the API starts: restart it after setting the key.

## Check that it works

From the API project folder (the command uses the same settings as the API and never prints the key):

```powershell
dotnet run --project AirlineReservationSystem -- check-assistant --models
```

It lists the models your key can use for answers (the configured one is marked with `*`), then asks the provider one tiny question.
Exit code 0 and "Ask AEROVA works" mean the assistant works. Without `--models` it only asks the tiny question.

## Settings (not secret, `appsettings.json`, section `TravelAssistant`)

| Setting | Default | Meaning |
| --- | --- | --- |
| `Provider` | `Gemini` | Provider that answers (built in: Gemini; others can be added behind `IAssistantProvider`) |
| `Model` | `gemini-2.5-flash` | Model that answers; pick one from `check-assistant --models` |
| `Endpoint` | `https://generativelanguage.googleapis.com/v1beta` | Gemini API address (the model and method are added to it) |
| `ThinkingBudget` | `0` | Tokens the model may spend thinking first; `0` turns thinking off (right for short answers). Remove the setting for a model that cannot turn thinking off |
| `TimeoutSeconds` | `30` | Time given to the provider before the request returns 504 |
| `MaxOutputTokens` | `700` | Longest answer (also bounds the cost of one question) |
| `MaxMessageLength` | `2000` | Characters in one message |
| `MaxMessages` | `20` | Messages in one request |
| `MaxTotalLength` | `12000` | Characters in all messages of one request |
| `RateLimitPerMinute` | `10` | Questions per minute and IP address |

CORS: the allowed frontend origins are the existing `Cors:AllowedOrigins` list (no extra origin is opened for the chat).

The request goes to `models/{Model}:generateContent` with the key in the `x-goog-api-key` header (never in the address, so it cannot
end up in a URL log).

## When it does not work

The API log says what failed on every failed question, and once at start-up ("Ask AEROVA is not set up..." or "provider ..., key
present"). The 503 carries a `code`:

| `code` | What to do |
| --- | --- |
| `assistant_not_configured` | Set `TravelAssistant:ApiKey` (user-secrets or `TravelAssistant__ApiKey`), then restart the API |
| `assistant_key_malformed` | The stored key is not a Gemini key (letters like Ç, spaces or quotes, or an `sk-` key of another provider): set the real key from Google AI Studio and restart |
| `assistant_key_rejected` | Google refused the key (deleted, restricted or mistyped): create a new one |
| `assistant_model_unavailable` | The model does not exist for this key: run `check-assistant --models` and set `TravelAssistant:Model` |
| `assistant_billing` | The project has no billing or plan for this use: check it in Google AI Studio |
| `assistant_busy` | A rate limit or quota of the key's project (the free tier is small) is reached: try again in a minute or check the quota |
| `assistant_request_rejected` | Check `Model`, `MaxOutputTokens` and `ThinkingBudget` (a model that cannot turn thinking off needs it removed) |
| `assistant_provider_error` | Google failed on its side: try again later |
| `assistant_unreachable` | No internet, a firewall, a proxy or a wrong `Endpoint` |
| `assistant_bad_answer` | The answer had no usable text: check `Model`, `Endpoint` and `ThinkingBudget` |
| `assistant_blocked` | Gemini's safety rules stopped the answer: try a different question |

The log holds the code, the HTTP status and the provider's error kind (for example `INVALID_ARGUMENT`), never the key, the question or
the provider's own message.

## What the assistant knows and what it never does

- Server-side rules (a system instruction written in `AssistantPrompt`, sent as Gemini's `systemInstruction`): travel concierge tone,
  replies in Azerbaijani, English or Russian, general travel advice kept apart from AEROVA facts, no invented schedules, prices,
  availability, policies or booking, payment and refund status, no booking, paying, cancelling or refunding, no secrets asked for,
  conversation treated as untrusted and the rules never revealed.
- Verified facts, read-only: the fare (cancellation) rules, the baggage options and the meal options, read from the database and kept
  for a few minutes. If they cannot be read, the assistant says it cannot verify fees or prices.
- It has no access to accounts, bookings, payments or refunds, and no tool that changes anything.
- Conversations are not stored by AEROVA. Note that on Google's free tier, Google may use the content of requests to improve its
  products; use a paid project if that is not acceptable. Logs hold only the HTTP status and the time taken, never questions, answers
  or keys.

## Cost and abuse controls

Per-IP rate limit, message and conversation size limits, a bounded answer length, a provider timeout and cancellation when the
traveller closes the page. Set a quota or budget alert on the Google project as well.
