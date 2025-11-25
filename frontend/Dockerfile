FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci && for f in node_modules/.bin/*; do sed -i 's/\\r$//' "$f" || true; chmod +x "$f"; done

COPY . .
RUN node ./node_modules/@angular/cli/bin/ng build

FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/frontend /usr/share/nginx/html
RUN if [ -d /usr/share/nginx/html/browser ]; then \
      cp -r /usr/share/nginx/html/browser/. /usr/share/nginx/html/ && \
      rm -rf /usr/share/nginx/html/browser; \
    fi
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
