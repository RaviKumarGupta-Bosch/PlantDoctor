import { Router } from "express";
import multer from "multer";
import AdmZip from "adm-zip";
import {
  DiagnosticArtifactSchema,
  DiagnosticArtifact,
} from "@plantdoctor/shared";
import { analyzeWithGemini } from "../services/geminiClient.js";
import { sendToBtp } from "../services/sapBtpClient.js";
import { artifactStore } from "../services/artifactStore.js";

const upload = multer({
  storage: multer.memoryStorage(),
  limits: { fileSize: 10 * 1024 * 1024 },
  fileFilter: (_req, file, cb) => {
    const ok =
      file.mimetype === "application/json" ||
      file.mimetype === "application/zip" ||
      file.originalname.endsWith(".json") ||
      file.originalname.endsWith(".zip");
    cb(ok ? null : new Error("Only .json or .zip allowed"), ok);
  },
});

export const artifactsRouter = Router();

artifactsRouter.post(
  "/analyze",
  upload.single("file"),
  async (req, res, next) => {
    try {
      if (!req.file) return res.status(400).json({ error: "MissingFile" });
      const raw = extractJson(req.file);
      const parsed: DiagnosticArtifact = DiagnosticArtifactSchema.parse(
        JSON.parse(raw),
      );
      const analysis = await analyzeWithGemini(parsed);
      artifactStore.set(parsed.artifactId, { artifact: parsed, analysis });
      res.json({ artifactId: parsed.artifactId, analysis });
    } catch (e) {
      next(e);
    }
  },
);

artifactsRouter.post("/:artifactId/send-to-btp", async (req, res, next) => {
  try {
    const record = artifactStore.get(req.params.artifactId);
    if (!record) return res.status(404).json({ error: "NotFound" });
    const result = await sendToBtp(record.artifact, record.analysis);
    res.json(result);
  } catch (e) {
    next(e);
  }
});

function extractJson(file: Express.Multer.File): string {
  if (file.originalname.endsWith(".zip")) {
    const zip = new AdmZip(file.buffer);
    const entry = zip.getEntries().find((e) => e.entryName.endsWith(".json"));
    if (!entry) throw new Error("No .json inside .zip");
    return entry.getData().toString("utf8");
  }
  return file.buffer.toString("utf8");
}
