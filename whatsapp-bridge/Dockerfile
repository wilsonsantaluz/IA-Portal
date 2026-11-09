FROM node:24-bookworm-slim
ENV PUPPETEER_SKIP_DOWNLOAD=true CHROMIUM_PATH=/usr/bin/chromium NODE_ENV=production BRIDGE_DATA=/data
RUN apt-get update && apt-get install -y --no-install-recommends chromium ca-certificates fonts-liberation \
    && rm -rf /var/lib/apt/lists/* && mkdir /data && chown node:node /data
WORKDIR /app
COPY package*.json ./
RUN npm ci --omit=dev && npm cache clean --force
COPY src ./src
USER node
EXPOSE 3010
CMD ["node", "src/server.js"]
