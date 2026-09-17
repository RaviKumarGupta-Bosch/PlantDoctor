# PlantDoctor

PlantDoctor is an AI-powered industrial diagnostics platform that monitors plant logs, automatically detects issues, explains root causes using AI, and delivers actionable diagnostics from edge systems to enterprise insights.

## Repository layout

```
PlantDoctor/
├── apps/
│   ├── PlantSimulator/          # App 1 — WPF + WCF (.NET 8), fully offline
│   ├── PlantDoctor.Agent/       # App 2 — WPF + Ollama (.NET 8), offline AI
│   └── PlantDoctor.Portal/      # App 3 — React + Node/Express, online (Gemini + SAP BTP)
├── docs/
│   ├── architecture.md
│   └── contracts/
│       └── diagnostic-artifact.schema.json   # Single source of truth for App 2 ↔ App 3
└── README.md
```

## Data flow

```
[PlantSimulator] --JSONL logs--> [PlantDoctor.Agent] --artifact.json--> [PlantDoctor.Portal] --> Gemini --> SAP BTP
     offline                          offline (Ollama)        file transfer         online
```

## Conventions (all apps)

- Enterprise layering: UI → Application/Services → Domain/Core → Infrastructure.
- DI: `Microsoft.Extensions.DependencyInjection` (.NET); explicit composition root (Node).
- MVVM in WPF via `CommunityToolkit.Mvvm`.
- Contracts first: `docs/contracts/diagnostic-artifact.schema.json` is the shared contract.
- No secrets in source. `.env` (Portal), `appsettings.json` (.NET apps).
- Tests per app: `dotnet test` / `npm test`.

See each app's `README.md` for run instructions.

## Deploying to Cloud Run

### Target GCP project

`deploy-devportal.yml` deploys `plantdoctor-devportal` to its own dedicated Google Cloud project:

| Field          | Value                    |
| -------------- | ------------------------ |
| Project name   | `PlantDoctor-TeamCodingAura` |
| Project number | `305630320430`               |
| Project ID     | `plantdoctor-teamcodingaura` |

