import api from "../services/api";


export async function login(email: string, password: string) {
  const { data } = await api.post("/api/auth/login", {
    email,
    password
  });

  return data;
}
