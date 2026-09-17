# 🚀 PlantDoctor - GCP + GitHub Actions Setup Complete

## ✅ What We've Done

### 1. Workload Identity Federation (WIF) Setup
- ✅ Created Workload Identity Pool: `github-pool`
- ✅ Created Workload Identity Provider for GitHub OIDC: `github-provider`
- ✅ Created Service Account: `plantdoctor-deployer@plantdoctor-codingaura.iam.gserviceaccount.com`
- ✅ Granted all required IAM roles:
  - `roles/run.admin` (deploy to Cloud Run)
  - `roles/aiplatform.user` (use Vertex AI/Claude API)
  - `roles/secretmanager.admin` (manage secrets)
  - `roles/artifactregistry.writer` (push Docker images)
  - `roles/serviceusage.serviceUsageConsumer` (use GCP APIs)
- ✅ Configured GitHub ↔ GCP Trust (principal set)

### 2. Updated GitHub Actions Pipeline
- ✅ Updated `deploy-devportal.yml` with correct project details:
  - Project ID: `plantdoctor-codingaura`
  - Project Number: `655336664371`
  - Region: `asia-south1` (India)
- ✅ Added fallback values for environment variables
- ✅ Simplified IAM role verification

### 3. Created Setup Documentation
- ✅ `GITHUB_SETUP.md` - Step-by-step guide for adding GitHub variables
- ✅ Bootstrap scripts (bash + PowerShell) for infrastructure setup

---

## 📋 Next Steps: Add GitHub Repository Variables

### Go to: https://github.com/RaviKumarGupta-Bosch/PlantDoctor/settings/variables

**Add these 4 required variables:**

```
1. GCP_WORKLOAD_IDENTITY_PROVIDER
   └─ projects/655336664371/locations/global/workloadIdentityPools/github-pool/providers/github-provider

2. GCP_SERVICE_ACCOUNT
   └─ plantdoctor-deployer@plantdoctor-codingaura.iam.gserviceaccount.com

3. PROJECT_ID
   └─ plantdoctor-codingaura

4. REGION
   └─ asia-south1
```

**Optional (recommended):**

```
5. SAP_BTP_MOCK_MODE
   └─ true
```

---

## 🔧 If You Get Billing Error

The APIs need to be manually enabled in Google Cloud Console:

1. Go to: https://console.cloud.google.com/apis/library?project=plantdoctor-codingaura
2. Search and enable:
   - Cloud Run API
   - Artifact Registry API
   - Secret Manager API
   - Vertex AI API

OR run this command:

```bash
gcloud services enable \
  run.googleapis.com \
  artifactregistry.googleapis.com \
  secretmanager.googleapis.com \
  aiplatform.googleapis.com \
  --project=plantdoctor-codingaura
```

---

## 🧪 Test Your Setup

Once you've added the GitHub variables:

```bash
# Push to main to trigger deployment
git push origin main

# OR manually trigger in GitHub UI
# 1. Go to: Actions → "Deploy plantdoctor-devportal to Cloud Run"
# 2. Click "Run workflow"
```

Expected output in workflow logs:
```
✅ Project set to plantdoctor-codingaura
✅ Authenticated to Google Cloud via WIF
✅ Built and pushed image to asia-south1-docker.pkg.dev/...
✅ Deployed to Cloud Run: https://plantdoctor-devportal-XXXXX.run.app
```

---

## 📊 Architecture Overview

```
┌─────────────────┐
│  GitHub Repo    │
│ (Main Branch)   │
└────────┬────────┘
         │
         └─→ GitHub Actions Workflow
            (deploy-devportal.yml)
              ├─ Authenticate via WIF (OIDC token)
              ├─ Build Docker image
              ├─ Push to Artifact Registry
              ├─ Deploy to Cloud Run
              └─ Output service URL

GCP Project: plantdoctor-codingaura (655336664371)
Region: asia-south1

Resources:
├─ Cloud Run Service: plantdoctor-devportal
├─ Artifact Registry: adk-agents/plantdoctor-devportal
├─ Secret Manager: SAP_BTP_CLIENT_SECRET
├─ Service Account: plantdoctor-deployer@...
└─ Workload Identity: github-pool/github-provider
```

---

## 🎯 Project Details

| Detail | Value |
|--------|-------|
| **GitHub Org/Repo** | RaviKumarGupta-Bosch/PlantDoctor |
| **GCP Project ID** | plantdoctor-codingaura |
| **GCP Project Number** | 655336664371 |
| **GCP Region** | asia-south1 (New Delhi) |
| **Service Account** | plantdoctor-deployer@plantdoctor-codingaura.iam.gserviceaccount.com |
| **Artifact Registry** | asia-south1-docker.pkg.dev/plantdoctor-codingaura/adk-agents |
| **Cloud Run Service** | plantdoctor-devportal |

---

## 📝 Files Created/Modified

```
PlantDoctor/
├── .github/workflows/
│   └── deploy-devportal.yml          ✏️ UPDATED - with correct project ID
├── scripts/
│   ├── bootstrap-wif.sh             ✨ NEW - Bash bootstrap script
│   └── bootstrap-wif.ps1            ✨ NEW - PowerShell bootstrap script
├── GITHUB_SETUP.md                   ✨ NEW - Step-by-step setup guide
└── GCP_SETUP_COMPLETE.md             ✨ NEW - This file
```

---

## ✨ What's Next?

1. **TODAY:**
   - [ ] Add 4 GitHub repository variables
   - [ ] Verify all variables are saved
   - [ ] Optionally run the deployment manually

2. **FIRST DEPLOYMENT:**
   - [ ] Code will be built as Docker image
   - [ ] Image pushed to Artifact Registry
   - [ ] Service deployed to Cloud Run
   - [ ] Receive public URL in workflow output

3. **CONFIGURATION:**
   - [ ] Update `SAP_BTP_CLIENT_SECRET` in Secret Manager with real credentials
   - [ ] Configure `DEVPORTAL_ALLOWED_INVOKERS` if needed
   - [ ] Set up custom domain (optional)

---

**Ready to go? Head over to GitHub → Settings → Secrets and variables → Variables and add those 4 variables!** 🚀
