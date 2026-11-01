import { Navigate, useLocation } from "react-router-dom";
import { Box, CircularProgress } from "@mui/material";
import { useAuth } from "./AuthProvider";
export default function RequireAuth({ children }: { children: JSX.Element }) {
  const { user, ready } = useAuth();
  const location = useLocation();
  if (!ready)
    return (
      <Box display="grid" minHeight="100dvh" sx={{ placeItems: "center" }}>
        <CircularProgress aria-label="Carregando sessão" />
      </Box>
    );
  if (!user?.token)
    return <Navigate to="/login" replace state={{ from: location }} />;
  return children;
}
