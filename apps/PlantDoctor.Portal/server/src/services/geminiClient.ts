import { GoogleGenerativeAI } from "@google/generative-ai";
import {
  DiagnosticArtifact,
  GeminiAnalysisResult,
  GeminiAnalysisResultSchema,
} from "@plantdoctor/shared";

const SYSTEM = `You are an expert industrial software diagnostics engineer.
Given a diagnostic artifact from an offline plant agent, identify the most likely root cause,
suggest a fix, and describe reproduction and testing steps.
Respond ONLY with valid JSON matching the provided schema. No prose.`;

function buildPrompt(a: DiagnosticArtifact): string {
  return `${SYSTEM}

JSON SCHEMA (respond with an object of this shape):
{
  "rootCause": string,
  "confidenceScore": number (0-100),
  "severityAssessment": "Low"|"Medium"|"High"|"Critical",
  "suggestedFixSummary": string,
  "suspectedFileOrModule": string|null,
  "suspectedLineHint": string|null,
  "codeRecommendation": string|null,
  "reproductionSteps": string[],
  "testingRecommendations": string[],
  "relatedPastIssueNotes": string|null
}

ARTIFACT:
${JSON.stringify(a, null, 2)}`;
}

export async function analyzeWithGemini(
  a: DiagnosticArtifact,
): Promise<GeminiAnalysisResult> {
  const key = process.env.GEMINI_API_KEY;
  if (!key) throw new Error("GEMINI_API_KEY not set");
  const modelName = process.env.GEMINI_MODEL ?? "gemini-2.0-flash";
  const client = new GoogleGenerativeAI(key);
  // NOTE: verify getGenerativeModel() call shape against installed SDK version.
  const model = client.getGenerativeModel({
    model: modelName,
    generationConfig: { responseMimeType: "application/json" },
  });

  const runOnce = async (extraInstruction = "") => {
    const res = await model.generateContent(buildPrompt(a) + extraInstruction);
    return res.response.text();
  };

  let text = await runOnce();
  try {
    return GeminiAnalysisResultSchema.parse(JSON.parse(text));
  } catch {
    text = await runOnce(
      "\n\nSTRICT: Return ONLY valid JSON matching the schema exactly, no markdown fences.",
    );
    return GeminiAnalysisResultSchema.parse(JSON.parse(text));
  }
}
