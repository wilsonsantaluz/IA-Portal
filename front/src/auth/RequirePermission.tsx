import { Alert, Box, Button, Typography } from "@mui/material";
import { Link, Navigate } from "react-router-dom";
import { useAuth } from "./AuthProvider";
export const screenRoutes = [
  ["chat.access", "/chat"],
  ["dashboard.view", "/dashboard"],
  ["kb.view", "/kb"],
  ["access.manage", "/users"],
  ["usage.admin", "/usage"],
  ["ai.manage", "/settings/ai"],
  ["audit.read", "/audit"],
];
export function HomeRoute() {
  const { hasPermission } = useAuth();
  const destination = screenRoutes.find(([permission]) =>
    hasPermission(permission),
  );
  return destination ? (
    <Navigate to={destination[1]} replace />
  ) : (
    <Box p={4}>
      <Alert severity="info">
        Sua conta está ativa, mas ainda não possui acesso a telas. Solicite a
        associação a um grupo ao administrador.
      </Alert>
    </Box>
  );
}
export function RequireAnyPermission({ permissions, children }: { permissions: string[]; children: JSX.Element }) {
  const { hasPermission } = useAuth();
  return permissions.some(hasPermission) ? children : <RequirePermission permission={permissions[0]}>{children}</RequirePermission>;
}
export default function RequirePermission({
  permission,
  children,
}: {
  permission: string;
  children: JSX.Element;
}) {
  const { hasPermission } = useAuth();
  return hasPermission(permission) ? (
    children
  ) : (
    <Box p={4}>
      <Typography variant="h5" mb={2}>
        Acesso não permitido
      </Typography>
      <Alert severity="warning">
        Seu grupo não possui permissão para acessar esta tela.
      </Alert>
      <Button component={Link} to="/" sx={{ mt: 2 }}>
        Voltar ao início
      </Button>
    </Box>
  );
}
