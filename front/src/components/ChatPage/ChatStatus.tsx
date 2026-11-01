// src/components/ChatStatus.tsx
import { Box, CircularProgress, Typography } from "@mui/material";
import React from "react";

export function ChatStatus({ text }: { text?: string }) {
  if (!text) return null;

  return (
    <Box
      role="status"
      aria-live="polite"
      aria-atomic="true"
      sx={{
        display: "flex", alignItems: "center", gap: 1.5,
        mx: { xs: 2, md: 4 }, mt: 1, px: 2, py: 1.5,
        bgcolor: "background.paper", border: "1px solid", borderColor: "divider",
        borderRadius: "12px", boxShadow: "var(--portal-shadow)",
        "@media (prefers-reduced-motion: reduce)": {
          "& .MuiCircularProgress-root, & .MuiCircularProgress-circle": { animation: "none" },
        },
      }}
    >
      <CircularProgress size={20} thickness={4} aria-hidden="true" sx={{ flexShrink: 0 }} />
      <Typography fontSize={13} color="text.secondary" sx={{ overflowWrap: "anywhere" }}>
        {text}
      </Typography>
    </Box>
  );
}
