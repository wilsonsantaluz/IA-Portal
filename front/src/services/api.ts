import axios from "axios";

const api = axios.create({
  baseURL: (import.meta.env.VITE_API_URL || "/").replace(/\/?$/, "/")
});

// 🔐 Injeta JWT automaticamente
api.interceptors.request.use(config => {
  const raw = localStorage.getItem("auth");
  if (raw) {
    const { token, organizationId } = JSON.parse(raw);
    if (organizationId && !config.headers.has("X-Organization-Id")) config.headers["X-Organization-Id"] = String(organizationId);
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  }
  return config;
});

// 🚪 Opcional: logout automático em 401
api.interceptors.response.use(
  r => r,
  err => {
    if (err.response?.status === 401) {
      localStorage.removeItem("auth");
      if (window.location.pathname !== "/login") {
        const invitation = window.location.pathname === "/invite" && /^#invite=[a-fA-F0-9]{64}$/.test(window.location.hash) ? window.location.hash : "";
        window.location.href = "/login" + invitation;
      }
    }
    return Promise.reject(err);
  }
);

export default api;
