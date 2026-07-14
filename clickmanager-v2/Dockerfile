# Front-end estático: sem build, é HTML/CSS/JS puro servido pelo nginx.
FROM nginx:1.27-alpine

COPY . /usr/share/nginx/html/
COPY nginx.conf /etc/nginx/conf.d/default.conf
RUN rm -f /usr/share/nginx/html/Dockerfile /usr/share/nginx/html/nginx.conf

EXPOSE 8080
