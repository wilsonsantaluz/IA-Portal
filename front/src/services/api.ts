import axios from "axios";

const api = axios.create({
  baseURL: (import.meta.env.VITE_API_URL || "/").replace(/\/?$/, "/")
});

// 🔐 Injeta JWT automaticamente
api.interceptors.request.use(config => {
  const raw = localStorage.getItem("auth");
  if (raw) {
    const { token } = JSON.parse(raw);
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
      window.location.href = "/login";
    }
    return Promise.reject(err);
  }
);

export default api;