This is a standalone project (separate from wherever the existing RAG-agent deployment runs), so it needs its own Workload Identity Pool, OIDC provider, service account, and Artifact Registry repo — see [Bootstrapping WIF for a new project](#bootstrapping-wif-for-a-new-project) below. Once bootstrapped, set these as GitHub Actions **repository variables** (Settings → Secrets and variables → Actions → Variables) so the workflow picks them up via `vars.*`:

| Variable                          | Value                                                                                     |
| ---------------------------------- | ------------------------------------------------------------------------------------------ |
| `PROJECT_ID`                       | `plantdoctor-teamcodingaura`                                                               |
| `REGION`                           | e.g. `us-central1` (whichever region you bootstrap into)                                   |
| `GCP_WORKLOAD_IDENTITY_PROVIDER`   | printed by `bootstrap-gcp.sh`: `projects/305630320430/locations/global/workloadIdentityPools/github-actions-pool/providers/github-actions-provider` |
| `GCP_SERVICE_ACCOUNT`              | printed by `bootstrap-gcp.sh`: `plantdoctor-devportal-deployer@plantdoctor-teamcodingaura.iam.gserviceaccount.com` |
| `DEVPORTAL_ALLOWED_INVOKERS`       | comma-separated list of `user:`/`group:` members to grant `roles/run.invoker` (optional)   |
| `SAP_BTP_MOCK_MODE`                | `true`/`false` toggle passed through as a plain env var                                    |

### Bootstrapping WIF for a new project

`plantdoctor-teamcodingaura` is a **new** GCP project, so it needs its own Workload Identity Pool, OIDC provider, and service account before the workflow can authenticate. This repository's Actions run on GitHub Enterprise Server (`github.boschdevcloud.com`), not public github.com, so the WIF provider must trust that instance's own OIDC token issuer rather than `token.actions.githubusercontent.com`.

Run the bootstrap script once, from a machine authenticated with an account that can manage IAM/services on the target project:

```bash
PROJECT_ID=plantdoctor-teamcodingaura \
PROJECT_NUMBER=305630320430 \
REGION=us-central1 \
GH_HOST=github.boschdevcloud.com \
GH_REPO=PLT3KOR/PlantDoctor \
  bash plantdoctor-devportal/scripts/bootstrap-gcp.sh
```

This creates, idempotently:

- A dedicated Workload Identity Pool + OIDC provider scoped to this exact repo (`GH_REPO`), so no other repo on the GHES instance can impersonate the resulting service account
- A single service account (`plantdoctor-devportal-deployer@plantdoctor-teamcodingaura.iam.gserviceaccount.com`) used both by CI to deploy and by Cloud Run at runtime, with `roles/run.admin`, `roles/artifactregistry.writer`, `roles/iam.serviceAccountUser`, `roles/aiplatform.user`, and `roles/secretmanager.admin`
- The shared `adk-agents` Artifact Registry repo in the chosen region

At the end it prints the exact values to paste into the repo's GitHub Actions **variables** (`PROJECT_ID`, `REGION`, `GCP_WORKLOAD_IDENTITY_PROVIDER`, `GCP_SERVICE_ACCOUNT`). **No GitHub secret is required for GCP authentication** — that's the point of WIF over static service-account keys. The only credential-like value the app needs (`SAP_BTP_CLIENT_SECRET`) lives in GCP Secret Manager instead, as described below.

⚠️ The script's `--issuer-uri="https://${GH_HOST}/_services/token"` reflects the standard GitHub Enterprise Server Actions OIDC issuer path. Confirm this against your GHES version's documentation before running, since it was written without live internet access to verify the current docs.

If this app is ever moved to share a project/service account with another existing deployment instead of running standalone, the workflow already supports that: it simply reads `vars.GCP_WORKLOAD_IDENTITY_PROVIDER` and `vars.GCP_SERVICE_ACCOUNT`, so pointing those repo variables at an already-bootstrapped identity is enough — no workflow changes needed.

The shared Cloud Run service account must also be allowed to call Vertex AI and Secret Manager. In addition to the permissions granted by the bootstrap script, confirm the following role(s) are present (or grant them manually if managing IAM outside the script):

```bash
gcloud projects add-iam-policy-binding "${PROJECT_ID}" \
  --member="serviceAccount:${GCP_SERVICE_ACCOUNT}" \
  --role="roles/aiplatform.user" \
  --condition=None

gcloud projects add-iam-policy-binding "${PROJECT_ID}" \
  --member="serviceAccount:${GCP_SERVICE_ACCOUNT}" \
  --role="roles/secretmanager.admin" \
  --condition=None
```

`roles/secretmanager.admin` (rather than just `secretAccessor`) is required because the same service account both creates/updates `SAP_BTP_CLIENT_SECRET` during CI and reads it at Cloud Run runtime — `secretAccessor` alone cannot create secrets or add versions.

The Cloud Run service is intentionally private by default (`--no-allow-unauthenticated`). To allow a developer or operator to open the URL, grant `roles/run.invoker` to their identity or group:

```bash
gcloud run services add-iam-policy-binding "plantdoctor-devportal" \
  --project="${PROJECT_ID}" \
  --region="${REGION}" \
  --member="user:developer@example.com" \
  --role="roles/run.invoker"
```

For groups, replace `user:` with `group:` and pass the group email. If you want the service public for a demo, switch the workflow to `--allow-unauthenticated` and confirm that this is the intended access model.

`SAP_BTP_CLIENT_SECRET` is managed in Google Secret Manager rather than injected as a plaintext env var in the GitHub Actions workflow. The deployment workflow creates the secret if it does not already exist and stores a placeholder version until the real secret is added manually. Do not store the real SAP secret in `deploy-devportal.yml` or in logs. After the first deployment, update the secret with:

```bash
gcloud secrets versions add "SAP_BTP_CLIENT_SECRET" \
  --project="${PROJECT_ID}" \
  --data-file=-
```

Then paste the real secret value into the prompt or use an approved secure pipeline step. `SAP_BTP_MOCK_MODE` remains a plain environment flag and is deliberately kept in the workflow because it is a safe toggle, not a secret.

