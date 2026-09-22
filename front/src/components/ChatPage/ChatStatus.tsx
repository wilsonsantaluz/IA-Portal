// src/components/ChatStatus.tsx
import { Typography } from "@mui/material";
import React from "react";

export function ChatStatus({ text }: { text?: string }) {
  if (!text) return null;

  return (
    <Typography
      fontSize={13}
      color="#6b7280"
      fontStyle="italic"
      sx={{
        backgroundColor: "#f8f9fa",
        padding: "6px 10px",
        borderRadius: 1,
        borderLeft: "3px solid #3b82f6",
        whiteSpace: "pre-wrap",
        marginBottom: "6px",
      }}
    >
      {text}
    </Typography>
  );
}
