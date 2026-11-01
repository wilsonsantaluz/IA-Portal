import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  Container,
  Grid,
  Paper,
  Skeleton,
  Stack,
  Typography,
  useTheme,
} from "@mui/material";
import { alpha } from "@mui/material/styles";
import { Link } from "react-router-dom";
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  ResponsiveContainer,
  BarChart,
  Bar,
} from "recharts";
import StarOutlineRounded from "@mui/icons-material/StarOutlineRounded";
import RateReviewOutlined from "@mui/icons-material/RateReviewOutlined";
import TrendingDownRounded from "@mui/icons-material/TrendingDownRounded";
import ForumOutlined from "@mui/icons-material/ForumOutlined";
import InsightsOutlined from "@mui/icons-material/InsightsOutlined";
import ArrowForwardRounded from "@mui/icons-material/ArrowForwardRounded";
import RefreshRounded from "@mui/icons-material/RefreshRounded";
import AutoAwesomeOutlined from "@mui/icons-material/AutoAwesomeOutlined";
import LibraryBooksOutlined from "@mui/icons-material/LibraryBooksOutlined";
import api from "../services/api";
import { DashboardCard } from "../components/DashboardCard";

type Summary = {
  totalRatings: number;
  ratingsLast30Days: number;
  avgRating: number;
  lowRatingPct: number;
};
type Month = { year: number; month: number; avgRating: number; count: number };
type Section = { section: string; avgRating: number; count: number };
const number = (value: number, digits = 0) =>
  value.toLocaleString("pt-BR", {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  });
