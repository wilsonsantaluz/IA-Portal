import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "./AuthProvider";
import React from "react";

export default function RequireAuth({ children }: { children: JSX.Element }) {
  const { user } = useAuth();
  const location = useLocation();

  if (!user || !user.token) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return children;
}
