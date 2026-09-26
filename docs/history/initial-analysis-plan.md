# Planejamento de Arquitetura de Microserviços
### Aplicação de Cartas com .NET, PostgreSQL, MongoDB e Redis

---

## BFF — Backend For Frontend

### O que é BFF?

**BFF (Backend For Frontend)** é um serviço criado **especificamente para servir um front-end**, agregando e transformando dados de múltiplas fontes para entregar exatamente o que a tela precisa.

```
                                    APIs Externas
                                   ┌─────────────┐
                     ┌────────────►│  API Preços │
                     │             └─────────────┘
Front-end ──► BFF ───┤             ┌─────────────┐
                     ├────────────►│  API Cartas │
                     │             └─────────────┘
                     │             ┌─────────────┐
                     └────────────►│  API Stock  │
                                   └─────────────┘
```

O front faz **uma chamada** pro BFF, e o BFF faz **várias chamadas** para APIs externas, junta tudo e devolve formatado!

---

### BFF vs Microserviço Comum

| | Microserviço Comum | BFF |
|---|---|---|
| **Serve** | Outros serviços / qualquer cliente | Um front-end específico |
| **Responsabilidade** | Regra de negócio própria | Agregar e transformar dados |
| **Banco de dados** | Normalmente tem o seu | Normalmente não tem (ou só cache) |
| **Quem chama** | Gateway / outros serviços | Diretamente o front-end |
| **Exemplo** | Auth Service, Wishlist Service | Juntar preço + disponibilidade + imagem da carta |

---

### Exemplo Prático

**Sem BFF**, o front teria que fazer isso:
```
Front-end:
    ├── fetch(API_Preços/card/1)      → { price: 10.50 }
    ├── fetch(API_Disponibilidade/1)  → { inStock: true }
    └── fetch(API_Imagens/card/1)     → { imageUrl: "..." }
        Montar o objeto final no front...
```

**Com BFF**, o front faz isso:
```
Front-end:
    └── fetch(BFF/card/1) → {
                                price: 10.50,
                                inStock: true,
                                imageUrl: "...",
                                lastUpdated: "..."
                            }
```

---

---

## Visão Geral da Arquitetura

```
                        ┌──────────────────┐
                        │    FRONT-END     │
                        └────────┬─────────┘
                                 │
                        ┌────────▼─────────┐
                        │   API GATEWAY    │
                        │     (Nginx)      │
                        └────────┬─────────┘
               ┌─────────────────┼────────────────────┐
               │                 │                    │
      ┌────────▼────────┐ ┌──────▼───────┐ ┌─────────▼───────┐
      │  Auth Service   │ │   Wishlist   │ │    Cards BFF    │
      │    (.NET)       │ │   Service    │ │    (.NET)       │
      │                 │ │   (.NET)     │ │                 │
      │ • Login         │ │              │ │ • Agrega APIs   │
      │ • Register      │ │ • CRUD de    │ │   externas      │
      │ • Google OAuth  │ │   cartas     │ │ • Formata dados │
      │ • JWT           │ │   desejadas  │ │ • Cache Redis   │
      └────────┬────────┘ └──────┬───────┘ └─────────┬───────┘
               │                 │                    │
      ┌────────▼────────┐ ┌──────▼───────┐    ┌──────▼──────────────┐
      │   PostgreSQL    │ │   MongoDB    │    │     Redis Cache     │
      │                 │ │              │    │  + APIs Externas    │
      │ • users         │ │ • wishlists  │    │                     │
      │ • oauth_tokens  │ │ • cards ref  │    │ • API de Preços     │
      └─────────────────┘ └──────────────┘    │ • API de Estoque    │
                                              └─────────────────────┘
```

---

## 1. Auth Service — .NET + PostgreSQL

### Responsabilidades
- Registro com e-mail/senha
- Login com e-mail/senha
- Login social (Google, Discord, etc.)
- Emissão e renovação de JWT

### Endpoints
```
POST   /auth/register
POST   /auth/login
POST   /auth/refresh-token
POST   /auth/logout
GET    /auth/google                ← redireciona para o Google
GET    /auth/google/callback       ← Google redireciona de volta aqui
```

