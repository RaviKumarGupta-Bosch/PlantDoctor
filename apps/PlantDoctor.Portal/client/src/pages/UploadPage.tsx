import { useState } from "react";
import type { GeminiAnalysisResult } from "@plantdoctor/shared";
import { DiagnosticArtifactSchema } from "@plantdoctor/shared";
import { analyze } from "../api.js";

interface Props {
  onAnalyzed: (r: { artifactId: string; analysis: GeminiAnalysisResult }) => void;
}

export function UploadPage({ onAnalyzed }: Props) {
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handle(file: File) {
    setError(null); setBusy(true);
    try {
      if (file.name.endsWith(".json")) {
        const text = await file.text();
        DiagnosticArtifactSchema.parse(JSON.parse(text));
      }
      const r = await analyze(file);
      onAnalyzed(r);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Invalid artifact");
    } finally { setBusy(false); }
  }

  return (
    <div className="bg-white rounded-lg shadow p-8">
      <h2 className="text-lg font-semibold mb-4">Upload diagnostic artifact</h2>
      <label
        className="block border-2 border-dashed border-slate-300 rounded-lg p-10 text-center cursor-pointer hover:bg-slate-50"
        onDragOver={e => e.preventDefault()}
        onDrop={e => { e.preventDefault(); const f = e.dataTransfer.files[0]; if (f) void handle(f); }}
      >
        <input
          type="file" accept=".json,.zip" className="hidden"
          onChange={e => e.target.files?.[0] && handle(e.target.files[0])}
        />
        <p>Drag & drop a <code>.json</code> or <code>.zip</code> artifact, or click to browse.</p>
      </label>
      {busy && <p className="mt-4 text-blue-600">Analyzing with Gemini...</p>}
      {error && <p role="alert" className="mt-4 text-red-600">{error}</p>}
    </div>
  );
}
