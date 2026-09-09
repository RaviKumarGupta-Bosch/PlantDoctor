import { DiagnosticArtifact, GeminiAnalysisResult } from "@plantdoctor/shared";

interface StoredRecord {
  artifact: DiagnosticArtifact;
  analysis: GeminiAnalysisResult;
}

// In-memory store for hackathon MVP. Swap for better-sqlite3 for durability.
class ArtifactStore {
  private readonly map = new Map<string, StoredRecord>();
  set(id: string, r: StoredRecord) {
    this.map.set(id, r);
  }
  get(id: string) {
    return this.map.get(id);
  }
}

export const artifactStore = new ArtifactStore();
