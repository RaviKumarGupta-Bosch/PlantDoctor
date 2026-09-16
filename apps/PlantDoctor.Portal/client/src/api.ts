import type {
  GeminiAnalysisResult,
  BtpSubmissionResult,
} from "@plantdoctor/shared";

export async function analyze(
  file: File,
): Promise<{ artifactId: string; analysis: GeminiAnalysisResult }> {
  const fd = new FormData();
  fd.append("file", file);
  const res = await fetch("/api/artifacts/analyze", {
    method: "POST",
    body: fd,
  });
  if (!res.ok)
    throw new Error(
      (await res.json().catch(() => ({}))).error ?? `HTTP ${res.status}`,
    );
  return res.json();
}

export async function sendToBtp(
  artifactId: string,
): Promise<BtpSubmissionResult> {
  const res = await fetch(
    `/api/artifacts/${encodeURIComponent(artifactId)}/send-to-btp`,
    { method: "POST" },
  );
  if (!res.ok)
    throw new Error(
      (await res.json().catch(() => ({}))).error ?? `HTTP ${res.status}`,
    );
  return res.json();
}
