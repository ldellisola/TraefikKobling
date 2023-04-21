# Traefik Kobling
A dynamic traefik to traefik discovery agent.

*"Kobling"* means *"coupling"* or *"linkink"* in norwegian. Traefik Kobling lets homelab
users to link Traefik instances on diferent hosts to a main, public-facing Traefik instance
without using docker swarm or publishing ports.

Going forward, we will refer to the main, publi-facing Traefik instance as the main instance
and the others will be called local instances.

This is acomplished by using Traefik's API on the local instances to find out which rules
are registered and then publish them to redis, so the main instance can read use them.

## Usage
For the main traefik instance, you have to configure it to use the redis provider in the `traefik.yml`:
```yml
providers:
  redis:
    endpoints:
      # change the address if the redis instance is not available in local host
      - "localhost:6379"
  # other providers
```
Also, if you want to use HTTPS, it should be done in this instance.

Configure all your local traefik instances to enable api access in their `traefik.yml`:
```yml
api:
  insecure: true
```
These instances do not (and probably should not) have HTTPS enabled.

And then we can set up Traefik Kobling on next to your main instance using docker:
```yml
version: "3"
services:
  traefik-kobling:
    image: ldellisola/traefik-kobling
    volumes:
      - ./config.yml:/config.yml
    environment:
      REDIS_URL: "localhost:6379"
```
And now we can finally write our config file `config.yml`:
```yml
servers:
  - name: "other-host"
    apiAddress: http://192.168.0.10:8080
    destinationAddress: http://192.168.0.10
```
## Configuration
There are two places where you can configure this application. First there are some
environment variables:
- REDIS_URL: It is a mandatory string specifying where the redis database is located.
- CONFIG_PATH: It let's the user change the location of the `config.yml` file. The default
location is `/config.yml`.
- RUN_EVERY: It specifies how many seconds to wait before checking the local instances for
changes. The default value is 60 seconds.

You must also create a `config.yml`. This file contains a list of servers with information
about what is the address of the local traefik's instance is and where the traffic should 
be redirected.
```yml
servers:
  - name: "host-1"
    apiAddress: http://192.168.0.10:8080
    destinationAddress: http://192.168.0.10
    
  - name: "host-2"
    apiAddress: http://192.168.0.11:8080
    destinationAddress: http://192.168.0.11
    
  - name: "host-3"
    apiAddress: http://192.168.0.12:8080
    destinationAddress: http://192.168.0.12
```

## Example
So what does this mean?

Let's say we have 2 machines in our home network:
- Machine A is exposing port 80 and 443 to the internet and it is running the following containers:
  - traefik: this is our main instance and has the dashboard exposed in the FQDN `main.doman.tld`
  - redis: its a storage for the main instance to use as a provider.
  - traefik koblink: it will read data from the traefik instance in machine B and provide redirect data
  for the main traefik instance.

For this machine we will deploy the following `docker-compose.yml` file:
```yml
version: "3"

networks:
  web:
    name: "web"

services:
  traefik:
    image: traefik:latest
    ports:
      - 80:80
      - 443:443
    environment:
      CF_API_EMAIL: ${CF_API_EMAIL}
      CF_API_KEY: ${CF_API_KEY}
    networks:
      - web
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - ./traefik.yml:/traefik.yml
      - ./acme.json:/acme.json
      
  redis:
    image: redis:latest

  traefik-kobling:
    image: ldellisola/traefik-kobling
    volumes:
      - ./config.yml:/config.yml
    environment:
      REDIS_URL: "redis:6379"
```

The `traefik.yml` looks like:
```yml
api:
  dashboard: true
  
entryPoints:
  web:
    address: ":80"
    http:
        redirections:
            entrypoint:
                to: web-secure
                scheme: https

  web-secure:
    address: ":433"
    http:
      tls:
        certResolver: "cloudflare"
        domains:
          - main: "domain.tld"
            sans:
              - "*.domain.tld"

serversTransport:
  insecureSkipVerify: true

providers:
  redis:
    endpoints:
      - "redis:6379"

certificatesResolvers:
  cloudflare:
    acme:
      email: me@domain.tld
      storage: /acme.json
      dnsChallenge:
        provider: cloudflare
        resolvers:
          - "1.1.1.1:53"
          - "1.0.0.1:53"
```

The `config.yml` looks like:
```yml
servers:
  - name: "machine-b"
    apiAddress: http://192.168.0.10:8080
    destinationAddress: http://192.168.0.10
```
  
- Machine B is on IP 192.168.0.10 and it runs:
  - traefik: this is a local instance and has the FQDN `local.domain.tld`
  - Service B: another random service hosted in this server, with the FQDN `serviceB.domain.tld`
 
It is deployed with the following `docker-compose.yml`:
```yml
version: "3"

networks:
  web:
    name: "web"

services:
  traefik:
    image: traefik:latest
    ports:
      - 80:80
      - 8080:8080
    networks:
      - web
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - ./traefik.yml:/traefik.yml
    labels:
      traefik.enable: "true"
      traefik.http.routers.traefik.rule: "Host(`local.domain.tld`)
      traefik.http.routers.traefik.service: "api@internal"
      traefik.http.services.traefik.loadbalancer.server.port: "8080"

  service-b:
    image: service-b:latest
    networks:
      - web
    labels:
      traefik.enable: "true"
      traefik.http.routers.service-b.rule: "Host(`serviceB.domain.tld`)
      traefik.http.services.service-b.loadbalancer.server.port: "8080"
```

And the `traefik.yml` is:
```yml
api:
    insecure: true
    
entryPoints:
    web:
        address: ":80"

providers:
    docker:
        endpoint: "unix:///var/run/docker.sock"
        exposedByDefault: false
        network: web
```

The main traefik instance is set up with https but the local ones do not have to, and both
instances are set up to redirect trafic to the services within the machine according to their
domain name.

So, if we want to access `serviceB.domain.tld`, the request should be redirected like:
```
[internet] -- serviceB.domain.tld --> [main traefik]
[main traefik] -- serviceB.domain.tld --> [local traefik] on 192.168.0.10
[local traefik] --> [Service B]
```
## License
- Traefik Kobling: MIT, (c) 2022, Pixelcop Research, Inc.
- traefik: MIT, Copyright (c) 2016-2020 Containous SAS; 2020-2023 Traefik Labs
