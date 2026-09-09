# PlantDoctor.Portal (App 3)

Online web portal. Upload a `DiagnosticArtifact` JSON → analyze with Gemini → push result to SAP BTP.

## Structure

```
apps/PlantDoctor.Portal/
├── client/       React + TS + Vite + Tailwind
├── server/       Express + TS (Gemini + SAP BTP clients)
└── shared/       zod-validated cross-boundary types (@plantdoctor/shared)
```

## Setup

```powershell
npm install
cp server\.env.example server\.env  # then fill in secrets
npm run dev                          # runs server (:5000) + client (:5173)
```

## Environment (`server/.env`)

| Var                     | Purpose                                     |
| ----------------------- | ------------------------------------------- |
| `GEMINI_API_KEY`        | Google Generative AI key (server-only)      |
| `GEMINI_MODEL`          | e.g. `gemini-2.0-flash`                     |
| `SAP_BTP_TOKEN_URL`     | XSUAA OAuth2 token endpoint                 |
| `SAP_BTP_CLIENT_ID`     | client credentials                          |
| `SAP_BTP_CLIENT_SECRET` | client credentials                          |
| `SAP_BTP_INGEST_URL`    | Destination CAP/iFlow endpoint              |
| `SAP_BTP_MOCK_MODE`     | `true` to skip real SAP calls (demo mode)   |
| `PORT`                  | server port (default 5000)                  |
| `CLIENT_ORIGIN`         | CORS origin (default `http://localhost:5173`) |

Secrets **never** leave the server. Client only calls `/api/*`.

## Contracts

- `DiagnosticArtifact` — input, from App 2. Defined in `shared/src/artifact.ts` and validated via zod on both client and server.
- `GeminiAnalysisResult` — Gemini response schema, validated server-side with retry on malformed JSON.

## Architecture

```
[Browser] --file--> [Express /api/artifacts/analyze]
                     └─ zod validate ─ Gemini SDK ─ zod validate ─┐
                                                                  ▼
[Browser] <---- results JSON --------------------------- in-memory store
[Browser] --click Send--> [/api/artifacts/:id/send-to-btp]
                                └─ OAuth2 (cached token) ─ POST ─ SAP BTP
```

## Tests

```powershell
npm test
```
