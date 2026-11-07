import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
} from "react";
import type { ReactNode } from "react";
import { login as loginApi } from "./authService";
import api from "../services/api";

type AuthUser = {
  organizationId: number;
  userId: number;
  userName: string;
  email: string;
  roles: string[];
  permissions: string[];
  token: string;
};
type AuthContextType = {
  user: AuthUser | null;
  ready: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  hasPermission: (perm: string) => boolean;
  refresh: () => Promise<void>;
};
const AuthContext = createContext<AuthContextType | null>(null);
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [ready, setReady] = useState(false);
  const refresh = useCallback(async () => {
    let stored: AuthUser | null = null;
    try {
      stored = JSON.parse(localStorage.getItem("auth") || "null");
    } catch {
      localStorage.removeItem("auth");
    }
    if (!stored?.token) {
      setUser(null);
      setReady(true);
      return;
    }
    try {
      const { data } = await api.get<Omit<AuthUser, "token">>("api/auth/me");
      const current = { ...data, token: stored.token };
      setUser(current);
      localStorage.setItem("auth", JSON.stringify(current));
    } catch {
      setUser(null);
    } finally {
      setReady(true);
    }
  }, []);
  useEffect(() => {
    void refresh();
    const update = () => {
      if (document.visibilityState === "visible") void refresh();
    };
    window.addEventListener("focus", update);
    const timer = window.setInterval(update, 30000);
    return () => {
      window.removeEventListener("focus", update);
      clearInterval(timer);
    };
  }, [refresh]);
  async function login(email: string, password: string) {
    const data = await loginApi(email, password);
    // Conversations are scoped to the account; do not reuse another account's session id.
    localStorage.removeItem("portal_session_id");
    setUser(data);
    setReady(true);
    localStorage.setItem("auth", JSON.stringify(data));
  }
  function logout() {
    setUser(null);
    localStorage.removeItem("auth");
    localStorage.removeItem("portal_session_id");
    window.location.href = "/login";
  }
  return (
    <AuthContext.Provider
      value={{
        user,
        ready,
        login,
        logout,
        refresh,
        hasPermission: (perm) => user?.permissions.includes(perm) ?? false,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("AuthContext não encontrado.");
  return ctx;
}
