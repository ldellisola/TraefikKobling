# Traefik Kobling

A dynamic traefik to traefik discovery agent.

*"Kobling"* means *"coupling"* or *"linking"* in Norwegian. Traefik Kobling lets homelab
users link Traefik instances on different hosts to a main, public-facing Traefik instance
without using Docker Swarm or publishing ports.

Going forward, we will refer to the main, public-facing Traefik instance as the main instance
and the others will be called local instances.

This is accomplished by using Traefik's API on the local instances to find out which rules
are registered and then publish them to redis, so the main instance can read and use them.

## Usage

For the main traefik instance, you have to configure it to use the redis provider in `traefik.yml`:

```yml
providers:
  redis:
    endpoints:
      - "localhost:6379" # change the address if the redis instance is not available in local host
  # other providers
```

Also, if you want to use HTTPS, it should be done in this instance.

Configure all your local traefik instances to have API access enabled in their `traefik.yml`:
- If the local traefik instances are located within your own local network, then you can allow insecure access and connect to it over port 8080:
```yml
api:
  insecure: true
```

- if your local traefik instances can be accessed through the internet, then you should not be allowing insecure access and you should set up some sort
of authentication. Traefik Kobling only supports Basic Auth and here's a [guide](https://doc.traefik.io/traefik/operations/api/) on how to set it up in your instance.

These instances do not (and probably should not) have HTTPS enabled.

We can write our config file `config.yml`:

```yml
servers:
  - name: "other-host"
    apiAddress: http://192.168.0.10:8080
    destinationAddress: http://192.168.0.10
    entryPoints:
      web: web
      websecure: web
```

And then finally, we can set up Traefik Kobling on your main instance:

```yml
services:
  traefik-kobling:
    image: ghcr.io/ldellisola/traefik-kobling
    volumes:
      - ./config.yml:/config.yml
    environment:
      REDIS_URL: "localhost:6379"
```

## Configuration

There are two places where you can configure this application. First there are some
environment variables:
- `REDIS_URL`: Specify the URL of the redis database. The default is `redis:6379`.
- `CONFIG_PATH`: It let's the user change the location of the `config.yml` file. The default
location is `/config.yml`.
- `RUN_EVERY`: It specifies how many seconds to wait before checking the local instances for
changes. The default value is 60 seconds.

You must also create a `config.yml`. This file contains a list of servers with information
about what the address of the local traefik instances are and where traffic should 
be redirected.

```yml
servers:
  - name: "host-1"
    apiAddress: http://192.168.0.10:8080
    destinationAddress: http://192.168.0.10
    entryPoints:
      web: web
      websecure: web
    
  - name: "host-2"
    apiAddress: http://192.168.0.11:8080
    destinationAddress: http://192.168.0.11
    entryPoints:
      web-tcp: local-tcp
    
  - name: "host-3"
    apiAddress: http://192.168.0.12:8080
    destinationAddress: http://192.168.0.12
```
The `entryPoints` mapping works in the following way:

The entrypoints of your main instance are on the left and the entrypoint of the local instance are on the right, where ultimately traffic will be forwarded.

This approach means we do not have to register more routers than necessary and it helps keep our main dashboard clean.

If no entrypoints are provided in the configuration, the default value `http` is used for both the main instance as well as local instances.
### Connecting to Traefik instances with Basic Auth
If your local instance has basic auth enabled, then you have to specify it in the Kobling config:
```yml
servers:
  - name: "host-1"
    apiAddress: http://username:password@192.168.0.10
    apiHost: traefik.domain.tld
    destinationAddress: http://192.168.0.10
    entryPoints:
      web: web
      websecure: web
```
The address in `apiAddress` should include the username and password to access the api and `apiHost` should be the host name for that service.

### Forwarding Services
Starting on version 0.2.0, Traefik Kobling can now identify if your internal router references a service that is not defined in the internal traefik instance.

So if you have a router with a custom service, for example:
```yml
services:
  whoami:
    ...
    labels:
      ...
      traefik.http.routers.utgifter-auth.service: "authentik@file"
```
It will look in the main instance for the `authentik@file` service.

### Forwarding Middlewares

Starting on version 0.2.0, Traefik Kobling can now forward middleware usage from internal instances to the main instance.
This is enabled by the `forwardMiddlewares` property on the `config.yml` file. 
This option can be controlled globally:
```yml
forwardMiddlewares: true
servers:
  - name: "host-1"
    apiAddress: http://username:password@192.168.0.10
    apiHost: traefik.domain.tld
    destinationAddress: http://192.168.0.10
    entryPoints:
      web: web
      websecure: web
```
Or in a per-server basis:
```yml
servers:
  - name: "host-1"
    apiAddress: http://username:password@192.168.0.10
    apiHost: traefik.domain.tld
    destinationAddress: http://192.168.0.10
    forwardMiddlewares: true
    entryPoints:
      web: web
      websecure: web
```
In practice, it means that whatever middleware you registered to a router:
```yml
services:
  whoami:
    ...
    labels:
      traefik.http.routers.whoami.middlewares: auth@file
```
This dependency will be brought to the main instance, and this instance will be the one responsible for finding the `auth@file` middleware.
This feature will not copy middleware definitions from internal instances into external ones.

