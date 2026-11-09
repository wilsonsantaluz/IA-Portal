# =======================================================
# 1) Build da aplicação (usa Node 22)
# =======================================================
FROM node:22-alpine AS build
ARG VITE_MINIMAL_HOSTING=false
ENV VITE_MINIMAL_HOSTING=$VITE_MINIMAL_HOSTING

WORKDIR /app

COPY package.json yarn.lock ./
RUN yarn install --frozen-lockfile

COPY . .
RUN yarn typecheck && yarn build


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
