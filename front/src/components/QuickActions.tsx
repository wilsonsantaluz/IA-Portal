import { Box, Card, Typography } from "@mui/material";
import EmojiObjectsOutlinedIcon from "@mui/icons-material/EmojiObjectsOutlined";
import RuleOutlinedIcon from "@mui/icons-material/RuleOutlined";
import FunctionsOutlinedIcon from "@mui/icons-material/FunctionsOutlined";
import React from "react";

export default function QuickActions({ onAsk }: { onAsk: (type: string) => void }) {

  const actions = [
    {
      id: "media_inadimplencia",
      title: "Média Inadimplência",
      icon: <EmojiObjectsOutlinedIcon sx={{ fontSize: 40 }} />,
      desc: "Resumo estatístico da inadimplência por carteira.",
    },
    {
      id: "elegibilidade_cauda",
      title: "Elegibilidade / Cauda",
      icon: <RuleOutlinedIcon sx={{ fontSize: 40 }} />,
      desc: "Critérios contratuais para análise de recebíveis.",
    },
    {
      id: "calculo_vp",
      title: "Cálculo VP",
      icon: <FunctionsOutlinedIcon sx={{ fontSize: 40 }} />,
      desc: "Cálculo financeiro aplicado nos relatórios.",
    },
  ];

  return (
    <Box
      sx={{
        display: "flex",
        gap: 3,
        px: 3,
        py: 2,
        overflowX: "auto",
        bgcolor: "transparent",
      }}
    >
      {actions.map((a) => (
        <Card
          key={a.id}
          onClick={() => onAsk(a.id)}
          sx={{
            p: 2,
            width: 230,
            cursor: "pointer",
            borderRadius: "18px",
            background: "rgba(255,255,255,0.6)",
            backdropFilter: "blur(14px)",
            boxShadow: "0 4px 18px rgba(0,0,0,0.08)",
            transition: "all .25s ease",
            "&:hover": {
              transform: "translateY(-4px)",
              boxShadow: "0 8px 28px rgba(0,0,0,0.18)",
              background: "rgba(255,255,255,0.85)",
            },
          }}
        >
          <Box sx={{ display: "flex", justifyContent: "center", mb: 1 }}>
            {a.icon}
          </Box>

          <Typography fontWeight={700} textAlign="center">
            {a.title}
          </Typography>

          <Typography
            variant="body2"
            textAlign="center"
            sx={{ opacity: 0.7, mt: 0.6 }}
          >
            {a.desc}
          </Typography>
        </Card>
      ))}
    </Box>
  );
}
