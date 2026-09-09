import { describe, it, expect } from "vitest";
import { DiagnosticArtifactSchema } from "@plantdoctor/shared";

const validArtifact = {
  artifactId: "11111111-1111-1111-1111-111111111111",
  generatedAtUtc: "2026-01-01T00:00:00Z",
  plantId: "P1",
  incident: {
    detectedAtUtc: "2026-01-01T00:00:00Z",
    severity: "Error",
    source: "Sensor",
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
    confidence: "Low",
  },
  operatorChatTranscript: [],
};

describe("DiagnosticArtifactSchema", () => {
  it("accepts a valid artifact", () => {
    expect(() => DiagnosticArtifactSchema.parse(validArtifact)).not.toThrow();
  });
  it("rejects unknown severity", () => {
    const bad = {
      ...validArtifact,
      incident: { ...validArtifact.incident, severity: "Fatal" },
    };
    expect(() => DiagnosticArtifactSchema.parse(bad)).toThrow();
  });
});
