# Stage 1: Build
FROM node:20-alpine AS build
WORKDIR /app

ARG NEXT_PUBLIC_API_BASE_URL=http://localhost:8080
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL

COPY source/Testurio.Web/package.json source/Testurio.Web/package-lock.json ./
RUN npm ci --ignore-scripts

COPY source/Testurio.Web/ .

ENV NODE_ENV=production
RUN npm run build

# Stage 2: Runtime
FROM gcr.io/distroless/nodejs20-debian12 AS final
WORKDIR /app

ENV NODE_ENV=production
ENV HOSTNAME=0.0.0.0

EXPOSE 3000

COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public

CMD ["server.js"]
