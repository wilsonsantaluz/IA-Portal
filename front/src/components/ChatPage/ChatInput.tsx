import React from "react";
import {
  Box,
  TextField,
  IconButton,
  CircularProgress,
} from "@mui/material";
import SendIcon from "@mui/icons-material/Send";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import { useTheme } from "@mui/material/styles";

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
  const theme = useTheme();
  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      onSend();
    }
  };

  return (
    <Box px={2} pb={1}>
      <Box
        sx={{
          maxWidth: 1200,
          mx: "auto",
          display: "flex",
          alignItems: "center",
          gap: 1,
          px: 2,
          position: "sticky",
          colosr: theme.palette.text.primary,
          border: `1px solid ${theme.palette.divider}`,
          
          py: 1,
          borderRadius: "28px",
          bottom: 0,
          zIndex: 20,
          background: theme.palette.background.paper,
          boxShadow: theme.shadows[8],
        }}
      >
        <TextField
         sx={{
          "& .MuiInputBase-input": { color: theme.palette.text.primary },
          "& textarea": { color: theme.palette.text.primary },
        }}
          fullWidth
          multiline
          maxRows={4}
          placeholder="Digite sua pergunta"
          variant="standard"
          InputProps={{ disableUnderline: true }}
          value={input}
          disabled={loading}
          onChange={(e) => onInputChange(e.target.value)}
          onKeyDown={handleKeyDown}
        />

        <IconButton disabled={loading} onClick={onSend}>
          {loading ? <CircularProgress size={22} /> : <SendIcon />}
        </IconButton>

        <IconButton component="label" disabled={loading} aria-label="Analisar PDF ou imagem">
          <PictureAsPdfIcon />
          <input type="file" hidden accept="application/pdf,image/png,image/jpeg" disabled={loading}
            onChange={(event) => {
              const file = event.target.files?.[0];
              event.target.value = "";
              if (file) onUpload(file);
            }} />
        </IconButton>
      </Box>
    </Box>
  );
}
