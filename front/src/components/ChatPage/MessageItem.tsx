import React from "react";
import { Box, Typography, IconButton, Collapse } from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import StarIcon from "@mui/icons-material/Star";
import HelpOutlineIcon from "@mui/icons-material/HelpOutline";
import MarkdownRenderer from "../MarkdownRenderer";
import { useTheme } from "@mui/material/styles";

interface MessageItemProps {
  message: {
    id: string;
    from: "user" | "ia";
    streamText?: string;
    finalText?: string;
    finished?: boolean;
    rating?: number;
    rated?: boolean;
    expanded?: boolean;
  };
  index: number;
  statusMessage?: string;
  onToggleExpand: (id: string) => void;
  onSendRating: (score: number, idx: number) => void;
}

const getPreview = (text: string, max = 120) => {
  const t = (text ?? "").trim();
  if (!t) return "";
  return t.length > max ? t.slice(0, max) + "…" : t;
};

export default function MessageItem({
  message,
  index,
  statusMessage,
  onToggleExpand,
  onSendRating,
}: MessageItemProps) {
  const isExpanded = message.expanded !== false;
  const theme = useTheme();
  const isDark = theme.palette.mode === "dark";
  const iaText =
    message.from === "ia"
      ? message.finished
        ? message.finalText ?? ""
        : message.streamText ?? ""
      : "";

  // =========================
  // USER
  // =========================
  if (message.from === "user" && message.finalText) {
    return (
      <Box display="flex" justifyContent="flex-end">
        <Box maxWidth="80%" className="user-question-card">
          <Box className="user-question-header">
            <HelpOutlineIcon sx={{ fontSize: 34, color: "#c2cf00" }} />
            <span className="user-question-label">{message.finalText}</span>
          </Box>
        </Box>
      </Box>
    );
  }

  // =========================
  // IA
  // =========================
  if (message.from === "ia" && iaText) {
    const preview = getPreview(iaText);

    return (
      <Box>
        {/* 🔥 STATUS DENTRO DA MENSAGEM (ENQUANTO STREAMA) */}
        {!message.finished && statusMessage && (
          <Typography
            fontSize={13}
            color={theme.palette.text.secondary}
            fontStyle="italic"
            sx={{
              backgroundColor: theme.palette.background.paper,
              padding: "6px 10px",
              borderRadius: 1,
              borderLeft: "3px solid #3b82f6",
              whiteSpace: "pre-wrap",
              marginBottom: "6px",
            }}
          >
            {statusMessage}
          </Typography>
        )}

        {!message.finished && (
          <Typography fontSize={12} color={theme.palette.text.secondary} fontStyle="italic">
            Respondendo…
          </Typography>
        )}

        <Box
          sx={{
            border: message.finished ? "1px solid ${theme.palette.divider}": "none",
            borderRadius: 2,
            backgroundColor: theme.palette.background.paper,
            padding: "8px 10px",
            borderLeft: message.finished ? "3px solid #c2cf00" : undefined,
            boxShadow:
            theme.palette.mode === "dark"
              ? "0 10px 28px rgba(0,0,0,.6), inset 0 1px 0 rgba(255,255,255,.04)"
              : "0 14px 36px rgba(0,0,0,.25)",
          }}
        >
          {message.finished && (
            <Box
              display="flex"
              alignItems="center"
              justifyContent="space-between"
              sx={{ cursor: "pointer" }}
              onClick={() => onToggleExpand(message.id)}
            >
              <IconButton
                size="small"
                sx={{
                  transition: "transform 180ms ease",
                  transform: isExpanded ? "rotate(180deg)" : "rotate(0deg)",
                }}
              >
                <ExpandMoreIcon />
              </IconButton>
            </Box>
          )}

          {message.finished && (
            <Collapse in={!isExpanded} timeout="auto" unmountOnExit>
              <Typography fontSize={13} color="#475569" mt={1}>
                {preview}
              </Typography>
            </Collapse>
          )}

          <Collapse
            in={!message.finished || isExpanded}
            timeout="auto"
            unmountOnExit
            data-rating-anchor
          >
            <Box mt={1}>
              {/* 🔥 SEMPRE MARKDOWN */}
              <MarkdownRenderer text={iaText} variant="ia" />

              {message.finished && (
                <Box mt={0.5}>
                  {[...Array(10)].map((_, i) => (
                    <IconButton
                      key={i}
                      size="small"
                      onClick={() => onSendRating(i + 1, index)}
                    >
                      <StarIcon
                        fontSize="small"
                        color={
                          message.rating && message.rating >= i + 1
                            ? "warning"
                            : "inherit"
                        }
                      />
                    </IconButton>
                  ))}
                </Box>
              )}
            </Box>
          </Collapse>
        </Box>
      </Box>
    );
  }

  return null;
}
