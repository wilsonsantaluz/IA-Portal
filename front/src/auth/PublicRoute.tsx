import { Navigate } from "react-router-dom";
import { useAuth } from "./AuthProvider";
export default function PublicRoute({ children }: { children: JSX.Element }) {
  const { user, ready } = useAuth();
  if (!ready) return null;
  return user?.token ? <Navigate to="/" replace /> : children;
}
