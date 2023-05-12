#docker run -d --hostname autocomplete-rabbit-mq --name autocomplete-rabbit-mq -p 5672:5672 -p 15672:15672 rabbitmq:3
docker run -p 15672:15672 -p 5672:5672 masstransit/rabbitmq
