import { z } from "zod";

// Mirrors docs/contracts/diagnostic-artifact.schema.json — single source of truth for App 2 ↔ App 3.

export const IncidentSchema = z.object({
  detectedAtUtc: z.string(),
  severity: z.enum(["Warning", "Error", "Critical"]),
  source: z.enum(["Sensor", "COM", "Application"]),
  errorCode: z.string().nullable(),
  primaryMessage: z.string(),
});

export const LogEntrySchema = z.object({
  timestamp: z.string(),
  level: z.string(),
  source: z.string(),
  sensorName: z.string().optional(),
  value: z.string().optional(),
  errorCode: z.string().optional(),
  message: z.string(),
  stackTrace: z.string().optional(),
});

export const SensorSchema = z.object({
  name: z.string(),
  value: z.number(),
  unit: z.string(),
  status: z.string(),
});

export const ComStateSchema = z.object({
  port: z.string(),
  status: z.string(),
  lastEventUtc: z.string(),
});

export const AiAnalysisSchema = z.object({
  modelUsed: z.string(),
  summary: z.string(),
  suspectedRootCause: z.string(),
  confidence: z.enum(["Low", "Medium", "High"]),
});

export const ChatMessageSchema = z.object({
  role: z.enum(["operator", "assistant"]),
  message: z.string(),
  timestampUtc: z.string(),
});

export const DiagnosticArtifactSchema = z.object({
  artifactId: z.string(),
  generatedAtUtc: z.string(),
  plantId: z.string(),
  incident: IncidentSchema,
  recentLogEntries: z.array(LogEntrySchema),
  sensorSnapshot: z.array(SensorSchema),
  comConnectionState: ComStateSchema,
  aiAnalysis: AiAnalysisSchema,
  operatorChatTranscript: z.array(ChatMessageSchema),
});

export type DiagnosticArtifact = z.infer<typeof DiagnosticArtifactSchema>;
