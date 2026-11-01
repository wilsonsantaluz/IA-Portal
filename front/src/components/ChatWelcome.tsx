import { Box, Button, Card, CardActionArea, Typography } from "@mui/material";
import { alpha, useTheme } from "@mui/material/styles";
import DescriptionOutlined from "@mui/icons-material/DescriptionOutlined";
import SummarizeOutlined from "@mui/icons-material/SummarizeOutlined";
import InsightsOutlined from "@mui/icons-material/InsightsOutlined";
import CompareArrowsRounded from "@mui/icons-material/CompareArrowsRounded";
import ArrowForwardRounded from "@mui/icons-material/ArrowForwardRounded";
import AutoAwesomeOutlined from "@mui/icons-material/AutoAwesomeOutlined";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
const actions = [
  {
    title: "Perguntar sobre um documento",
    desc: "Encontre respostas a partir dos seus documentos.",
    prompt: "Quero tirar uma dúvida sobre um documento: ",
    icon: <DescriptionOutlined />,
    tone: "primary",
  },
  {
    title: "Resumir conteúdo",
    desc: "Transforme textos longos em ideias essenciais.",
    prompt: "Resuma os principais pontos do seguinte conteúdo: ",
    icon: <SummarizeOutlined />,
    tone: "secondary",
  },
  {
    title: "Analisar informações",
    desc: "Explore os detalhes e conecte as informações.",
    prompt:
      "Analise as informações a seguir e destaque os pontos mais importantes: ",
    icon: <InsightsOutlined />,
    tone: "violet",
  },
  {
    title: "Comparar conteúdos",
    desc: "Identifique semelhanças e diferenças importantes.",
    prompt:
      "Compare os conteúdos a seguir, destacando semelhanças e diferenças: ",
    icon: <CompareArrowsRounded />,
    tone: "success",
  },
] as const;
export default function ChatWelcome({
  onChoose,
}: {
  onChoose: (prompt: string) => void;
}) {
  const theme = useTheme();
  const { hasPermission } = useAuth();
  return (
    <Box
      className="portal-page"
      sx={{
        maxWidth: 1040,
        mx: "auto",
        width: "100%",
        px: { xs: 2, md: 4 },
        pt: { xs: 3, md: 5 },
        pb: 3,
      }}
    >
      <Box
        sx={{
          display: "inline-flex",
          alignItems: "center",
          gap: 1,
          border: "1px solid",
          borderColor: "divider",
          borderRadius: 8,
          px: 1.5,
          py: 0.6,
          bgcolor: "background.paper",
          color: "primary.main",
          mb: 2.5,
        }}
      >
        <AutoAwesomeOutlined sx={{ fontSize: 15 }} />
        <Typography fontSize={11} fontWeight={650}>
          Seu espaço de conhecimento
        </Typography>
      </Box>
      <Typography
        component="h2"
        variant="h4"
        fontSize={{ xs: 28, md: 36 }}
        lineHeight={1.2}
      >
        Boas ideias começam
        <br />
        com uma{" "}
        <Box component="span" sx={{ color: "primary.main" }}>
          boa pergunta.
        </Box>
      </Typography>
      <Typography color="text.secondary" fontSize={14} mt={1.5} mb={4}>
        Converse com seus documentos e explore sua base de conhecimento.
      </Typography>
      <Box
        className="portal-stagger"
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            lg: "repeat(4, 1fr)",
          },
          gap: 1.5,
        }}
      >
        {actions.map((a) => {
          const color =
            a.tone === "violet"
              ? "var(--portal-violet)"
              : theme.palette[a.tone].main;
          return (
            <Card
              key={a.title}
              sx={{
                transition:
                  "transform .22s, box-shadow .22s, border-color .22s",
                "&:hover": {
                  transform: "translateY(-5px)",
                  boxShadow: "var(--portal-shadow-hover)",
                  borderColor: color,
                },
              }}
            >
              <CardActionArea
                onClick={() => onChoose(a.prompt)}
                sx={{
                  p: 2.25,
                  height: "100%",
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "flex-start",
                  justifyContent: "flex-start",
                }}
              >
                <Box
                  sx={{
                    display: "grid",
                    placeItems: "center",
                    width: 38,
                    height: 38,
                    mb: 2,
                    borderRadius: "10px",
                    color,
                    background: `color-mix(in srgb, ${color} 12%, transparent)`,
                    "& svg": { fontSize: 21 },
                  }}
                >
                  {a.icon}
                </Box>
                <Typography fontSize={13} fontWeight={650} mb={0.7}>
                  {a.title}
                </Typography>
                <Typography
                  fontSize={11}
                  color="text.secondary"
                  lineHeight={1.7}
                >
                  {a.desc}
                </Typography>
                <ArrowForwardRounded sx={{ fontSize: 16, color, mt: 2 }} />
              </CardActionArea>
            </Card>
          );
        })}
      </Box>
      <Box
        sx={{
          mt: 3,
          p: 2.5,
          display: "flex",
          alignItems: { xs: "flex-start", sm: "center" },
          flexDirection: { xs: "column", sm: "row" },
          gap: 2,
          borderRadius: "14px",
          border: "1px solid",
          borderColor: "divider",
          background: `linear-gradient(100deg, ${alpha(theme.palette.primary.main, 0.04)}, transparent)`,
        }}
      >
        <Box flex={1}>
          <Typography fontSize={13} fontWeight={600}>
            O conhecimento começa na sua base
          </Typography>
          <Typography fontSize={12} color="text.secondary" mt={0.5}>
            Organize os conteúdos que dão contexto às suas conversas.
          </Typography>
        </Box>
        {hasPermission("kb.view") && (
          <Button
            component={Link}
            to="/kb"
            size="small"
            endIcon={<ArrowForwardRounded />}
          >
            Explorar base
          </Button>
        )}
      </Box>
    </Box>
  );
}
