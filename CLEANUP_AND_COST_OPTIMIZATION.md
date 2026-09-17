# PlantDoctor Cleanup & Cost Optimization Guide

## Overview

This guide explains how to use the **Cleanup PlantDoctor Resources** workflow to reduce GCP billing when the service is not in active use, while maintaining the infrastructure for quick redeployment.

## The Problem

Running Cloud Run services incurs charges for:
- **Compute resources** (vCPU, memory, even at idle)
- **Service invocation** (per request)
- **Storage** (old Docker images in Artifact Registry)

Over time, unused services can rack up unexpected bills.

## The Solution

**Cleanup Workflow**: Removes billable resources while keeping infrastructure intact for quick redeployment.

## What Gets Cleaned Up

### 🔴 Billable Resources REMOVED:
| Resource | Cost | Action |
|----------|------|--------|
| Cloud Run Service | ~$0.00002+ per vCPU-second | ❌ **DELETED** |
| Idle Compute | Continuous charge | ✅ Stopped |
| Old Docker Images | $0.10/GB/month | 🧹 Cleaned (keep latest 5) |

### 🟢 Non-Billable Infrastructure PRESERVED:
| Resource | Cost | Action |
|----------|------|--------|
| Workload Identity Pool | FREE | ✅ **KEPT** |
| Service Account | FREE | ✅ **KEPT** |
| IAM Bindings | FREE | ✅ **KEPT** |
| Artifact Registry Repo | FREE (when empty) | ✅ **KEPT** |
| GitHub Actions Secrets | FREE | ✅ **KEPT** |

## Cleanup Levels

The workflow supports different cleanup intensities:

### Level 1: Service Only
```
Action: Delete Cloud Run service
Result: Removes ~80% of ongoing costs
Redeploy time: 2-3 minutes
Loss: None (code & secrets preserved)
```

**Use when:** Taking a short break, expecting to restart soon

### Level 2: Service + Old Images
```
Action: Delete Cloud Run + remove old Docker images (keep latest 5)
Result: Removes ~90% of costs
Redeploy time: 2-3 minutes (rebuilds image from latest)
Loss: Old image history (latest kept)
```

**Use when:** Taking a medium break, no need for image history

### Level 3: Full Cleanup
```
Action: Delete everything billable except infrastructure
Result: Removes ~95% of costs
Redeploy time: 2-3 minutes
Loss: None (complete rebuild possible)
```

**Use when:** Taking extended break, project paused

## How to Run Cleanup

### Option 1: Manual Cleanup
1. Go to GitHub repository → **Actions** tab
2. Select **"Cleanup PlantDoctor Resources (Cost Optimization)"** workflow
3. Click **"Run workflow"**
4. Choose cleanup level:
   - `service-only` (fastest, keeps images)
   - `service-and-images` (moderate, cleans old images)
   - `full` (comprehensive)
5. Click **"Run workflow"**
6. Wait for completion (~2-5 minutes)

### Option 2: Scheduled Cleanup (Optional)
Enable automatic cleanup by uncommenting the schedule in the workflow:

```yaml
schedule:
  # Run cleanup every Sunday at 2 AM UTC
  - cron: '0 2 * * 0'
```

This will automatically clean up resources on a schedule if you forget.

## Cost Savings

### Before Cleanup
```
Cloud Run compute:     $5-20/month (depending on invocations)
Storage (images):      $0.50-2/month
Total:                 ~$5.50-22/month
```

### After Cleanup
```
Workload Identity:     $0 (first 1M operations free)
Service Account:       $0
IAM operations:        $0
Total:                 ~$0/month ✅
```

**Potential savings: 95-100% reduction in running costs**

## Redeploy After Cleanup

When you're ready to use PlantDoctor again:

### Option 1: GitHub Actions
1. Go to **Actions** tab
2. Select **"Deploy plantdoctor-devportal to Cloud Run"**
3. Click **"Run workflow"**
4. Wait for completion (~3-5 minutes)

### Option 2: Command Line
```bash
gh workflow run deploy-devportal.yml -R RaviKumarGupta-Bosch/PlantDoctor
```

### Result
✅ Service redeploys in 2-3 minutes
✅ All code and configuration preserved
✅ No data loss
✅ Public URL remains the same

## FAQ

### Q: Will I lose any data?
**A:** No. Code, secrets, and infrastructure remain intact. Only the running service is removed.

### Q: How long does cleanup take?
**A:** 2-5 minutes depending on number of images to delete.

### Q: How long to redeploy?
**A:** 2-3 minutes (Docker build + Cloud Run deployment).

### Q: Is there a way to automate this?
**A:** Yes. Uncomment the `schedule` section in `cleanup-devportal.yml` for automatic weekly cleanup.

### Q: What if I accidentally run cleanup?
**A:** Just run the deploy workflow to redeploy. No harm done.

### Q: Does cleanup affect my SAP BTP integration?
**A:** No. The integration configuration is preserved. Just redeploy when ready.

### Q: Can I cleanup specific resources only?
**A:** Yes. Use the `cleanup_level` input:
- `service-only` - Just remove the service
- `service-and-images` - Service + old images
- `full` - Everything

### Q: What about GitHub Actions secrets?
**A:** They are preserved. You can redeploy anytime without re-entering secrets.

## Billing Dashboard

Monitor your savings in GCP Console:
1. Go to **Billing** → **Billing Accounts**
2. Select your project
3. View **Usage by service** → Cloud Run
4. Confirm compute charges reduced to $0 after cleanup

## Recommended Strategy

### For Active Development
- Keep running (redeploy when making changes)
- Cost: $5-20/month

### For Staging/Testing
- Deploy before testing
- Cleanup after testing
- Cost: ~$1-5/month

### For Production + Dev
- Keep production running
- Cleanup dev when not in use
- Cost: $10-30/month (prod only)

## Infrastructure Diagram

```
┌──────────────────────────────────────────┐
│   GitHub Actions Workflows               │
├──────────────────────────────────────────┤
│                                          │
│  Deploy                  Cleanup         │
│   ├─ Build image          ├─ Delete      │
│   ├─ Push to registry     │  Cloud Run   │
│   ├─ Deploy to Cloud Run  ├─ Clean old   │
│   └─ Grant public access  │  images      │
│                           └─ Preserve    │
│                              infrastructure
│
└──────────────────────────────────────────┘
                    │
                    ↓
┌──────────────────────────────────────────┐
│   GCP Resources                          │
├──────────────────────────────────────────┤
│                                          │
│  BILLABLE (removed)       NON-BILLABLE   │
│  ✅ Cloud Run             (preserved)    │
│  ✅ Images (old)          ✅ WIF Pool    │
│                           ✅ Service Act │
│  KEPT                     ✅ IAM bindings│
│  ✅ Latest 5 images       ✅ Repository  │
│  ✅ Artifact Registry     ✅ Secrets     │
│                                          │
└──────────────────────────────────────────┘
```

## Support

For issues with cleanup:
- Check workflow logs in **Actions** tab
- Verify GCP credentials are valid
- Ensure `GCP_SERVICE_ACCOUNT` has necessary permissions
- For persistent issues, contact your GCP admin

---

**Last Updated:** 2026-09-17
**Workflow File:** `.github/workflows/cleanup-devportal.yml`
