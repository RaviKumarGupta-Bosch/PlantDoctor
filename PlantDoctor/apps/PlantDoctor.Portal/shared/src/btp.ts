import { DiagnosticArtifact } from "./artifact.js";
import { GeminiAnalysisResult } from "./gemini.js";

export interface BtpSubmissionPayload {
  artifactId: string;
  plantId: string;
  incidentSeverity: string;
  generatedAtUtc: string;
  originalArtifact: DiagnosticArtifact;
  geminiAnalysis: GeminiAnalysisResult;
  submittedAtUtc: string;
}

export interface BtpSubmissionResult {
  id: string;
}
