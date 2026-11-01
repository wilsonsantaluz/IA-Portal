import type { ReactNode } from "react";
import { Card, Box, Typography, Skeleton } from "@mui/material";
import { alpha, useTheme } from "@mui/material/styles";

type Props = {
  title: string;
  value: string | number;
  caption: string;
  tone?: "primary" | "secondary" | "success" | "warning";
  icon: ReactNode;
  loading?: boolean;
};
export function DashboardCard({
  title,
  value,
  caption,
  tone = "primary",
  icon,
  loading,
}: Props) {
  const theme = useTheme();
  const color = theme.palette[tone].main;
  return (
    <Card
      sx={{
        p: 2.5,
        height: "100%",
        minWidth: 0,
        position: "relative",
        overflow: "hidden",
        transition:
          "transform .25s ease, box-shadow .25s ease, border-color .25s ease",
        "&:hover": {
          transform: "translateY(-4px)",
          boxShadow: "var(--portal-shadow-hover)",
          borderColor: alpha(color, 0.4),
        },
        "&::after": {
          content: '""',
          position: "absolute",
          right: -35,
          bottom: -55,
          width: 135,
          height: 135,
          borderRadius: "50%",
          background: `radial-gradient(circle, ${alpha(color, 0.08)}, transparent 70%)`,
          pointerEvents: "none",
        },
      }}
    >
      <Box
        display="flex"
        alignItems="center"
        justifyContent="space-between"
        gap={1}
        mb={2}
      >
        <Typography fontSize={13} fontWeight={550} color="text.secondary">
          {title}
        </Typography>
        <Box
          sx={{
            display: "grid",
            placeItems: "center",
            width: 42,
            height: 42,
            borderRadius: "12px",
            color,
            background: `linear-gradient(135deg, ${alpha(color, 0.16)}, ${alpha(color, 0.06)})`,
            border: `1px solid ${alpha(color, 0.12)}`,
            "& svg": { fontSize: 22 },
          }}
        >
          {icon}
        </Box>
      </Box>
      <Typography
        component="div"
        fontSize={32}
        fontWeight={750}
        letterSpacing="-.045em"
        lineHeight={1.2}
        sx={{ fontVariantNumeric: "tabular-nums" }}
      >
        {loading ? <Skeleton width="55%" /> : value}
      </Typography>
      <Typography fontSize={11} color="text.secondary" mt={1.2}>
        {caption}
      </Typography>
    </Card>
  );
}
