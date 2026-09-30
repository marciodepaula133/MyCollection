FROM nginx:1.30-alpine

# Remove the image's default server block so our template (rendered by the base image's
# docker-entrypoint envsubst step into /etc/nginx/conf.d/nginx.conf) is the only one.
RUN rm -f /etc/nginx/conf.d/default.conf

COPY apps/gateway/templates/nginx.conf.template /etc/nginx/templates/nginx.conf.template

EXPOSE 8080
