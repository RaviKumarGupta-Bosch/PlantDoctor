import { useState } from "react";
import { Prism as SyntaxHighlighter } from "react-syntax-highlighter";
import type { GeminiAnalysisResult } from "@plantdoctor/shared";
import { sendToBtp } from "../api.js";

interface Props {
  artifactId: string;
  analysis: GeminiAnalysisResult;
  onReset: () => void;
}

export function ResultsPage({ artifactId, analysis, onReset }: Props) {
  const [btpId, setBtpId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit() {
    setError(null); setBusy(true);
    try { setBtpId((await sendToBtp(artifactId)).id); }
    catch (e) { setError(e instanceof Error ? e.message : "Send failed"); }
    finally { setBusy(false); }
  }

  return (
    <div className="space-y-4">
      <button className="text-sm text-slate-500 underline" onClick={onReset}>← Upload another</button>

      <section className="bg-white rounded-lg shadow p-6">
        <div className="flex items-center justify-between">
          <span className="inline-block bg-red-600 text-white text-xs px-2 py-1 rounded">{analysis.severityAssessment}</span>
          <span className="text-sm text-slate-500">Confidence {analysis.confidenceScore}%</span>
        </div>
        <h2 className="text-2xl font-bold mt-2">{analysis.rootCause}</h2>
        <p className="text-slate-700 mt-2">{analysis.suggestedFixSummary}</p>
      </section>

      {analysis.codeRecommendation && (
        <section className="bg-white rounded-lg shadow p-6">
          <h3 className="font-semibold mb-2">Suggested Fix</h3>
          <SyntaxHighlighter language="csharp">{analysis.codeRecommendation}</SyntaxHighlighter>
        </section>
      )}

      <section className="bg-white rounded-lg shadow p-6">
        <h3 className="font-semibold mb-2">Reproduction Steps</h3>
        <ol className="list-decimal ml-6">{analysis.reproductionSteps.map((s, i) => <li key={i}>{s}</li>)}</ol>
      </section>

      <section className="bg-white rounded-lg shadow p-6">
        <h3 className="font-semibold mb-2">Testing Recommendations</h3>
        <ul className="list-disc ml-6">{analysis.testingRecommendations.map((s, i) => <li key={i}>{s}</li>)}</ul>
      </section>

      <section className="bg-white rounded-lg shadow p-6">
        <button
          className="bg-slate-900 text-white px-4 py-2 rounded disabled:opacity-50"
          disabled={busy || !!btpId} onClick={submit}
        >
          {busy ? "Sending..." : btpId ? `Sent — record ${btpId}` : "Send to SAP BTP"}
        </button>
        {error && <p role="alert" className="mt-2 text-red-600">{error}</p>}
      </section>
    </div>
  );
}
