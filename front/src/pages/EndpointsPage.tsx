import { useState } from "react";
import { Box, Button, TextField, Typography, Paper } from "@mui/material";
import api from "../services/api";
import React from "react";

export default function EndpointsPage() {
  const [result, setResult] = useState("");
  const [endpoint, setEndpoint] = useState("/chatkb/metrics/summary");

  const callEndpoint = async () => {
    try {
      const resp = await api.get(endpoint);
      setResult(JSON.stringify(resp.data, null, 2));
    } catch (err: any) {
      setResult("Erro: " + err.message);
    }
  };

  return (
    <Box sx={{ p: 2 }}>
      <Typography variant="h5" sx={{ mb: 2 }}>
        Testar Endpoints da API
      </Typography>

      <Paper sx={{ p: 2, mb: 2 }}>
        <TextField
          fullWidth
          label="Endpoint"
          value={endpoint}
          onChange={(e) => setEndpoint(e.target.value)}
          sx={{ mb: 2 }}
        />

        <Button variant="contained" onClick={callEndpoint}>
          Executar
        </Button>
      </Paper>

      <Paper sx={{ p: 2, whiteSpace: "pre-wrap", background: "#111", color: "#0f0" }}>
        {result}
      </Paper>
    </Box>
  );
}
