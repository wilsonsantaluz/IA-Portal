import { useEffect, useState } from "react";
import {
  Container,
  Grid,
  Paper,
  Typography,
} from "@mui/material";
import api from "../services/api";

import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  ResponsiveContainer,
  BarChart,
  Bar
} from "recharts";

import ShoppingBagIcon from "@mui/icons-material/ShoppingBag";
import PersonIcon from "@mui/icons-material/Person";
import ShoppingCartIcon from "@mui/icons-material/ShoppingCart";
import MailIcon from "@mui/icons-material/Mail";
import { DashboardCard } from "../components/DashboardCard";
import React from "react";

export default function AdminDashboardPage() {
  const [summary, setSummary] = useState<any>(null);

  // 🔥 Blindagem automática — garante Arrays SEMPRE
  const [byMonth, setByMonth] = useState<any[]>([]);
  const [bySection, setBySection] = useState<any[]>([]);

  function ensureArray(value: any) {
    return Array.isArray(value) ? value : [];
  }

  useEffect(() => {
    (async () => {
      const s = await api.get("api/chatkb/metrics/summary");
      const data = s.data;

      setSummary({
        totalRatings: data.totalRatings ?? 0,
        ratingsLast30Days: data.ratingsLast30Days ?? 0,
        avgRating: Number(data.avgRating ?? 0),
        lowRatingPct: Number(data.lowRatingPct ?? 0),
      });

      const m = await api.get("api/chatkb/metrics/by-month");
      setByMonth(ensureArray(m.data));  // <— BLINDADO

      const sec = await api.get("api/chatkb/metrics/by-section");
      setBySection(ensureArray(sec.data)); // <— BLINDADO
    })();
  }, []);

  return (
    <Container maxWidth="lg" sx={{ mt: 4, mb: 6 }}>
      <Typography
        variant="h5"
        sx={{ mb: 3, fontWeight: 700, animation: "fadeIn .6s ease" }}
      >
        Painel de Qualidade da IA
      </Typography>

      {/* === CARDS === */}
      <Grid container spacing={3}>
        <Grid item xs={12} md={3}>
          <DashboardCard
            title="Nota Média"
            value={summary?.avgRating?.toFixed(2) ?? "-"}
            color="#4C6FFF"
            icon={<ShoppingBagIcon />}
          />
        </Grid>

        <Grid item xs={12} md={3}>
          <DashboardCard
            title="Avaliações (30 dias)"
            value={summary?.ratingsLast30Days ?? "-"}
            color="#B77CFF"
            icon={<PersonIcon />}
          />
        </Grid>

        <Grid item xs={12} md={3}>
          <DashboardCard
            title="% Nota Baixa (≤5)"
            value={
              summary?.lowRatingPct != null
                ? summary.lowRatingPct.toFixed(1) + "%"
                : "-"
            }
            color="#FFDA7C"
            icon={<ShoppingCartIcon />}
          />
        </Grid>

        <Grid item xs={12} md={3}>
          <DashboardCard
            title="Total Avaliações"
            value={summary?.totalRatings ?? "-"}
            color="#FF8A80"
            icon={<MailIcon />}
          />
        </Grid>

        {/* === LINE CHART === */}
        <Grid item xs={12} md={8}>
          <Paper
            sx={{
              p: 2,
              height: 340,
              borderRadius: 4,
              background: "rgba(255,255,255,0.7)",
              backdropFilter: "blur(12px)",
              boxShadow: "0 8px 32px rgba(0,0,0,0.08)",
              animation: "fadeInUp .6s ease",
            }}
          >
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              Média de rating por mês
            </Typography>

            <ResponsiveContainer width="100%" height="100%">
              <LineChart
                data={ensureArray(byMonth)}   // <— BLINDADO
                margin={{ top: 10, right: 20, bottom: 0, left: 0 }}
              >
                <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                <XAxis dataKey={(x: any) => `${x.month}/${x.year}`} />
                <YAxis />
                <Tooltip />

                <Line
                  type="monotone"
                  dataKey="avgRating"
                  stroke="#4C6FFF"
                  strokeWidth={3}
                  dot={{ r: 5 }}
                  activeDot={{ r: 7 }}
                  animationDuration={900}
                  animationEasing="ease-out"
                />
              </LineChart>
            </ResponsiveContainer>
          </Paper>
        </Grid>

        {/* === BAR CHART === */}
        <Grid item xs={12} md={4}>
          <Paper
            sx={{
              p: 2,
              height: 340,
              borderRadius: 4,
              background: "rgba(255,255,255,0.7)",
              backdropFilter: "blur(12px)",
              boxShadow: "0 8px 32px rgba(0,0,0,0.08)",
              animation: "fadeInUp .8s ease",
            }}
          >
            <Typography variant="subtitle2" sx={{ mb: 1 }}>
              Notas por seção
            </Typography>

            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={ensureArray(bySection)}> {/* BLINDADO */}
                <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                <XAxis dataKey="section" />
                <YAxis />
                <Tooltip />

                <Bar
                  dataKey="avgRating"
                  fill="#7B61FF"
                  animationDuration={1100}
                  animationEasing="ease-out"
                  radius={[8, 8, 0, 0]}
                />
              </BarChart>
            </ResponsiveContainer>
          </Paper>
        </Grid>
      </Grid>

      {/* Animações */}
      <style>
        {`
          @keyframes fadeIn {
            from { opacity: 0; }
            to { opacity: 1; }
          }
          @keyframes fadeInUp {
            from { opacity: 0; transform: translateY(10px); }
            to { opacity: 1; transform: translateY(0); }
          }
        `}
      </style>
    </Container>
  );
}
