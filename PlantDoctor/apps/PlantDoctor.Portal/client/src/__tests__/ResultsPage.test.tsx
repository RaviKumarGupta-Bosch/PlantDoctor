import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ResultsPage } from "../pages/ResultsPage.js";
import type { GeminiAnalysisResult } from "@plantdoctor/shared";

const fixture: GeminiAnalysisResult = {
  rootCause: "Vibration spike",
  confidenceScore: 87,
  severityAssessment: "High",
  suggestedFixSummary: "Rebalance rotor",
  suspectedFileOrModule: null,
  suspectedLineHint: null,
  codeRecommendation: null,
  reproductionSteps: ["Start plant", "Wait 5 min"],
  testingRecommendations: ["Check vibration threshold"],
  relatedPastIssueNotes: null,
};

describe("ResultsPage", () => {
  it("renders headline and confidence", () => {
    render(<ResultsPage artifactId="x" analysis={fixture} onReset={() => {}} />);
    expect(screen.getByText("Vibration spike")).toBeTruthy();
    expect(screen.getByText(/87%/)).toBeTruthy();
  });
});
