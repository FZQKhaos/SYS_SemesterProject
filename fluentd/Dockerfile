# Basis-imaget har ikke Elasticsearch-pluginet, så vi bygger vores eget image med det installeret.
FROM fluent/fluentd:v1.17-1

USER root
# elasticsearch-gem'en låses til version 8, så den passer til Elasticsearch 8.
# (Version 9 sender en header, som Elasticsearch 8 afviser med "Invalid media-type".)
RUN gem install elasticsearch -v 8.15.0 --no-document \
 && gem install fluent-plugin-elasticsearch --no-document
