import { Box, Typography } from "@mui/material";

// Typographic placeholder only. Replace the monogram with the reviewed SVG in a later visual review.
export default function Brand({ compact = false }: { compact?: boolean }) {
  return <Box display="flex" alignItems="center" gap={1.3}>
    <Box aria-hidden="true" sx={{
      position: "relative", width: 34, height: 34, flexShrink: 0, borderRadius: "9px",
      display: "grid", placeItems: "center", bgcolor: "primary.main", color: "primary.contrastText",
      fontSize: 15, fontWeight: 750, letterSpacing: "-.06em",
      "&::after": { content: '""', position: "absolute", top: -2, right: -2, width: 8, height: 8, borderRadius: "50%", bgcolor: "var(--portal-violet)", border: "2px solid var(--portal-sidebar)" },
    }}>IA</Box>
    {!compact && <Typography fontSize={21} fontWeight={750} letterSpacing="-.04em" color="text.primary">IA-Portal</Typography>}
  </Box>;
}
