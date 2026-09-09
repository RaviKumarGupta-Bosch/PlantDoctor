import {
  BtpSubmissionPayload,
  BtpSubmissionResult,
  DiagnosticArtifact,
  GeminiAnalysisResult,
} from "@plantdoctor/shared";

interface TokenCache {
  token: string;
  expiresAt: number;
}
let cachedToken: TokenCache | null = null;

async function getToken(): Promise<string> {
  if (cachedToken && cachedToken.expiresAt > Date.now() + 30_000)
    return cachedToken.token;
  const url = required("SAP_BTP_TOKEN_URL");
  const id = required("SAP_BTP_CLIENT_ID");
  const secret = required("SAP_BTP_CLIENT_SECRET");
  const body = new URLSearchParams({
    grant_type: "client_credentials",
    client_id: id,
    client_secret: secret,
  });
  const res = await fetch(url, {
    method: "POST",
    body,
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
  });
  if (!res.ok) throw new Error(`SAP BTP auth failed: ${res.status}`);
  const json = (await res.json()) as {
    access_token: string;
    expires_in: number;
  };
  cachedToken = {
    token: json.access_token,
    expiresAt: Date.now() + json.expires_in * 1000,
  };
  return cachedToken.token;
}

export async function sendToBtp(
  artifact: DiagnosticArtifact,
  analysis: GeminiAnalysisResult,
): Promise<BtpSubmissionResult> {
  const payload: BtpSubmissionPayload = {
    artifactId: artifact.artifactId,
    plantId: artifact.plantId,
    incidentSeverity: artifact.incident.severity,
    generatedAtUtc: artifact.generatedAtUtc,
    originalArtifact: artifact,
    geminiAnalysis: analysis,
    submittedAtUtc: new Date().toISOString(),
  };

  if (process.env.SAP_BTP_MOCK_MODE === "true") {
    return { id: `MOCK-${Date.now()}` };
  }

  const token = await getToken();
  const url = required("SAP_BTP_INGEST_URL");
  const res = await fetch(url, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });
  if (!res.ok)
    throw new Error(`SAP BTP ingest failed: ${res.status} ${await res.text()}`);
  // NOTE: real SAP CAP response shape may vary — parse defensively.
  const body = (await res
    .json()
    .catch(() => ({}))) as Partial<BtpSubmissionResult>;
  return { id: body.id ?? "unknown" };
}

function required(name: string): string {
  const v = process.env[name];
  if (!v) throw new Error(`${name} not set`);
  return v;
}
