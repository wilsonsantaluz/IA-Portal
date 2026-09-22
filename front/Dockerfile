# =======================================================
# 1) Build da aplicação (usa Node 20 LTS)
# =======================================================
FROM node:20-alpine AS build

WORKDIR /app

COPY package.json yarn.lock ./
RUN yarn install --frozen-lockfile

COPY . .
RUN yarn build


# =======================================================
# 2) Servir a aplicação via NGINX (produção real)
# =======================================================
FROM nginx:1.25-alpine

# Remove config padrão
RUN rm -rf /etc/nginx/conf.d/*

# Copia configuração otimizada
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copia os arquivos gerados pelo Vite
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
