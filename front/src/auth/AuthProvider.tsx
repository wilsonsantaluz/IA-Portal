import { createContext, useContext, useEffect, useState } from "react";
import type { ReactNode, Context } from "react";
import { login as loginApi } from "./authService";

type AuthUser = {
  userId: number;
  userName: string;
  email: string;
  roles: string[];
  permissions: string[];
  token: string;
};

type AuthContextType = {
  user: AuthUser | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  hasPermission: (perm: string) => boolean;
};

const AuthContext: Context<AuthContextType | null> =
  createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);

  useEffect(() => {
    const raw = localStorage.getItem("auth");
    if (raw) setUser(JSON.parse(raw));
  }, []);

  async function login(email: string, password: string) {
    const data = await loginApi(email, password);
    setUser(data);
    localStorage.setItem("auth", JSON.stringify(data));
  }

  function logout() {
    setUser(null);
    localStorage.removeItem("auth");
    window.location.href = "/login";
  }



  function hasPermission(perm: string) {
    return user?.permissions.includes(perm) ?? false;
  }

  return (
    <AuthContext.Provider value={{ user, login, logout, hasPermission }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("AuthContext não encontrado");
  return ctx;
}
