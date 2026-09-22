import { useEffect, useState } from "react";
import { Box, List, ListItemButton, ListItemText, Typography, Divider } from "@mui/material";
import api from "../services/api";
import React from "react";

export default function SidebarHistory({ onSelectSession }: { onSelectSession: (sessionId: string) => void }) {
  const [sessions, setSessions] = useState<any[]>([]);

  useEffect(() => {
    api.get("/api/chatBot/sessions") // <-- VOCÊ PRECISA RETORNAR TODAS AS SESSÕES
      .then(res => setSessions(res.data))
      .catch(() => console.warn("Erro ao carregar sessões"));
  }, []);

  return (
    <Box
      sx={{
        width: "280px",
        height: "100vh",
        borderRight: "1px solid #ddd",
        overflowY: "auto",
        p: 2,
        bgcolor: "#fafafa",
        position: "fixed",
        left: 0,
        top: 0,
      }}
    >
      <Typography variant="h6" sx={{ mb: 2 }}>Seus Chats</Typography>

      <List>
        {sessions.map((s, index) => (
          <Box key={index}>
            <ListItemButton onClick={() => onSelectSession(s.sessionId)}>
              <ListItemText
                primary={s.title ?? "Conversa sem título"}
                secondary={new Date(s.createdAt).toLocaleString()}
              />
            </ListItemButton>
            <Divider />
          </Box>
        ))}
      </List>
    </Box>
  );
}
