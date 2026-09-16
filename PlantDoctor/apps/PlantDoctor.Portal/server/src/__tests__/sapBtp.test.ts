import { describe, it, expect, beforeEach } from "vitest";
import { sendToBtp } from "../services/sapBtpClient.js";

const artifact = {
  artifactId: "abc",
  generatedAtUtc: "2026-01-01T00:00:00Z",
  plantId: "P1",
  incident: {
    detectedAtUtc: "2026-01-01T00:00:00Z",
    severity: "Error" as const,
    source: "Sensor" as const,
    errorCode: null,
    primaryMessage: "x",
  },
  recentLogEntries: [],
  sensorSnapshot: [],
  comConnectionState: {
    port: "COM1",
    status: "Connected",
    lastEventUtc: "2026-01-01T00:00:00Z",
  },
  aiAnalysis: {
    modelUsed: "m",
    summary: "s",
    suspectedRootCause: "r",
    confidence: "Low" as const,
  },
  operatorChatTranscript: [],
};
const analysis = {
  rootCause: "r",
  confidenceScore: 80,
  severityAssessment: "High" as const,
  suggestedFixSummary: "s",
  suspectedFileOrModule: null,
  suspectedLineHint: null,
  codeRecommendation: null,
  reproductionSteps: [],
  testingRecommendations: [],
  relatedPastIssueNotes: null,
};

describe("sapBtpClient (mock mode)", () => {
  beforeEach(() => {
    process.env.SAP_BTP_MOCK_MODE = "true";
  });
  it("returns a mock id without HTTP", async () => {
    const r = await sendToBtp(artifact, analysis);
    expect(r.id).toMatch(/^MOCK-/);
  });
});
