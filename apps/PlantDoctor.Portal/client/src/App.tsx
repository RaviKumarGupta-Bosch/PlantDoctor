import { useState } from "react";
import type { GeminiAnalysisResult } from "@plantdoctor/shared";
import { UploadPage } from "./pages/UploadPage.js";
import { ResultsPage } from "./pages/ResultsPage.js";

export function App() {
  const [result, setResult] = useState<{ artifactId: string; analysis: GeminiAnalysisResult } | null>(null);
  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <header className="bg-slate-900 text-white p-4 shadow">
        <h1 className="text-xl font-bold">PlantDoctor Portal</h1>
      </header>
      <main className="max-w-5xl mx-auto p-6">
        {result
          ? <ResultsPage {...result} onReset={() => setResult(null)} />
          : <UploadPage onAnalyzed={setResult} />}
      </main>
    </div>
  );
}
