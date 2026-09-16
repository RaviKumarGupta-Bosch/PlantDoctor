import { z } from "zod";

export const GeminiAnalysisResultSchema = z.object({
  rootCause: z.string(),
  confidenceScore: z.number().min(0).max(100),
  severityAssessment: z.enum(["Low", "Medium", "High", "Critical"]),
  suggestedFixSummary: z.string(),
  suspectedFileOrModule: z.string().nullable(),
  suspectedLineHint: z.string().nullable(),
  codeRecommendation: z.string().nullable(),
  reproductionSteps: z.array(z.string()),
  testingRecommendations: z.array(z.string()),
  relatedPastIssueNotes: z.string().nullable(),
});

export type GeminiAnalysisResult = z.infer<typeof GeminiAnalysisResultSchema>;