### Schema PostgreSQL
```sql
-- Usuário base
users
├── id          UUID PRIMARY KEY
├── email       VARCHAR UNIQUE
├── name        VARCHAR
├── avatar_url  VARCHAR
├── created_at  TIMESTAMP
└── updated_at  TIMESTAMP

-- Credenciais locais (e-mail/senha)
user_credentials
├── id           UUID PRIMARY KEY
├── user_id      UUID → FK users
└── password_hash VARCHAR

-- Tokens de redes sociais
user_oauth
├── id           UUID PRIMARY KEY
├── user_id      UUID → FK users
├── provider     VARCHAR  (google, discord...)
├── provider_id  VARCHAR  (id do usuário na rede social)
└── access_token VARCHAR
```

### Conceitos Envolvidos

| Conceito | Detalhe |
|---|---|
| **OAuth 2.0** | Protocolo usado pelo Google/Discord para login social |
| **OpenID Connect** | Camada de identidade em cima do OAuth 2.0 |
| **JWT** | Access Token (curta duração) + Refresh Token (longa duração) |
| **Bcrypt** | Hash de senhas — nunca salvar senha em texto puro! |

> No .NET use a lib **AspNet.Security.OAuth.Providers** para login social e **System.IdentityModel.Tokens.Jwt** para JWT.

---

## 2. Wishlist Service — .NET + MongoDB

### Responsabilidades
- Gerenciar lista de cartas desejadas por usuário
- Associar uma carta da wishlist a dados de preço do BFF

### Endpoints
```
GET    /wishlist          ← lista cartas do usuário logado
POST   /wishlist          ← adiciona carta
DELETE /wishlist/:cardId  ← remove carta
PATCH  /wishlist/:cardId  ← atualiza prioridade/anotação
```

### Schema MongoDB
```json
{
  "_id": "ObjectId",
  "userId": "uuid-do-usuario",
  "cards": [
    {
      "cardId": "card-123",
      "cardName": "Black Lotus",
      "addedAt": "ISODate",
      "priority": "high",
      "notes": "Quero para o deck X"
    }
  ],
  "updatedAt": "ISODate"
}
```

### Por que MongoDB aqui?

Cartas têm estruturas variadas dependendo do jogo (Magic, Pokémon, etc.). O MongoDB é **schemaless**, então você não precisa de migrations a cada novo campo!

---

## 3. Cards BFF — .NET + Redis

### Responsabilidades
- Receber uma chamada do front
- Chamar APIs externas em paralelo
- Agregar e formatar a resposta
- Cachear no Redis para não bater na API externa toda vez

### Endpoints
```
GET /cards/search?name=lotus     ← busca cartas por nome
GET /cards/:cardId               ← detalhes + preço de uma carta
GET /cards/:cardId/prices        ← só preços de uma carta
GET /cards/wishlist-summary      ← resumo de preços da wishlist inteira
```

### Como Funciona Internamente
```
Front → GET /cards/black-lotus
             │
             ▼
        Cards BFF
             │
             ├── Redis tem cache? ──► SIM → retorna na hora
             │
             └── NÃO → chama em paralelo:
                          ├── API Scryfall (dados da carta)
                          ├── API TCGPlayer (preços)
                          └── API CardMarket (preços EU)
                               │
                               ▼
                        Agrega tudo num objeto só
                               │
                               ▼
                        Salva no Redis (TTL: 1 hora)
                               │
                               ▼
                        Retorna pro front
```

### Conceitos Envolvidos

| Conceito | Detalhe |
|---|---|
| **Cache com TTL** | Preço de carta não muda a cada segundo — cache de 1h já resolve |
| **Parallel Requests** | Chamar múltiplas APIs ao mesmo tempo (Task.WhenAll no .NET) |
| **Circuit Breaker** | Se a API externa cair, retorna cache antigo ou erro amigável |
| **Rate Limiting** | APIs externas têm limite de chamadas — o cache protege isso |

---

## API Gateway — Nginx

O Gateway é o **único ponto de contato do front-end** com o backend.

```
Front-end chama → /api/auth/login     → roteia para → Auth Service
Front-end chama → /api/wishlist       → roteia para → Wishlist Service
Front-end chama → /api/cards/card/1   → roteia para → Cards BFF
```

### Responsabilidades do Gateway
- Roteamento de requisições
- Validação do JWT (antes de chegar nos serviços)
- Rate Limiting global
- CORS centralizado
- Logs e monitoramento

---

## Docker Compose de Desenvolvimento