export default function AdminDashboardPage() {
  const theme = useTheme();
  const [summary, setSummary] = useState<Summary | null>(null);
  const [byMonth, setByMonth] = useState<Month[]>([]);
  const [bySection, setBySection] = useState<Section[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const load = useCallback(async () => {
    setLoading(true);
    setError(false);
    try {
      const [s, m, sec] = await Promise.all([
        api.get<Summary>("api/chatkb/metrics/summary"),
        api.get<Month[]>("api/chatkb/metrics/by-month"),
        api.get<Section[]>("api/chatkb/metrics/by-section"),
      ]);
      setSummary(s.data);
      setByMonth(Array.isArray(m.data) ? m.data : []);
      setBySection(Array.isArray(sec.data) ? sec.data : []);
    } catch {
      setError(true);
      setSummary(null);
      setByMonth([]);
      setBySection([]);
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => {
    void load();
  }, [load]);
  const hasRatings = !!summary?.totalRatings;
  const panel = {
    p: { xs: 2.5, md: 3 },
    border: "1px solid",
    borderColor: "divider",
    borderRadius: "16px",
    boxShadow: "var(--portal-shadow)",
    height: "100%",
  };
  const tooltip = {
    backgroundColor: theme.palette.background.paper,
    borderColor: theme.palette.divider,
    borderRadius: 12,
    color: theme.palette.text.primary,
    boxShadow: "var(--portal-shadow)",
  };
  const empty = (
    <Box
      sx={{
        height: 260,
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        textAlign: "center",
        gap: 1.5,
      }}
    >
      <Box
        sx={{
          p: 1.5,
          display: "flex",
          borderRadius: "14px",
          bgcolor: alpha(theme.palette.primary.main, 0.07),
          color: "primary.main",
        }}
      >
        <InsightsOutlined />
      </Box>
      <Typography fontSize={14} fontWeight={600}>
        {error ? "Dados indisponíveis" : "As primeiras avaliações começam aqui"}
      </Typography>
      <Typography fontSize={12} color="text.secondary" maxWidth={270}>
        {error
          ? "Tente atualizar o painel novamente."
          : "Avalie as respostas no chat para acompanhar a qualidade da IA."}
      </Typography>
      {!error && (
        <Button
          component={Link}
          to="/chat"
          size="small"
          endIcon={<ArrowForwardRounded />}
        >
          Ir para o chat
        </Button>
      )}
    </Box>
  );
  return (
    <Container
      maxWidth="xl"
      className="portal-page"
      sx={{ py: { xs: 3, md: 4 }, px: { xs: 2, md: 4 } }}
    >
      <Stack
        direction={{ xs: "column", sm: "row" }}
        justifyContent="space-between"
        alignItems={{ sm: "center" }}
        gap={2}
        mb={3.5}
      >
        <Box>
          <Typography
            variant="overline"
            color="primary.main"
            fontWeight={700}
            letterSpacing=".14em"
          >
            VISÃO GERAL
          </Typography>
          <Typography variant="h4" fontSize={{ xs: 25, md: 30 }}>
            Painel de Qualidade da IA
          </Typography>
          <Typography color="text.secondary" fontSize={13} mt={0.8}>
            Cada conversa, uma oportunidade de evoluir o conhecimento.
          </Typography>
        </Box>
        <Button
          variant="outlined"
          startIcon={<RefreshRounded />}
          disabled={loading}
          onClick={() => void load()}
          sx={{
            alignSelf: { xs: "flex-start", sm: "center" },
            borderColor: "divider",
            bgcolor: "background.paper",
          }}
        >
          Atualizar painel
        </Button>
      </Stack>
      {error && (
        <Alert severity="error" sx={{ mb: 3 }}>
          Não foi possível carregar as métricas. Atualize o painel para tentar
          novamente.
        </Alert>
      )}
      <Grid container spacing={2.5} className="portal-stagger">
        <Grid item xs={12} sm={6} lg={3}>
          <DashboardCard
            title="Nota média"
            value={hasRatings ? number(summary!.avgRating, 2) : "—"}
            caption="Qualidade das respostas · escala de 1 a 10"
            icon={<StarOutlineRounded />}
            loading={loading}
          />
        </Grid>
        <Grid item xs={12} sm={6} lg={3}>
          <DashboardCard
            title="Avaliações recentes"
            value={summary ? number(summary.ratingsLast30Days) : "—"}
            caption="Avaliações recebidas nos últimos 30 dias"
            tone="secondary"
            icon={<RateReviewOutlined />}
            loading={loading}
          />
        </Grid>
        <Grid item xs={12} sm={6} lg={3}>
          <DashboardCard
            title="Pontos de atenção"
            value={hasRatings ? `${number(summary!.lowRatingPct, 1)}%` : "—"}
            caption="Respostas com avaliação igual ou inferior a 5"
            tone="warning"
            icon={<TrendingDownRounded />}
            loading={loading}
          />
        </Grid>
        <Grid item xs={12} sm={6} lg={3}>
          <DashboardCard
            title="Total de avaliações"
            value={summary ? number(summary.totalRatings) : "—"}
            caption="Feedbacks registrados desde o início"
            tone="success"
            icon={<ForumOutlined />}
            loading={loading}
          />
        </Grid>
        <Grid item xs={12} lg={8}>
          <Paper sx={panel}>
            <Stack
              direction="row"
              justifyContent="space-between"
              alignItems="center"
              gap={1}
            >
              <Box>
                <Typography fontWeight={650}>Evolução da qualidade</Typography>
                <Typography fontSize={12} color="text.secondary" mt={0.5}>
                  Nota média das respostas ao longo dos meses
                </Typography>
              </Box>
              <Chip size="small" label="Mensal" variant="outlined" />
            </Stack>
            {loading ? (
              <Skeleton variant="rounded" height={260} sx={{ mt: 3 }} />
            ) : byMonth.length ? (
              <Box sx={{ height: 280, mt: 3 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart
                    data={byMonth}
                    margin={{ top: 10, right: 10, left: -20, bottom: 0 }}
                  >
                    <defs>
                      <linearGradient
                        id="quality-fill"
                        x1="0"
                        y1="0"
                        x2="0"
                        y2="1"
                      >
                        <stop
                          offset="0%"
                          stopColor={theme.palette.primary.main}
                          stopOpacity={0.22}
                        />
                        <stop
                          offset="100%"
                          stopColor={theme.palette.primary.main}
                          stopOpacity={0}
                        />
                      </linearGradient>
                    </defs>
                    <CartesianGrid
                      stroke={theme.palette.divider}
                      strokeDasharray="4 6"
                      vertical={false}
                    />
                    <XAxis
                      dataKey={(x) =>
                        `${String(x.month).padStart(2, "0")}/${x.year}`
                      }
                      tick={{
                        fill: theme.palette.text.secondary,
                        fontSize: 11,
                      }}
                      axisLine={false}
                      tickLine={false}
                      dy={10}
                    />
                    <YAxis
                      domain={[0, 10]}
                      tick={{
                        fill: theme.palette.text.secondary,
                        fontSize: 11,
                      }}
                      axisLine={false}
                      tickLine={false}
                    />
                    <Tooltip
                      contentStyle={tooltip}
                      formatter={(value) => [
                        number(Number(value), 2),
                        "Nota média",
                      ]}
                    />
                    <Area
                      name="Nota média"
                      type="monotone"
                      dataKey="avgRating"
                      stroke={theme.palette.primary.main}
                      strokeWidth={3}
                      fill="url(#quality-fill)"
                      dot={{
                        r: 4,
                        strokeWidth: 3,
                        fill: theme.palette.background.paper,
                      }}
                      animationDuration={800}
                      isAnimationActive={
                        !window.matchMedia("(prefers-reduced-motion: reduce)")
                          .matches
                      }
                    />
                  </AreaChart>
                </ResponsiveContainer>
              </Box>
            ) : (
              empty
            )}
          </Paper>
        </Grid>
        <Grid item xs={12} lg={4}>
          <Paper sx={panel}>
            <Typography fontWeight={650}>Qualidade por seção</Typography>
            <Typography fontSize={12} color="text.secondary" mt={0.5}>
              Como cada área da base está contribuindo
            </Typography>
            {loading ? (
              <Skeleton variant="rounded" height={260} sx={{ mt: 3 }} />
            ) : bySection.length ? (
              <Box sx={{ height: Math.max(280, bySection.length * 42), mt: 3 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart
                    data={bySection}
                    layout="vertical"
                    margin={{ left: 0, right: 16 }}
                  >
                    <CartesianGrid
                      stroke={theme.palette.divider}
                      strokeDasharray="4 6"
                      horizontal={false}
                    />
                    <XAxis
                      type="number"
                      domain={[0, 10]}
                      tick={{
                        fill: theme.palette.text.secondary,
                        fontSize: 11,
                      }}
                      axisLine={false}
                      tickLine={false}
                    />
                    <YAxis
                      type="category"
                      dataKey="section"
                      width={95}
                      tick={{
                        fill: theme.palette.text.secondary,
                        fontSize: 11,
                      }}
                      axisLine={false}
                      tickLine={false}
                    />
                    <Tooltip
                      contentStyle={tooltip}
                      cursor={{ fill: alpha(theme.palette.primary.main, 0.06) }}
                      formatter={(value) => [
                        number(Number(value), 2),
                        "Nota média",
                      ]}
                    />
                    <Bar
                      dataKey="avgRating"
                      fill={theme.palette.secondary.main}
                      radius={[0, 5, 5, 0]}
                      maxBarSize={18}
                      isAnimationActive={
                        !window.matchMedia("(prefers-reduced-motion: reduce)")
                          .matches
                      }
                    />
                  </BarChart>
                </ResponsiveContainer>
              </Box>
            ) : (
              empty
            )}
          </Paper>
        </Grid>
        <Grid item xs={12}>
          <Paper
            sx={{
              ...panel,
              background: `linear-gradient(110deg, ${alpha(theme.palette.primary.main, 0.06)}, ${alpha(theme.palette.secondary.main, 0.035)}), ${theme.palette.background.paper}`,
            }}
          >
            <Stack
              direction={{ xs: "column", md: "row" }}
              gap={3}
              alignItems={{ md: "center" }}
            >
              <Box
                sx={{
                  display: "grid",
                  placeItems: "center",
                  width: 48,
                  height: 48,
                  borderRadius: "14px",
                  bgcolor: alpha(theme.palette.primary.main, 0.1),
                  color: "primary.main",
                  flexShrink: 0,
                }}
              >
                <AutoAwesomeOutlined />
              </Box>
              <Box flex={1}>
                <Typography fontWeight={650}>
                  Transforme informação em conhecimento
                </Typography>
                <Typography color="text.secondary" fontSize={13} mt={0.6}>
                  Mantenha a base atualizada e avalie as respostas para
                  acompanhar a experiência com a IA.
                </Typography>
              </Box>
              <Button
                component={Link}
                to="/kb"
                variant="outlined"
                startIcon={<LibraryBooksOutlined />}
                sx={{ borderColor: "divider", bgcolor: "background.paper" }}
              >
                Gerenciar base
              </Button>
              <Button
                component={Link}
                to="/chat"
                variant="contained"
                endIcon={<ArrowForwardRounded />}
              >
                Conversar com a IA
              </Button>
            </Stack>
          </Paper>
        </Grid>
      </Grid>
    </Container>
  );
}
