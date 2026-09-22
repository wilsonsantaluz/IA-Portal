import { Card, Box, Typography } from "@mui/material";
import React from "react";

export function DashboardCard({ title, value, color, icon }: any) {
  return (
    <Card
      sx={{
        position: "relative",
        minWidth: 240,
        height: 130, // mais fino
        borderRadius: 3,
        px: 2,
        py: 1.5,
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        overflow: "visible",
        background: `linear-gradient(135deg, ${color}22, ${color}11)`,
        border: `1px solid ${color}33`,
        boxShadow: "0 4px 14px rgba(0,0,0,0.06)",
        transition: "0.25s ease",
        "&:hover": {
          transform: "translateY(-3px)",
          boxShadow: "0 10px 26px rgba(0,0,0,0.12)",
        },
      }}
    >
      {/* ÍCONE FLUTUANTE */}
      <Box
        sx={{
          position: "absolute",
          top: -18,
          right: 16,
          width: 48,
          height: 48,
          borderRadius: 3,
          background: color,
          color: "#fff",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: 26,
          boxShadow: "0 6px 18px rgba(0,0,0,0.24)",
        }}
      >
        {icon}
      </Box>

      {/* TÍTULO */}
      <Typography
        sx={{
          opacity: 0.7,
          fontSize: "0.80rem",
          fontWeight: 500,
          mb: 0.5,
        }}
      >
        {title}
      </Typography>

      {/* VALOR */}
      <Typography
        sx={{
          fontSize: "1.8rem",
          fontWeight: 700,
          lineHeight: 1.2,
        }}
      >
        {value}
      </Typography>
    </Card>
  );
}
