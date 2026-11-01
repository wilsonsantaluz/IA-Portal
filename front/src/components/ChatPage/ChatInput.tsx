import React from "react";
import {
  Box,
  TextField,
  IconButton,
  CircularProgress,
  Typography,
  Tooltip,
} from "@mui/material";
import SendRounded from "@mui/icons-material/SendRounded";
import AttachFileRounded from "@mui/icons-material/AttachFileRounded";
interface ChatInputProps {
  input: string;
  loading: boolean;
  onInputChange: (value: string) => void;
  onSend: () => void;
  onUpload: (file: File) => void;
}
export default function ChatInput({
  input,
  loading,
  onInputChange,
  onSend,
  onUpload,
}: ChatInputProps) {
  return (
    <Box
      sx={{
        px: { xs: 2, md: 4 },
        pb: 2,
        pt: 1.5,
        width: "100%",
        maxWidth: 1040,
        mx: "auto",
        flexShrink: 0,
      }}
    >
      <Box
        sx={{
          p: 2,
          bgcolor: "background.paper",
          border: "1px solid",
          borderColor: "divider",
          borderRadius: "16px",
          boxShadow: "var(--portal-shadow)",
          transition: "border-color .2s, box-shadow .2s",
          "&:focus-within": {
            borderColor: "primary.main",
            boxShadow:
              "0 0 0 3px color-mix(in srgb, var(--portal-primary) 10%, transparent), var(--portal-shadow)",
          },
        }}
      >
        <TextField
          fullWidth
          multiline
          maxRows={5}
          minRows={2}
          placeholder="Como posso ajudar você hoje?"
          variant="standard"
          InputProps={{ disableUnderline: true }}
          inputProps={{
            "aria-label": "Sua pergunta para a IA",
            "data-chat-input": true,
          }}
          sx={{ "& textarea": { fontSize: 14, lineHeight: 1.7 } }}
          value={input}
          disabled={loading}
          onChange={(e) => onInputChange(e.target.value)}
          onKeyDown={(e) => {
            if (
              e.key === "Enter" &&
              !e.shiftKey &&
              !e.nativeEvent.isComposing
            ) {
              e.preventDefault();
              if (!loading && input.trim()) onSend();
            }
          }}
        />
        <Box
          display="flex"
          alignItems="center"
          justifyContent="space-between"
          mt={1}
        >
          <Tooltip title="Analisar PDF ou imagem">
            <IconButton
              component="label"
              disabled={loading}
              aria-label="Analisar PDF ou imagem"
              size="small"
            >
              <AttachFileRounded fontSize="small" />
              <input
                type="file"
                hidden
                accept="application/pdf,image/png,image/jpeg"
                disabled={loading}
                onChange={(event) => {
                  const file = event.target.files?.[0];
                  event.target.value = "";
                  if (file) onUpload(file);
                }}
              />
            </IconButton>
          </Tooltip>
          <IconButton
            aria-label="Enviar pergunta"
            disabled={loading || !input.trim()}
            onClick={onSend}
            sx={{
              width: 38,
              height: 38,
              color: "primary.contrastText",
              bgcolor: "primary.main",
              boxShadow:
                "0 4px 12px color-mix(in srgb, var(--portal-primary) 25%, transparent)",
              "&:hover": {
                bgcolor: "primary.dark",
                transform: "translateY(-1px)",
              },
              "&.Mui-disabled": { bgcolor: "action.disabledBackground" },
              transition: "transform .2s",
            }}
          >
            {loading ? (
              <CircularProgress size={20} color="inherit" />
            ) : (
              <SendRounded sx={{ fontSize: 19 }} />
            )}
          </IconButton>
        </Box>
      </Box>
      <Typography
        fontSize={10}
        color="text.secondary"
        textAlign="center"
        mt={1.2}
      >
        A IA pode cometer erros. Confira as informações importantes.
      </Typography>
    </Box>
  );
}