This approach has one problem:
If your router in the internal traefik depends on a middlware that does not exists, the router will be skipped during request matching.
There are 2 ways around this:

1. Create a second router that matches to the same container but does not have the middleware dependency. You need to make sure that this new router has less priority than the original
```yml
services:
  whoami:
    ...
    labels:
      traefik.http.routers.whoami.rule: "Host(`whoami.lud.ar`)"
      # longer rule means less priority
      traefik.http.routers.whoami-test.rule: "Host(`whoami.lud.ar`) && Host(`whoami.lud.ar`)"
      traefik.http.services.whoami.loadbalancer.server.port: "80"
      traefik.http.routers.whoami.middlewares: auth@file
```
In this case, we are defining 2 routers for the `whoami` service. The main traefik instance will have both routers: `whoami` and `whoami-test` but will always match the first one because of the priority. The internal instance will have 2 routers, but `whoami` will not be valid because it is missing the middleware implementation, so all requests will match to `whoami-test` and the public instance will handle the middleware.

2. Create a mock middleware with the same name on your internal traefik instance.
```yml
http:
  middlewares:
    auth:
      redirectRegex: # a dummy, do-nothing redirect
        regex: "^/$"
        replacement: "/"
```
This way the entry on both instances will be valid, but only the one on the public instance will run the actual middleware.

## Example

So what does this mean?

Let's say we have 2 machines in our home network:
- Machine A is exposing port 80 and 443 to the internet and it is running the following containers:
  - `traefik`: this is our main instance and has the dashboard exposed in the FQDN `main.domain.tld`
  - `redis`: it's the storage for the main instance to use as a provider.
  - `traefik koblink`: it will read data from the traefik instance in machine B and provide redirect data
  for the main traefik instance.

For this machine we will deploy the following `docker-compose.yml` file:

```yml
networks:
  default:
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
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - ./traefik.yml:/traefik.yml
      - ./acme.json:/acme.json
      
  redis:
    image: redis:alpine

  traefik-kobling:
    image: ghcr.io/ldellisola/traefik-kobling
    volumes:
      - ./config.yml:/config.yml
    environment:
      REDIS_URL: "redis:6379"
```

The `traefik.yml` looks like this:

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

The `config.yml` looks like this:
```yml
servers:
  - name: "machine-b"
    apiAddress: http://192.168.0.10:8080
    destinationAddress: http://192.168.0.10
    entryPoints:
      web: local
      web-secure: local
```
  
- Machine B is on IP 192.168.0.10 and it runs:
  - `traefik`: this is a local instance and has the FQDN `local.domain.tld`
  - `Service B`: another random service hosted in this server, with the FQDN `serviceB.domain.tld`
 
It is deployed with the following `docker-compose.yml`:

```yml
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
    local:
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

```text
[internet] -- serviceB.domain.tld --> [main traefik]
[main traefik] -- serviceB.domain.tld --> [local traefik] on 192.168.0.10
[local traefik] --> [Service B]
```

### Integration with Authentik
Both additions on version 0.2.0 were made to support authentik and other domain level proxy authentication providers.
If you define the following middleware on your main traefik instance:
```yml
http:
    middlewares:
        auth:
            forwardAuth:
                address: http://authentik.domain.tld:9000/outpost.goauthentik.io/auth/traefik
                trustForwardHeader: true
                authResponseHeaders:
                  - X-authentik-username
                  - X-authentik-groups
                  - X-authentik-entitlements
                  - X-authentik-email
                  - X-authentik-name
                  - X-authentik-uid
                  - X-authentik-jwt
                  - X-authentik-meta-jwks
                  - X-authentik-meta-outpost
                  - X-authentik-meta-provider
                  - X-authentik-meta-app
                  - X-authentik-meta-version

    routers:
        authentik:
            entryPoints:
                - web
                - web-secure
            service: authentik
            rule: Host(`authentik.domain.tld`)
    services:
        authentik:
            loadBalancer:
                servers:
                    - url: http://authentik.domain.tld:9000
```
On your internal instance you define the following middleware:
```yml
http:
  middlewares:
    auth:
      redirectRegex: # a dummy, do-nothing redirect
        regex: "^/$"
        replacement: "/"
```
Then, you can define your services like:
```yml
services:
  whoami:
    ...
    labels:
      traefik.enable: "true"
      traefik.http.routers.whoami.rule: "Host(`whoami.domain.tld`)"
      traefik.http.services.whoami.loadbalancer.server.port: "80"
      traefik.http.routers.whoami.middlewares: auth@file
      traefik.http.routers.whoami-auth.rule: "Host(`whoami.domain.tld`) && PathPrefix(`/outpost.goauthentik.io/`)"
      traefik.http.routers.whoami-auth.service: "authentik@file"
```
And remember to enable the `forwardMiddlewares` feature on that server or globally.

## License
- Traefik Kobling: MIT, (c) 2023 Lucas Dell'Isola.
- traefik: MIT, Copyright (c) 2016-2020 Containous SAS; 2020-2023 Traefik Labs
