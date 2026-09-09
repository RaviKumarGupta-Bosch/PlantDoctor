import { NextFunction, Request, Response } from "express";
import { ZodError } from "zod";

export function errorHandler(
  err: unknown,
  _req: Request,
  res: Response,
  _next: NextFunction,
) {
  if (err instanceof ZodError) {
    return res
      .status(400)
      .json({ error: "ValidationError", issues: err.issues });
  }
  const message = err instanceof Error ? err.message : "Unknown error";
  console.error("[error]", err);
  res.status(500).json({ error: "ServerError", message });
}