```yaml
# docker-compose.dev.yml
services:

  auth-service:
    build:
      context: ./auth-service
      dockerfile: Dockerfile.dev
    ports:
      - "5001:8080"
    volumes:
      - ./auth-service:/app
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Postgres=Host=auth-db;Database=auth;Username=postgres;Password=postgres
      - Jwt__Secret=dev_super_secret_key
      - Google__ClientId=seu_client_id
      - Google__ClientSecret=seu_client_secret
    depends_on:
      - auth-db

  wishlist-service:
    build:
      context: ./wishlist-service
      dockerfile: Dockerfile.dev
    ports:
      - "5002:8080"
    volumes:
      - ./wishlist-service:/app
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Mongo=mongodb://mongo:27017/wishlist

  cards-bff:
    build:
      context: ./cards-bff
      dockerfile: Dockerfile.dev
    ports:
      - "5003:8080"
    volumes:
      - ./cards-bff:/app
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - Redis__Url=redis://redis:6379
      - ExternalApis__Scryfall=https://api.scryfall.com

  auth-db:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: auth
    volumes:
      - auth-db-data:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  mongo:
    image: mongo:7
    volumes:
      - mongo-data:/data/db
    ports:
      - "27017:27017"

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

volumes:
  auth-db-data:
  mongo-data:
```

---

## Estrutura de Pastas

```
my-app/
├── docker-compose.dev.yml
├── docker-compose.yml
├── nginx/
│   └── nginx.conf
│
├── auth-service/
│   ├── Dockerfile
│   ├── Dockerfile.dev
│   └── AuthService/
│       ├── Controllers/
│       ├── Services/
│       ├── Models/
│       └── Program.cs
│
├── wishlist-service/
│   ├── Dockerfile
│   ├── Dockerfile.dev
│   └── WishlistService/
│       ├── Controllers/
│       ├── Services/
│       ├── Models/
│       └── Program.cs
│
├── cards-bff/
│   ├── Dockerfile
│   ├── Dockerfile.dev
│   └── CardsBff/
│       ├── Controllers/
│       ├── Aggregators/
│       ├── Clients/
│       └── Program.cs
│
└── frontend/
    └── (seu front, roda fora do Docker em dev)
```

---

## Desenvolvimento Local com Docker

### O Segredo: Volumes + Hot Reload

O Docker permite **mapear uma pasta do seu computador para dentro do container**. Assim, quando você salva um arquivo, o container já enxerga a mudança automaticamente.

### Quando Rebuildar é Necessário?

| Situação | Precisa de rebuild? |
|---|---|
| Editar um .cs | ❌ Hot reload cuida |
| Instalar um novo pacote (NuGet) | ✅ Sim (--build) |
| Mudar o Dockerfile | ✅ Sim (--build) |
| Mudar variável de ambiente no compose | ✅ Sim (restart) |
| Salvar e testar lógica normal | ❌ Só salva e testa! |

### Comandos Úteis do Dia a Dia

```bash
# Subir tudo pela primeira vez
docker compose -f docker-compose.dev.yml up --build

# Nos outros dias
docker compose -f docker-compose.dev.yml up

# Ver logs de um serviço específico
docker compose logs -f auth-service

# Reiniciar só um serviço
docker compose up -d --build auth-service

# Entrar dentro de um container
docker compose exec auth-service sh

# Derrubar tudo mas manter os dados
docker compose down

# Derrubar tudo E apagar os dados (reset total)
docker compose down -v
```

---

## Roadmap de Implementação

```
Fase 1 → Docker Compose + Auth Service (Login/JWT)
    │
Fase 2 → API Gateway (Nginx)
    │
Fase 3 → Wishlist Service + integração com Auth
    │
Fase 4 → Cards BFF + Redis Cache
    │
Fase 5 → Login Social (Google OAuth)
    │
Fase 6 → Observabilidade (logs, health checks)
```

---

## Conceitos para Entrevistas — Resumo Final

| Serviço | Conceitos |
|---|---|
| **Auth Service** | OAuth 2.0, OpenID Connect, JWT, Bcrypt, PostgreSQL |
| **Wishlist Service** | CRUD, MongoDB, Autenticação por recurso |
| **Cards BFF** | BFF Pattern, Cache Redis, Parallel Requests, Circuit Breaker |
| **Infra** | Docker, API Gateway, Microserviços, variáveis de ambiente |

---

> Só de conseguir **explicar a diferença entre um BFF e um microserviço comum**, e **por que usou cada banco de dados**, você já demonstra um nível de maturidade técnica que a maioria dos candidatos júnior/pleno não tem!
