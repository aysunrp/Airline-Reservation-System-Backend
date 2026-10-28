# Ask AEROVA (travel assistant) setup

Ask AEROVA is the chat on every page of the site. The browser never talks to the AI provider: it calls the AEROVA API, the API calls
the provider with a key only the server knows.

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
| 503 | The assistant is not set up (no key) or the provider failed or is busy |
| 504 | The provider did not answer in time |

## Settings (not secret, `appsettings.json`, section `TravelAssistant`)

| Setting | Default | Meaning |
| --- | --- | --- |
| `Provider` | `Anthropic` | Provider that answers (built in: Anthropic; others can be added behind `IAssistantProvider`) |
| `Model` | `claude-haiku-5-5` | Model that answers |
| `Endpoint` | `https://api.anthropic.com/v1/messages` | Provider endpoint |
| `TimeoutSeconds` | `30` | Time given to the provider before the request returns 504 |
| `MaxOutputTokens` | `700` | Longest answer (also bounds the cost of one question) |
| `MaxMessageLength` | `2000` | Characters in one message |
| `MaxMessages` | `20` | Messages in one request |
| `MaxTotalLength` | `12000` | Characters in all messages of one request |
| `RateLimitPerMinute` | `10` | Questions per minute and IP address |

CORS: the allowed frontend origins are the existing `Cors:AllowedOrigins` list (no extra origin is opened for the chat).

## The key (secret)

The provider key is `TravelAssistant:ApiKey`. It is never put in `appsettings.json`, the frontend, Git, the log or a response.
Without it the API runs and the chat answers `503` ("The assistant is not available right now").

Local development (from the API project folder):

```bash
dotnet user-secrets set "TravelAssistant:ApiKey" "<your-provider-api-key>" --project AirlineReservationSystem
```

Production: set the environment variable (or the secret store of the host)

```bash
TravelAssistant__ApiKey=<your-provider-api-key>
```

`<your-provider-api-key>` is a placeholder: use a key created in the provider's console, with a monthly spending limit.

## Checking that it works, and finding out why it does not

Run this from the API project folder (it uses the same settings as the API, asks the provider one tiny question and never prints the key):

```bash
dotnet run --project AirlineReservationSystem -- check-assistant
```

Exit code 0 means Ask AEROVA works. Otherwise it says which step failed. The API log says the same on every failed question and once at
start-up ("Ask AEROVA is not set up..." or "provider ..., key present"), and the 503 carries a `code`:

| `code` | What to do |
| --- | --- |
| `assistant_not_configured` | Set `TravelAssistant:ApiKey` (user-secrets or `TravelAssistant__ApiKey`), then restart the API |
| `assistant_key_malformed` | The stored key is not a key (it has letters like Ç, spaces or quotes): usually the placeholder text of an instruction was saved instead of the real key. Set the real key and restart |
| `assistant_key_rejected` | The key is wrong, expired, revoked or has no API access: create a new one |
| `assistant_model_unavailable` | Check `TravelAssistant:Model` and `:Endpoint`, and that the key's account may use that model |
| `assistant_billing` | The provider account has no credit or billing: add credit in the provider's console |
| `assistant_busy` | The provider is overloaded or its rate limit is reached: try again later |
| `assistant_request_rejected` | Check `Model`, `MaxOutputTokens` and `ApiVersion` |
| `assistant_provider_error` | The provider failed on its side: try again later |
| `assistant_unreachable` | No internet, a firewall, a proxy or a wrong `Endpoint` |
| `assistant_bad_answer` | The provider answered without usable text: check `Model` and `Endpoint` |

The log holds the code, the HTTP status and the provider's error kind (for example `authentication_error`), never the key, the question or
the provider's own message. User-secrets are only read when the API starts: restart it after setting the key.

## What the assistant knows and what it never does

- Server-side rules (a system instruction written in `AssistantPrompt`): travel concierge tone, replies in Azerbaijani, English or
  Russian, general travel advice kept apart from AEROVA facts, no invented schedules, prices, availability, policies or booking,
  payment and refund status, no booking, paying, cancelling or refunding, no secrets asked for, conversation treated as untrusted and the
  rules never revealed.
- Verified facts, read-only: the fare (cancellation) rules, the baggage options and the meal options, read from the database and kept
  for a few minutes. If they cannot be read, the assistant says it cannot verify fees or prices.
- It has no access to accounts, bookings, payments or refunds, and no tool that changes anything.
- Conversations are not stored. Logs hold only the HTTP status and the time taken, never questions, answers or keys.

## Cost and abuse controls

Per-IP rate limit, message and conversation size limits, a bounded answer length, a provider timeout and cancellation when the
traveller closes the page. Set a spending limit at the provider as well.
