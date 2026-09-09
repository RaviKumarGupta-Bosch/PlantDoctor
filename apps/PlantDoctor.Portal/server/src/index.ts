import "dotenv/config";
import express from "express";
import cors from "cors";
import { artifactsRouter } from "./routes/artifacts.js";
import { errorHandler } from "./middleware/errorHandler.js";

const app = express();
app.use(cors({ origin: process.env.CLIENT_ORIGIN ?? "http://localhost:5173" }));
app.use(express.json({ limit: "10mb" }));

app.get("/api/health", (_, res) => res.json({ ok: true }));
app.use("/api/artifacts", artifactsRouter);
app.use(errorHandler);

const port = Number(process.env.PORT ?? 5000);
app.listen(port, () =>
  console.log(`PlantDoctor.Portal server listening on :${port}`),
);
