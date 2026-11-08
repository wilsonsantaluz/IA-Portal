import api from "../services/api";

export async function switchOrganization(id: number, destination: "/" | "/organizations" = "/") {
  const stored = JSON.parse(localStorage.getItem("auth") || "null");
  if (!stored?.token) throw new Error("Sessão indisponível.");
  const { data } = await api.get("/api/auth/me", { headers: { "X-Organization-Id": String(id) } });
  localStorage.setItem("auth", JSON.stringify({ ...data, token: stored.token }));
  // Full navigation clears old tenant views and in-flight component state.
  window.location.assign(destination);
}
