import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "./AuthProvider";
import React from "react";

export default function PublicRoute({ children }: { children: JSX.Element }) {
  const { user } = useAuth();
  const location = useLocation();

  const from = (location.state as any)?.from?.pathname || "/dashboard";

  if (user && user.token) {
    return <Navigate to={from} replace />;
  }

  return children;
}
